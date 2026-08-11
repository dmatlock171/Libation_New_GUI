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
	/// Only entries confirmed by observation. Deliberately sparse: a wrong label is
	/// worse than none. User edits win, so these never overwrite existing notes.
	/// </summary>
	private static void SeedKnownDescriptions()
	{
		if (descriptions is null)
			return;

		AddIfMissing("BaseMediumLow", "Grid lines, grid border, queue divider");
		AddIfMissing("BaseLow", "Line beneath the menu bar");

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
