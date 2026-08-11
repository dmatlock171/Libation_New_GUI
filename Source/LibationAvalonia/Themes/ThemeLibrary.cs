using LibationFileManager;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LibationAvalonia.Themes;

/// <summary>
/// Named colour schemes stored as individual files in a "Themes" folder inside the
/// Libation files directory.
/// <para/>
/// Applying a scheme copies it over ChardonnayTheme.json and then re-runs the normal
/// theme load path, rather than re-implementing theme application. That keeps this
/// class to file management only.
/// </summary>
public static class ThemeLibrary
{
	public const string ThemesFolderName = "Themes";
	private const string Extension = ".json";

	public static string ThemesFolder
		=> Path.Combine(Configuration.Instance.LibationFiles.Location, ThemesFolderName);

	public static string EnsureThemesFolder()
	{
		var folder = ThemesFolder;
		Directory.CreateDirectory(folder);
		return folder;
	}

	/// <summary> Scheme names (file names without extension), alphabetical. </summary>
	public static IReadOnlyList<string> ListThemeNames()
	{
		try
		{
			if (!Directory.Exists(ThemesFolder))
				return [];

			return Directory
				.EnumerateFiles(ThemesFolder, "*" + Extension)
				.Select(Path.GetFileNameWithoutExtension)
				.Where(n => !string.IsNullOrWhiteSpace(n))
				.Select(n => n!)
				.OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
				.ToList();
		}
		catch (Exception ex)
		{
			Serilog.Log.Logger.Error(ex, "Failed to list saved themes");
			return [];
		}
	}

	public static string PathFor(string themeName)
		=> Path.Combine(ThemesFolder, themeName + Extension);

	/// <summary>
	/// Saves the currently applied colours under <paramref name="themeName"/>.
	/// Overwrites silently: the caller is responsible for confirming.
	/// </summary>
	public static bool SaveCurrentAs(string themeName)
	{
		try
		{
			EnsureThemesFolder();

			// ChardonnayTheme.json always reflects the live theme, so copying it is
			// equivalent to serialising the current palette and avoids duplicating
			// the persister's converter setup. Create() writes the file from the live
			// theme when it doesn't yet exist.
			if (!File.Exists(ChardonnayThemePersister.jsonPath))
				ChardonnayThemePersister.Create()?.Dispose();

			if (!File.Exists(ChardonnayThemePersister.jsonPath))
				return false;

			File.Copy(ChardonnayThemePersister.jsonPath, PathFor(themeName), overwrite: true);
			return true;
		}
		catch (Exception ex)
		{
			Serilog.Log.Logger.Error(ex, "Failed to save theme {ThemeName}", themeName);
			return false;
		}
	}

	/// <summary>
	/// Makes the named scheme current. The caller must trigger theme re-application
	/// afterwards, and should do so off the current event (see MainVM.Theme).
	/// </summary>
	public static bool MakeCurrent(string themeName)
	{
		try
		{
			var source = PathFor(themeName);
			if (!File.Exists(source))
				return false;

			File.Copy(source, ChardonnayThemePersister.jsonPath, overwrite: true);
			return true;
		}
		catch (Exception ex)
		{
			Serilog.Log.Logger.Error(ex, "Failed to apply theme {ThemeName}", themeName);
			return false;
		}
	}

	public static bool Delete(string themeName)
	{
		try
		{
			var path = PathFor(themeName);
			if (!File.Exists(path))
				return false;

			File.Delete(path);
			return true;
		}
		catch (Exception ex)
		{
			Serilog.Log.Logger.Error(ex, "Failed to delete theme {ThemeName}", themeName);
			return false;
		}
	}

	/// <summary> Strips characters that aren't valid in a file name. </summary>
	public static string SanitizeName(string name)
	{
		var invalid = Path.GetInvalidFileNameChars();
		var cleaned = new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
		return string.IsNullOrWhiteSpace(cleaned) ? "Untitled" : cleaned;
	}
}
