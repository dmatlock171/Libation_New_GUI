using LibationFileManager;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace LibationAvalonia.Themes;

/// <summary>
/// User-written notes describing what each theme colour actually affects.
/// <para/>
/// Avalonia's palette names (BaseHigh, ChromeMediumLow, ...) say nothing about what
/// they control in this app, and several control nothing visible at all. Rather than
/// ship guessed labels, this lets the user record findings as they discover them.
/// <para/>
/// Stored as ColorDescriptions.json in the Libation files folder, so notes survive
/// upgrades and can be shared.
/// </summary>
public static class ThemeDescriptions
{
	public const string FileName = "ColorDescriptions.json";

	private static Dictionary<string, string>? descriptions;

	private static string FilePath
		=> Path.Combine(Configuration.Instance.LibationFiles.Location, FileName);

	private static Dictionary<string, string> Descriptions
	{
		get
		{
			if (descriptions is not null)
				return descriptions;

			descriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			try
			{
				if (File.Exists(FilePath) &&
					JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(FilePath))
						is Dictionary<string, string> loaded)
				{
					foreach (var pair in loaded)
						descriptions[pair.Key] = pair.Value;
				}
			}
			catch (Exception ex)
			{
				Serilog.Log.Logger.Error(ex, "Failed to read {FileName}", FileName);
			}

			SeedKnownDescriptions();
			return descriptions;
		}
	}

	/// <summary>
	/// Starting notes. Entries marked "?" are Fluent conventions rather than anything
	/// observed in Libation, and several palette colours may do nothing visible here at
	/// all - correct them as you find out. User edits always win; these only fill gaps.
	/// </summary>
	private static void SeedKnownDescriptions()
	{
		if (descriptions is null)
			return;

		// --- Libation's own brushes: confirmed from their usage in the source ---
		AddIfMissing("GridLines", "Grid row/column separators, grid border, column header dividers");
		AddIfMissing("WindowOutline", "Outline just inside every window edge, and the OS window border");
		AddIfMissing("WindowCaption", "Title bar background (Windows 11 only)");
		AddIfMissing("WindowCaptionText", "Title bar text (Windows 11 only)");
		AddIfMissing("IconFill", "Menu and toolbar icons, queue collapse arrow");
		AddIfMissing("StoplightRed", "Status column traffic light: not downloaded");
		AddIfMissing("StoplightYellow", "Status column traffic light: partially downloaded");
		AddIfMissing("StoplightGreen", "Status column traffic light: downloaded");
		AddIfMissing("CancelRed", "Error icon in the status column");
		AddIfMissing("HyperlinkNew", "Unvisited links");
		AddIfMissing("HyperlinkVisited", "Visited links");
		AddIfMissing("SeriesEntryGridBackgroundBrush", "Background of series parent rows in the grid");
		AddIfMissing("AlternatingRowBackgroundBrush", "Shading on every other row in the grid and download queue");
		AddIfMissing("ProcessQueueBookFailedBrush", "Process queue: failed item background");
		AddIfMissing("ProcessQueueBookCompletedBrush", "Process queue: completed item background");
		AddIfMissing("ProcessQueueBookCancelledBrush", "Process queue: cancelled item background");

		// --- Observed in Libation ---
		AddIfMissing("BaseHigh", "Primary text colour throughout the app");
		AddIfMissing("BaseLow", "Line beneath the menu bar");
		AddIfMissing("BaseMediumLow", "Window chrome borders, queue divider");

		// --- Fluent conventions, NOT verified in Libation ---
		AddIfMissing("Accent", "? Selection highlight, focus rings");
		AddIfMissing("RegionColor", "? Window background");
		AddIfMissing("ErrorText", "? Validation and error text");
		AddIfMissing("BaseMedium", "? Secondary/muted text");
		AddIfMissing("BaseMediumHigh", "? Slightly muted text");
		AddIfMissing("AltHigh", "? Control backgrounds, opposite of Base");
		AddIfMissing("AltLow", "? Subtle control backgrounds");
		AddIfMissing("AltMedium", "? Mid-tone control backgrounds");
		AddIfMissing("AltMediumHigh", "? Mid-tone control backgrounds");
		AddIfMissing("AltMediumLow", "? Mid-tone control backgrounds");
		AddIfMissing("ChromeHigh", "? Control borders");
		AddIfMissing("ChromeLow", "? Control fills");
		AddIfMissing("ChromeMedium", "? Panel and toolbar backgrounds");
		AddIfMissing("ChromeMediumLow", "? Panel backgrounds");
		AddIfMissing("ChromeAltLow", "? Button text (see App.axaml Button style)");
		AddIfMissing("ChromeGray", "? Neutral chrome elements");
		AddIfMissing("ChromeWhite", "? Highest-contrast chrome");
		AddIfMissing("ChromeDisabledHigh", "? Disabled control background");
		AddIfMissing("ChromeDisabledLow", "? Disabled control text; read-only text boxes");
		AddIfMissing("ChromeBlackHigh", "? Rarely used in Libation");
		AddIfMissing("ChromeBlackLow", "? Rarely used in Libation");
		AddIfMissing("ChromeBlackMedium", "? Rarely used in Libation");
		AddIfMissing("ChromeBlackMediumLow", "? Rarely used in Libation");
		AddIfMissing("ListLow", "? List item pressed background");
		AddIfMissing("ListMedium", "? List item hover background");

		void AddIfMissing(string key, string value)
		{
			if (!descriptions.ContainsKey(key))
				descriptions[key] = value;
		}
	}

	public static string Get(string themeItemName)
		=> Descriptions.TryGetValue(themeItemName, out var description) ? description : string.Empty;

	public static void Set(string themeItemName, string? description)
	{
		Descriptions[themeItemName] = description ?? string.Empty;
		Save();
	}

	private static void Save()
	{
		try
		{
			File.WriteAllText(FilePath, JsonConvert.SerializeObject(Descriptions, Formatting.Indented));
		}
		catch (Exception ex)
		{
			Serilog.Log.Logger.Error(ex, "Failed to save {FileName}", FileName);
		}
	}
}
