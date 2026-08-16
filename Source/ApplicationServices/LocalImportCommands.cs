using DataLayer;
using LibationFileManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ApplicationServices;

/// <summary>Outcome of one import run.</summary>
/// <param name="Imported">Books added to the library.</param>
/// <param name="Skipped">Files found but already registered to a book.</param>
public record LocalImportResult(int Imported, int Skipped);

/// <summary>
/// Adds audiobooks Libation did not download to the library: files bought DRM-free from Libro.fm,
/// Downpour or Authors Direct, ripped from CD, fetched from LibriVox, or inherited from another
/// library manager.
/// <para>
/// Deliberately not a store integration. There are no credentials, no scraping and nothing to
/// decrypt — the user already has the file, and this is a library problem rather than a download
/// one. See LOCAL_IMPORT_PROPOSAL.md.
/// </para>
/// </summary>
public static class LocalImportCommands
{
	/// <summary>
	/// Books need a locale and non-Audible ones have no real answer. Anything Audible-specific is
	/// already gated on <see cref="Book.IsAudible"/>, so this is only ever a placeholder — but it
	/// must be a name <c>AudibleApi.Localization.Get</c> recognises, in case a code path is
	/// reached that was missed.
	/// </summary>
	private const string PlaceholderLocale = "us";

	/// <summary>Shown when a file carries no artist tag, rather than inventing an empty author.</summary>
	private const string UnknownAuthor = "Unknown author";

	/// <summary>
	/// Scans a folder and adds everything new to the library.
	/// </summary>
	public static async Task<LocalImportResult> ImportFolderAsync(
		string folder,
		CancellationToken cancellationToken = default)
	{
		var skipPaths = RegisteredPaths();
		var found = new List<LocalAudiobook>();

		await foreach (var book in LocalAudiobookScanner.ScanAsync(folder, skipPaths, cancellationToken))
			found.Add(book);

		if (found.Count == 0)
			return new LocalImportResult(0, skipPaths.Count);

		var imported = await ImportAsync(found, cancellationToken);
		return new LocalImportResult(imported, skipPaths.Count);
	}

	/// <summary>
	/// Adds already-scanned books to the library.
	/// <para>
	/// Takes the same import gate as the Audible importers. They serialise to avoid inserting the
	/// same ASIN twice; this one has unique ids by construction, but it writes to the same tables
	/// and must not interleave with a library scan.
	/// </para>
	/// </summary>
	public static async Task<int> ImportAsync(
		IReadOnlyCollection<LocalAudiobook> books,
		CancellationToken cancellationToken = default)
	{
		if (books.Count == 0)
			return 0;

		await LibraryCommands.WaitImportGateAsync(cancellationToken);
		try
		{
			return Import(books);
		}
		finally
		{
			LibraryCommands.ReleaseImportGate();
		}
	}

	#region Catalogue-only entries

	/// <summary>A book you own but have no file for.</summary>
	public record CatalogueEntry(string Title, string Author, string Narrator);

	/// <summary>
	/// Parses pasted text into catalogue entries, one book per line.
	/// <para>
	/// Fields are separated by a tab, a pipe, or " - ": title, then optional author, then
	/// optional narrator. Tab and pipe are tried first because a hyphen is common inside real
	/// titles, and splitting "Rendezvous - Book 3" into a fake author would be worse than
	/// leaving the whole line as the title.
	/// </para>
	/// <para>
	/// Blank lines are ignored, as are lines starting with #, so a pasted list can carry
	/// headings or notes without needing to be cleaned up first.
	/// </para>
	/// </summary>
	public static IReadOnlyList<CatalogueEntry> ParseCatalogueList(string? text)
	{
		if (string.IsNullOrWhiteSpace(text))
			return [];

		var entries = new List<CatalogueEntry>();

		foreach (var rawLine in text.Split('\n'))
		{
			var line = rawLine.Trim().TrimEnd('\r');

			if (line.Length == 0 || line.StartsWith('#'))
				continue;

			var fields
				= line.Contains('\t') ? line.Split('\t')
				: line.Contains('|') ? line.Split('|')
				: line.Split(" - ");

			string Field(int i) => fields.Length > i ? fields[i].Trim() : "";

			var title = Field(0);
			if (title.Length == 0)
				continue;

			entries.Add(new CatalogueEntry(title, Field(1), Field(2)));
		}

		return entries;
	}

	/// <summary>
	/// Records books you own but have no file for, so the library shows everything rather than
	/// only what Libation could download.
	/// <para>
	/// This is the answer for stores whose audio cannot legitimately leave their app. The entry
	/// asserts ownership and nothing else: no path, no download, no audio. Entered by hand or
	/// from a pasted list, never read out of a retailer account.
	/// </para>
	/// </summary>
	public static async Task<int> AddCatalogueEntriesAsync(
		IReadOnlyCollection<CatalogueEntry> entries,
		CancellationToken cancellationToken = default)
	{
		if (entries.Count == 0)
			return 0;

		await LibraryCommands.WaitImportGateAsync(cancellationToken);
		try
		{
			// DoDbSizeChangeOperation returns EF's changed-row count. One book is a Books row, a
			// LibraryBooks row, a UserDefinedItem, a link row per contributor, and any contributor
			// that did not already exist -- so three books reported as nineteen. Callers asked how
			// many books were added.
			var qtyChanges = LibraryCommands.DoDbSizeChangeOperation(context =>
			{
				var contributors = new ContributorCache(context);
				var now = DateTime.UtcNow;

				foreach (var entry in entries)
				{
					var author = string.IsNullOrWhiteSpace(entry.Author) ? UnknownAuthor : entry.Author;
					var narrator = string.IsNullOrWhiteSpace(entry.Narrator) ? author : entry.Narrator;

					var book = Book.CreateNonAudible(
						BookSource.CatalogueOnly,
						entry.Title,
						subtitle: null,
						description: null,
						lengthInMinutes: 0,
						[contributors.Get(author)],
						[contributors.Get(narrator)],
						PlaceholderLocale);

					// Left NotLiberated on purpose. There is no file, so claiming otherwise would
					// be a lie the grid then has to explain. Nothing will try to download it:
					// Downloadable requires an Audible source.
					context.Books.Add(book);
					context.LibraryBooks.Add(new LibraryBook(book, now, LibraryBook.LocalAccount));
				}
			});

			return qtyChanges == 0 ? 0 : entries.Count;
		}
		finally
		{
			LibraryCommands.ReleaseImportGate();
		}
	}

	#endregion

	private static int Import(IReadOnlyCollection<LocalAudiobook> books)
	{
		// Paths are registered after the transaction commits: FilePathCache is a separate JSON
		// file, so registering first would leave orphaned entries if the save failed.
		var toRegister = new List<(string ProductId, string Path)>();

		var qtyChanges = LibraryCommands.DoDbSizeChangeOperation(context =>
		{
			var contributors = new ContributorCache(context);
			var now = DateTime.UtcNow;

			foreach (var found in books)
			{
				var author = string.IsNullOrWhiteSpace(found.Author) ? UnknownAuthor : found.Author;

				// The Audible importer does the same when a title has no narrators: a book must
				// have at least one of each, and the author is a better guess than a blank.
				var narrator = string.IsNullOrWhiteSpace(found.Narrator) ? author : found.Narrator;

				var book = Book.CreateNonAudible(
					BookSource.Local,
					found.Title,
					subtitle: null,
					description: null,
					found.LengthInMinutes,
					[contributors.Get(author)],
					[contributors.Get(narrator)],
					PlaceholderLocale);

				// Safe on a brand-new entity: UserDefinedItem suppresses its change event until
				// the row has an id, so this does not fire mid-insert.
				book.UserDefinedItem.BookStatus = LiberatedStatus.Liberated;

				context.Books.Add(book);
				context.LibraryBooks.Add(new LibraryBook(book, now, LibraryBook.LocalAccount));

				toRegister.Add((book.AudibleProductId, found.Path.Path));
			}
		});

		if (qtyChanges == 0)
			return 0;

		// Being Liberated and having a registered path are independent: the status makes the grid
		// show it as owned, the cache entry is what "where is this file" actually consults. A
		// local book has no filename containing its id, so the cache is its only path source.
		foreach (var (productId, path) in toRegister)
		{
			try
			{
				FilePathCache.Insert(productId, path);
			}
			catch (Exception ex)
			{
				Log.Logger.Error(ex, "Imported {ProductId} but could not register its path {Path}", productId, path);
			}
		}

		Log.Logger.Information("Imported {Count} local audiobooks", toRegister.Count);
		// Books, not the row count. See the note in AddCatalogueEntriesAsync.
		return toRegister.Count;
	}

	/// <summary>
	/// Every path already registered to a book, so a re-scan of the same folder imports nothing
	/// twice. FilePathCache is keyed by product id with no reverse lookup, so this walks the
	/// library once rather than querying per file.
	/// </summary>
	private static IReadOnlySet<string> RegisteredPaths()
	{
		var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		try
		{
			using var context = DbContexts.GetContext();

			foreach (var productId in context.LibraryBooks.Select(lb => lb.Book.AudibleProductId).ToArray())
				foreach (var (_, path) in FilePathCache.GetFiles(productId))
					paths.Add(path.Path);
		}
		catch (Exception ex)
		{
			// Worst case is offering to re-import something already present, which the user sees
			// in the results. Better than refusing to scan.
			Log.Logger.Warning(ex, "Could not build the set of known file paths; scanning without it");
		}

		return paths;
	}

	/// <summary>
	/// Creates contributors on demand and reuses them within one import.
	/// <para>
	/// Deduped by exact name, matching ContributorImporter — Libation treats the name as the
	/// identity for contributors without an Audible id, so "Neil Gaiman" and "neil gaiman" are
	/// two people. Consistent with the Audible path, which is what matters.
	/// </para>
	/// </summary>
	private sealed class ContributorCache(LibationContext context)
	{
		private readonly Dictionary<string, Contributor> cache = new(StringComparer.Ordinal);

		public Contributor Get(string name)
		{
			if (cache.TryGetValue(name, out var cached))
				return cached;

			var contributor
				= context.Contributors.FirstOrDefault(c => c.Name == name)
				?? context.Contributors.Add(new Contributor(name)).Entity;

			cache[name] = contributor;
			return contributor;
		}
	}
}
