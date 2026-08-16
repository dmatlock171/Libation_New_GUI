using ApplicationServices;
using DataLayer;
using Dinah.Core.Logging;
using LibationFileManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace AppScaffolding;

/// <summary>How a <see cref="DiagnosticItem"/> should be presented. Anything above
/// <see cref="Ok"/> is something the user probably needs to act on.</summary>
public enum DiagnosticSeverity
{
	Ok,
	Warning,
	Error
}

/// <summary>One labelled fact about the running install.</summary>
/// <param name="Label">Short name shown in the left column.</param>
/// <param name="Value">The value itself. Never null; use "(none)" and friends instead.</param>
/// <param name="Severity">Drives highlighting in the UI.</param>
/// <param name="Detail">Optional explanation of <em>why</em> this is a problem, shown under the value.</param>
public record DiagnosticItem(string Label, string Value, DiagnosticSeverity Severity = DiagnosticSeverity.Ok, string? Detail = null);

/// <summary>A group of related <see cref="DiagnosticItem"/>s.</summary>
public record DiagnosticSection(string Name, IReadOnlyList<DiagnosticItem> Items)
{
	/// <summary>The worst severity in the section, for collapsing/badging the header.</summary>
	public DiagnosticSeverity Severity => Items.Count == 0 ? DiagnosticSeverity.Ok : Items.Max(i => i.Severity);
}

/// <summary>
/// Collects the "where is everything and is it actually working" information that otherwise
/// only ever reaches the log file — which is unhelpful precisely when logging is what's broken.
/// <para>
/// This exists because two separate silent failures each cost an evening to diagnose: a Serilog
/// path pointing at a drive letter that no longer existed (logging was dead for weeks with no
/// indication anywhere in the UI), and a <c>LIBATION_FILES_DIR</c> that is silently ignored when
/// the directory does not exist, falling back to appsettings.json and presenting an empty library.
/// Both are detected here and reported as problems rather than left to be inferred.
/// </para>
/// </summary>
public static class LibationDiagnostics
{
	private const string NotSet = "(not set)";
	private const string Unknown = "(unknown)";

	/// <summary>Gathers every section. Safe to call from the UI thread; the only potentially slow
	/// part is the library count, which is guarded and degrades to an error row.</summary>
	public static IReadOnlyList<DiagnosticSection> Collect()
		=> new[]
		{
			GetApplicationSection(),
			GetPathsSection(),
			GetDatabaseSection(),
			GetLoggingSection(),
			GetLibrarySection()
		};

	#region Sections

	private static DiagnosticSection GetApplicationSection()
	{
		var items = new List<DiagnosticItem>
		{
			new("Version", $"Libation {LibationScaffolding.Variety} v{LibationScaffolding.BuildVersion}"),
			new("Release", LibationScaffolding.ReleaseIdentifier.ToString()),
			new("Build", GetBuildMode()),
			new("OS", $"{Configuration.OS} — {Environment.OSVersion}"),
			new("Runtime", Environment.Version.ToString()),
			new("Process", Environment.ProcessPath ?? Unknown)
		};

		// A null interop type means the OS-specific shim failed to load. Everything still runs,
		// but file associations and folder icons quietly do nothing, so it is worth surfacing.
		var interop = InteropFactory.InteropFunctionsType;
		items.Add(interop is null
			? new DiagnosticItem("OS interop", "failed to load", DiagnosticSeverity.Warning,
				"OS-specific features such as folder icons and file associations will not work.")
			: new DiagnosticItem("OS interop", interop.Name));

		return new DiagnosticSection("Application", items);
	}

	private static DiagnosticSection GetPathsSection()
	{
		var items = new List<DiagnosticItem>();
		var libationFiles = Configuration.Instance.LibationFiles;

		// The env var is only honoured when the directory exists (see LibationFiles ctor).
		// When it is set but wrong, Libation falls back to appsettings.json without a word,
		// which looks exactly like "my library disappeared".
		var envVar = Environment.GetEnvironmentVariable(LibationFiles.LIBATION_FILES_DIR);
		if (string.IsNullOrWhiteSpace(envVar))
			items.Add(new DiagnosticItem(LibationFiles.LIBATION_FILES_DIR, NotSet));
		else if (Directory.Exists(envVar))
			items.Add(new DiagnosticItem(LibationFiles.LIBATION_FILES_DIR, envVar, DiagnosticSeverity.Ok, "In use — this overrides appsettings.json."));
		else
			items.Add(new DiagnosticItem(LibationFiles.LIBATION_FILES_DIR, envVar, DiagnosticSeverity.Error,
				"This directory does not exist, so it is being IGNORED. Libation fell back to appsettings.json, " +
				"which is why the library below may not be the one you expected."));

		var filesLocation = libationFiles.Location.Path;
		items.Add(Directory.Exists(filesLocation)
			? new DiagnosticItem("Libation files", filesLocation)
			: new DiagnosticItem("Libation files", filesLocation, DiagnosticSeverity.Error, "Directory does not exist."));

		// AppsettingsJsonFile is null exactly when the env var won. Match the wording already
		// used by LibationScaffolding and DbContexts so log and dialog agree.
		items.Add(new DiagnosticItem("appsettings.json",
			libationFiles.AppsettingsJsonFile ?? "(null, LIBATION_FILES_DIR may be set)"));

		items.Add(File.Exists(libationFiles.SettingsFilePath)
			? new DiagnosticItem("Settings.json", libationFiles.SettingsFilePath,
				libationFiles.SettingsAreValid ? DiagnosticSeverity.Ok : DiagnosticSeverity.Error,
				libationFiles.SettingsAreValid ? null : "File exists but is missing required values (Books directory).")
			: new DiagnosticItem("Settings.json", libationFiles.SettingsFilePath, DiagnosticSeverity.Error, "File does not exist."));

		items.Add(DescribeDirectory("Books", AudibleFileStorage.BooksDirectory?.Path));
		items.Add(DescribeDirectory("In progress", Configuration.Instance.InProgress));

		return new DiagnosticSection("Paths", items);
	}

	private static DiagnosticSection GetDatabaseSection()
	{
		var items = new List<DiagnosticItem>();

		var postgres = Configuration.Instance.PostgresqlConnectionString;
		if (!string.IsNullOrWhiteSpace(postgres))
		{
			// Deliberately not printed: the connection string carries credentials and this
			// dialog has a "copy to clipboard" button aimed at bug reports.
			items.Add(new DiagnosticItem("Provider", "PostgreSQL"));
			items.Add(new DiagnosticItem("Connection", "(configured — hidden, contains credentials)"));
			return new DiagnosticSection("Database", items);
		}

		items.Add(new DiagnosticItem("Provider", "SQLite"));

		var dbPath = SqliteStorage.DatabasePath;
		if (File.Exists(dbPath))
		{
			items.Add(new DiagnosticItem("File", dbPath));
			items.Add(new DiagnosticItem("Size", FormatBytes(new FileInfo(dbPath).Length)));
			items.Add(new DiagnosticItem("Modified", File.GetLastWriteTime(dbPath).ToString("g")));
		}
		else
		{
			items.Add(new DiagnosticItem("File", dbPath, DiagnosticSeverity.Error,
				"Database file does not exist. The library will be empty until a scan is run."));
		}

		return new DiagnosticSection("Database", items);
	}

	private static DiagnosticSection GetLoggingSection()
	{
		var items = new List<DiagnosticItem>();

		items.Add(Configuration.Instance.SerilogInitialized
			? new DiagnosticItem("Serilog", "initialized")
			: new DiagnosticItem("Serilog", "NOT initialized", DiagnosticSeverity.Error,
				"Nothing is being logged. Errors elsewhere in the app will leave no trace."));

		items.Add(new DiagnosticItem("Minimum level", Configuration.Instance.LogLevel.ToString()));

		// Two different paths, and the difference is the whole point of this section:
		// "configured" is what Settings.json asks for, "active" is what the file sink actually
		// opened. A configured path on a missing drive produces no active path and no error.
		//
		// The verdict comes from LogPathValidator, the same code that raises the startup
		// warning, so the dialog and the warning can never disagree about whether logging works.
		var problem = LogPathValidator.Detect();
		var configuredPath = LogPathValidator.GetConfiguredLogPath();

		if (configuredPath is null)
		{
			items.Add(new DiagnosticItem("Configured path", "(no File sink configured)", DiagnosticSeverity.Error,
				problem?.Message ?? "Settings.json has no Serilog File sink, so nothing is written to disk."));
		}
		// NoFileOpened is reported by the "Active log file" row below, so the path itself is
		// only flagged when the path is what's wrong.
		else if (problem is null || problem.Kind is LogPathProblemKind.NoFileOpened)
		{
			items.Add(new DiagnosticItem("Configured path", configuredPath));
		}
		else
		{
			items.Add(new DiagnosticItem("Configured path", configuredPath, DiagnosticSeverity.Error,
				problem.Message + " Fix the Serilog path in Settings.json."));
		}

		var activePath = LogFileFilter.LogFilePath;
		items.Add(string.IsNullOrWhiteSpace(activePath)
			? new DiagnosticItem("Active log file", "(none opened)", DiagnosticSeverity.Error,
				"No log file has been opened this session, so nothing is being logged.")
			: new DiagnosticItem("Active log file", activePath, DiagnosticSeverity.Ok,
				File.Exists(activePath) ? $"{FormatBytes(new FileInfo(activePath).Length)}, last written {File.GetLastWriteTime(activePath):g}" : null));

		items.Add(new DiagnosticItem("Levels enabled", string.Join(", ", EnabledLevels())));

		return new DiagnosticSection("Logging", items);
	}

	private static DiagnosticSection GetLibrarySection()
	{
		var items = new List<DiagnosticItem>();

		try
		{
			using var context = DbContexts.GetContext();
			var (notInTrash, inTrash) = context.GetLibraryBookCountsByTrashFlag();

			items.Add(new DiagnosticItem("Books in library", notInTrash.ToString("N0")));
			items.Add(new DiagnosticItem("Books in trash", inTrash.ToString("N0")));
			items.Add(new DiagnosticItem("Book records", context.GetBookCount().ToString("N0")));
		}
		catch (Exception ex)
		{
			items.Add(new DiagnosticItem("Library", "could not be read", DiagnosticSeverity.Error, ex.Message));
		}

		return new DiagnosticSection("Library", items);
	}

	#endregion

	#region Plain text export

	/// <summary>
	/// Renders the sections as plain text suitable for pasting into a GitHub issue.
	/// Problems are marked so they survive the loss of colour.
	/// </summary>
	public static string ToPlainText(IReadOnlyList<DiagnosticSection>? sections = null)
	{
		sections ??= Collect();

		var sb = new StringBuilder();
		sb.AppendLine("Libation diagnostics");
		sb.AppendLine($"Generated {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

		foreach (var section in sections)
		{
			sb.AppendLine();
			sb.AppendLine($"## {section.Name}");

			var labelWidth = section.Items.Count == 0 ? 0 : section.Items.Max(i => i.Label.Length);

			foreach (var item in section.Items)
			{
				var marker = item.Severity switch
				{
					DiagnosticSeverity.Error => "[!] ",
					DiagnosticSeverity.Warning => "[?] ",
					_ => "    "
				};

				sb.AppendLine($"{marker}{item.Label.PadRight(labelWidth)} : {item.Value}");

				if (!string.IsNullOrWhiteSpace(item.Detail))
					sb.AppendLine($"    {new string(' ', labelWidth)}   -> {item.Detail}");
			}
		}

		return sb.ToString();
	}

	#endregion

	#region Helpers

	private static string GetBuildMode()
	{
#if DEBUG
		var mode = "Debug";
#else
		var mode = "Release";
#endif
		return Debugger.IsAttached ? mode + " (debugger attached)" : mode;
	}

	private static DiagnosticItem DescribeDirectory(string label, string? path)
		=> string.IsNullOrWhiteSpace(path)
			? new DiagnosticItem(label, NotSet, DiagnosticSeverity.Warning, "No directory is configured.")
			: Directory.Exists(path)
				? new DiagnosticItem(label, path)
				: new DiagnosticItem(label, path, DiagnosticSeverity.Warning, "Directory does not exist.");

	private static IEnumerable<string> EnabledLevels()
	{
		if (Log.Logger.IsVerboseEnabled()) yield return "Verbose";
		if (Log.Logger.IsDebugEnabled()) yield return "Debug";
		if (Log.Logger.IsInformationEnabled()) yield return "Information";
		if (Log.Logger.IsWarningEnabled()) yield return "Warning";
		if (Log.Logger.IsErrorEnabled()) yield return "Error";
		if (Log.Logger.IsFatalEnabled()) yield return "Fatal";
	}

	private static string FormatBytes(long bytes)
	{
		string[] units = { "B", "KB", "MB", "GB", "TB" };
		double value = bytes;
		var unit = 0;

		while (value >= 1024 && unit < units.Length - 1)
		{
			value /= 1024;
			unit++;
		}

		return unit == 0 ? $"{bytes:N0} B" : $"{value:N1} {units[unit]}";
	}

	#endregion
}
