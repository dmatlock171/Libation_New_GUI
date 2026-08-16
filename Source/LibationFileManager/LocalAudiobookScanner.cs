using Dinah.Core;
using FileManager;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace LibationFileManager;

/// <summary>An audiobook file found on disk that Libation did not download.</summary>
/// <param name="Path">Where the file is. Never moved, renamed or rewritten by import.</param>
/// <param name="Title">From tags, falling back to the file name.</param>
/// <param name="Author">Empty when the file carries no artist tag.</param>
/// <param name="Narrator">Empty when unknown; the importer then reuses the author, as the Audible importer does.</param>
/// <param name="LengthInMinutes">0 when it could not be determined. The grid renders 0 as blank.</param>
public record LocalAudiobook(
	LongPath Path,
	string Title,
	string Author,
	string Narrator,
	int LengthInMinutes);

/// <summary>
/// Finds audiobooks in a folder and reads what the files say about themselves.
/// <para>
/// Deliberately close to <see cref="AudioFileStorage.FindAudiobooksAsync"/>, which walks a folder
/// the same way but only looks for an Audible ASIN. That one answers "is this a book I already
/// know about"; this one answers "what is this book" for files Libation has never seen.
/// </para>
/// <para>
/// Reads only. Import never moves, renames or rewrites the source files — users organise these
/// deliberately and touching them would be a serious breach of expectations.
/// </para>
/// </summary>
public static class LocalAudiobookScanner
{
	private static EnumerationOptions EnumerationOptions { get; } = new()
	{
		RecurseSubdirectories = true,
		IgnoreInaccessible = true,
		AttributesToSkip = FileAttributes.Hidden,
	};

	/// <summary>
	/// Yields every importable audiobook under <paramref name="searchDirectory"/>.
	/// <para>
	/// Files carrying an Audible ASIN are skipped: those belong to "Locate Audiobooks", which
	/// matches them to a book already in the library. Importing one here would create a second,
	/// unrelated row for a book Libation already knows about.
	/// </para>
	/// </summary>
	/// <param name="skipPaths">
	/// Paths already registered to a book. Supplied by the caller rather than looked up here,
	/// because <see cref="FilePathCache"/> is keyed by product id and offers no path-to-book
	/// lookup — and because the file layer has no business querying the library. Passing the set
	/// is what makes re-running a scan over the same folder safe.
	/// </param>
	public static async IAsyncEnumerable<LocalAudiobook> ScanAsync(
		LongPath searchDirectory,
		IReadOnlySet<string>? skipPaths = null,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		ArgumentValidator.EnsureNotNull(searchDirectory, nameof(searchDirectory));

		foreach (LongPath path in Directory.EnumerateFiles(searchDirectory, "*.*", EnumerationOptions))
		{
			if (cancellationToken.IsCancellationRequested)
				yield break;

			if (!IsAudiobook(path) || skipPaths?.Contains(path.Path) is true)
				continue;

			LocalAudiobook? found = null;

			try
			{
				found = await Task.Run(() => Read(path), cancellationToken);
			}
			catch (Exception ex)
			{
				// One unreadable file must not abandon the folder.
				Serilog.Log.Logger.Warning(ex, "Could not read audiobook metadata from {File}", path.Path);
			}

			if (found is not null)
				yield return found;
		}
	}

	private static bool IsAudiobook(string path)
		=> Path.GetExtension(path).ToLowerInvariant() is ".m4b" or ".m4a" or ".mp3";

	private static LocalAudiobook? Read(LongPath path)
		=> Path.GetExtension(path).ToLowerInvariant() is ".mp3"
		? ReadMp3(path)
		: ReadMp4(path);

	private static LocalAudiobook? ReadMp4(LongPath path)
	{
		using var file = new Mpeg4Lib.Mpeg4File(File.OpenRead(path));
		var tags = file.MetadataItems;

		// Belongs to Locate Audiobooks, not here.
		if (!string.IsNullOrWhiteSpace(tags?.Asin))
			return null;

		return new LocalAudiobook(
			path,
			Clean(tags?.Title) ?? FileNameTitle(path),
			Clean(tags?.FirstAuthor) ?? Clean(tags?.Artist) ?? "",
			Clean(tags?.Narrator) ?? "",
			(int)file.Duration.TotalMinutes);
	}

	private static LocalAudiobook? ReadMp3(LongPath path)
	{
		using var stream = File.OpenRead(path);
		var id3 = Mpeg4Lib.ID3.Id3Tag.Create(stream);

		var userText = id3?.Children.OfType<Mpeg4Lib.ID3.TXXXFrame>().ToArray() ?? [];

		if (!string.IsNullOrWhiteSpace(userText.FirstOrDefault(f => f.FieldName == "AUDIBLE_ASIN")?.FieldValue))
			return null;

		string? Text(string id) => id3?.Children
			.OfType<Mpeg4Lib.ID3.TEXTFrame>()
			.FirstOrDefault(f => f.Header?.Identifier == id)
			?.Text;

		return new LocalAudiobook(
			path,
			Clean(Text("TIT2")) ?? FileNameTitle(path),
			Clean(Text("TPE1")) ?? "",
			// TCOM is composer, which is where narrator conventionally lands in audiobook mp3s.
			Clean(Text("TCOM")) ?? Clean(userText.FirstOrDefault(f => f.FieldName == "NARRATOR")?.FieldValue) ?? "",
			// Mpeg4Lib exposes no duration for mp3. 0 renders as blank in the grid, which is
			// honest; guessing from file size would not be.
			0);
	}

	private static string FileNameTitle(LongPath path)
		=> Path.GetFileNameWithoutExtension(path.Path) is { Length: > 0 } name ? name : "Unknown title";

	private static string? Clean(string? value)
		=> string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
