using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;

namespace LibationFileManager;

/// <summary>What is wrong with the Serilog File sink's destination.</summary>
public enum LogPathProblemKind
{
	/// <summary>Settings.json has no File sink at all, so nothing is written to disk.</summary>
	NoFileSink,
	/// <summary>The configured folder does not exist. This is the classic case: a path on a
	/// drive letter that has since gone away.</summary>
	DirectoryMissing,
	/// <summary>The folder exists but cannot be written to — permissions, read-only media,
	/// or a network share that is no longer reachable.</summary>
	DirectoryNotWritable,
	/// <summary>The path looks fine but the sink never opened a file this session.</summary>
	NoFileOpened
}

/// <param name="Kind">Which check failed.</param>
/// <param name="ConfiguredPath">The path from Settings.json, if there was one.</param>
/// <param name="Directory">The folder that path lives in, if it could be determined.</param>
/// <param name="Message">One-line description, safe to show to a user or write to a log.</param>
public sealed record LogPathProblem(LogPathProblemKind Kind, string? ConfiguredPath, string? Directory, string Message);

/// <summary>
/// Answers "is Libation actually able to write its log?" — a question
/// <see cref="Configuration.ValidateSerilogConfiguration"/> does not ask.
/// <para>
/// That method validates the <em>shape</em> of the Serilog section: a WriteTo array exists,
/// each sink has a Name, MinimumLevel parses. A structurally perfect config pointing at
/// <c>F:\_Libations\Log.log</c> on a drive that no longer exists passes every one of those
/// checks. Serilog's file sink does not throw in that situation either; it simply never opens
/// a file. The result is logging that is silently dead, discovered only when someone goes
/// looking for a log that was never written.
/// </para>
/// <para>
/// This is deliberately a warning rather than an exception. A missing log destination should
/// never stop Libation from running — it should just stop being invisible.
/// </para>
/// </summary>
public static class LogPathValidator
{
	/// <summary>
	/// Checks the configured File sink destination. Returns <see langword="null"/> when logging
	/// is fine.
	/// </summary>
	/// <param name="checkFileOpened">
	/// Whether to also report a sink that never opened a file. Only meaningful once logging has
	/// been configured, so the Diagnostics dialog passes <see langword="true"/> and startup
	/// callers may too, but anything running before <see cref="Configuration.ConfigureLogging"/>
	/// should pass <see langword="false"/> to avoid a false positive.
	/// </param>
	public static LogPathProblem? Detect(bool checkFileOpened = true)
	{
		var configuredPath = GetConfiguredLogPath();

		if (string.IsNullOrWhiteSpace(configuredPath))
			return new LogPathProblem(
				LogPathProblemKind.NoFileSink,
				null,
				null,
				"Settings.json has no Serilog File sink, so no log file is being written.");

		var directory = Path.GetDirectoryName(configuredPath);

		if (string.IsNullOrWhiteSpace(directory) || !System.IO.Directory.Exists(directory))
			return new LogPathProblem(
				LogPathProblemKind.DirectoryMissing,
				configuredPath,
				directory,
				$"The log folder '{directory}' does not exist, so nothing is being logged.");

		if (!DirectoryIsWritable(directory))
			return new LogPathProblem(
				LogPathProblemKind.DirectoryNotWritable,
				configuredPath,
				directory,
				$"The log folder '{directory}' exists but cannot be written to, so nothing is being logged.");

		// Everything about the destination looks right, so if no file was opened the failure is
		// somewhere else in the sink. Still worth saying out loud.
		if (checkFileOpened && string.IsNullOrWhiteSpace(LogFileFilter.LogFilePath))
			return new LogPathProblem(
				LogPathProblemKind.NoFileOpened,
				configuredPath,
				directory,
				"No log file has been opened this session, even though the configured path looks valid.");

		return null;
	}

	/// <summary>
	/// The File sink's <c>path</c> argument from the Serilog section of Settings.json. This is
	/// the <em>requested</em> path, which is not necessarily the one Serilog managed to open —
	/// compare with <see cref="LogFileFilter.LogFilePath"/> for that.
	/// </summary>
	public static string? GetConfiguredLogPath()
	{
		try
		{
			if (Configuration.Instance.GetObject("Serilog") is not JObject serilog)
				return null;

			return serilog
				.SelectTokens("$.WriteTo[?(@.Name == 'File')].Args.path", false)
				.Select(t => t.Value<string>())
				.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));
		}
		catch
		{
			// A malformed Serilog section is ValidateSerilogConfiguration's problem, not ours.
			return null;
		}
	}

	/// <summary>
	/// Probes by writing a temp file rather than inferring from attributes. Permissions,
	/// read-only media and disconnected network shares all pass <see cref="Directory.Exists"/>
	/// and then fail on write.
	/// </summary>
	public static bool DirectoryIsWritable(string directory)
	{
		var probe = Path.Combine(directory, $".libation-write-test-{Guid.NewGuid():N}.tmp");
		try
		{
			using (File.Create(probe)) { }
			File.Delete(probe);
			return true;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// The message shown to the user at startup. Longer and more actionable than
	/// <see cref="LogPathProblem.Message"/>, which is written to the log.
	/// </summary>
	public static string ToUserMessage(LogPathProblem problem)
	{
		var settingsPath = Configuration.Instance.LibationFiles.SettingsFilePath;

		var explanation = problem.Kind switch
		{
			LogPathProblemKind.NoFileSink =>
				"Libation is not writing a log file because Settings.json has no Serilog File sink.",

			LogPathProblemKind.DirectoryMissing =>
				$"Libation cannot write its log file because this folder does not exist:\n\n{problem.Directory}",

			LogPathProblemKind.DirectoryNotWritable =>
				$"Libation cannot write its log file because this folder is not writable:\n\n{problem.Directory}",

			_ =>
				$"Libation has not opened a log file this session, though the configured path looks valid:\n\n{problem.ConfiguredPath}"
		};

		return $"""
			{explanation}

			Libation will keep running, but errors will not be recorded. If something goes
			wrong, there will be no log to look at.

			To fix this, edit the Serilog path in:
			{settingsPath}

			Settings > Diagnostics shows the current paths and whether they are working.
			""";
	}
}
