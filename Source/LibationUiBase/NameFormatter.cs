using System;
using System.Collections.Generic;
using System.Linq;

namespace LibationUiBase;

/// <summary>
/// Converts display names ("Ursula K. Le Guin") into surname-first form
/// ("Le Guin, Ursula K.") for sorting and display.
/// <para/>
/// Audible supplies a single flat name string per contributor, so this is
/// necessarily heuristic. Known-awkward names can be corrected exactly via
/// <see cref="Overrides"/> rather than by extending the heuristics.
/// </summary>
public static class NameFormatter
{
	/// <summary> Separator used between contributors in surname-first output. </summary>
	/// <remarks>
	/// Deliberately not a comma: surname-first names contain commas themselves,
	/// so "Clarke, Susanna, Miller, Madeline" would be unreadable.
	/// </remarks>
	public const string Separator = "; ";

	/// <summary>
	/// Exact full-name replacements, checked before any parsing.
	/// Populated from a user-editable file so users can fix their own edge cases.
	/// Keys are compared case-insensitively.
	/// </summary>
	public static Dictionary<string, string> Overrides { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// Names that aren't people. Audible lists imprints, series brands and
	/// production companies in the author field; those shouldn't be reordered
	/// as "Courses, The Great" or sorted under T.
	/// <para/>
	/// Seeded with common cases and extended by the user's customization file.
	/// </summary>
	public static HashSet<string> NonPersonNames { get; } = new(StringComparer.OrdinalIgnoreCase)
	{
		"The Great Courses",
		"Great Courses",
		"Audible Originals",
		"Audible Studios",
		"Audible Original",
		"full cast",
		"a full cast",
		"various authors",
		"various",
		"anonymous",
		"uncredited",
	};

	/// <summary> File in the Libation files folder holding user customizations. </summary>
	public const string CustomizationsFileName = "NameCustomizations.json";

	private static bool customizationsLoaded;

	/// <summary>
	/// Loads user customizations from <see cref="CustomizationsFileName"/> if present.
	/// Expected shape:
	/// <code>
	/// {
	///   "nonPersonNames": [ "Some Imprint" ],
	///   "overrides": { "Ursula K. Le Guin": "Le Guin, Ursula K." }
	/// }
	/// </code>
	/// Failures are swallowed: a malformed file must not stop the grid from loading.
	/// </summary>
	public static void LoadCustomizations()
	{
		if (customizationsLoaded)
			return;

		customizationsLoaded = true;

		try
		{
			var path = System.IO.Path.Combine(
				LibationFileManager.Configuration.Instance.LibationFiles.Location,
				CustomizationsFileName);

			if (!System.IO.File.Exists(path))
				return;

			var json = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(path));

			if (json["nonPersonNames"] is Newtonsoft.Json.Linq.JArray names)
			{
				foreach (var token in names)
				{
					var name = token.ToString();
					if (!string.IsNullOrWhiteSpace(name))
						NonPersonNames.Add(name.Trim());
				}
			}

			if (json["overrides"] is Newtonsoft.Json.Linq.JObject overrides)
			{
				foreach (var pair in overrides)
				{
					var replacement = pair.Value?.ToString();
					if (!string.IsNullOrWhiteSpace(replacement))
						Overrides[pair.Key] = replacement;
				}
			}
		}
		catch
		{
			// Malformed or unreadable customization file: fall back to built-in defaults.
		}
	}

	/// <summary> Generational and honorific suffixes that trail the surname. </summary>
	private static readonly HashSet<string> Suffixes = new(StringComparer.OrdinalIgnoreCase)
	{
		"jr", "jr.", "sr", "sr.",
		"i", "ii", "iii", "iv", "v", "vi",
		"phd", "ph.d", "ph.d.", "md", "m.d.", "dds", "esq", "esq.",
		"cpa", "dvm", "rn", "usa", "usn", "ret", "ret.",
	};

	/// <summary> Nobiliary particles and prefixes that belong to the surname. </summary>
	private static readonly HashSet<string> Particles = new(StringComparer.OrdinalIgnoreCase)
	{
		"van", "von", "de", "del", "della", "der", "den", "des", "du",
		"da", "di", "la", "le", "lo", "ter", "ten", "op",
		"bin", "ibn", "al", "el", "abu",
		"st", "st.", "saint",
	};

	/// <summary> Tokens that mark a name as an organisation rather than a person. </summary>
	private static readonly string[] OrganizationMarkers =
	{
		"inc", "inc.", "llc", "ltd", "ltd.", "corp", "corp.", "company",
		"press", "publishing", "publishers", "media", "productions",
		"studios", "audio", "books", "network", "foundation", "institute",
		"university", "associates", "partners", "group", "team", "editors",
	};

	/// <summary>
	/// Converts a delimited list of names to surname-first form.
	/// Input is expected in the "First Last, First Last" form produced by
	/// Book.AuthorNames / Book.NarratorNames.
	/// </summary>
	public static string ToSurnameFirst(string? names)
	{
		if (string.IsNullOrWhiteSpace(names))
			return string.Empty;

		LoadCustomizations();

		var stripNonPersons = LibationFileManager.Configuration.Instance.StripNonPersonAuthors;

		var converted = names
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Where(n => !stripNonPersons || !NonPersonNames.Contains(n))
			.Select(SingleToSurnameFirst)
			.Where(n => !string.IsNullOrWhiteSpace(n));

		var result = string.Join(Separator, converted);

		// Everything was stripped: better to show the original than an empty cell.
		return string.IsNullOrWhiteSpace(result) ? names : result;
	}

	/// <summary>
	/// Removes imprints, brands and other non-people from a comma-separated name list,
	/// leaving the remaining names in their original order. Returns the input unchanged
	/// when the setting is off, or when stripping would leave nothing.
	/// </summary>
	public static string StripNonPersons(string? names)
	{
		if (string.IsNullOrWhiteSpace(names))
			return string.Empty;

		if (!LibationFileManager.Configuration.Instance.StripNonPersonAuthors)
			return names;

		LoadCustomizations();

		var kept = names
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Where(n => !NonPersonNames.Contains(n))
			.ToList();

		return kept.Count == 0 ? names : string.Join(", ", kept);
	}

	/// <summary> Converts one name to "Surname, Given" form. </summary>
	public static string SingleToSurnameFirst(string? name)
	{
		if (string.IsNullOrWhiteSpace(name))
			return string.Empty;

		name = name.Trim();

		if (Overrides.TryGetValue(name, out var replacement))
			return replacement;

		// Already surname-first, or a deliberately punctuated name. Leave alone.
		if (name.Contains(','))
			return name;

		var tokens = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);

		// Mononyms ("Homer", "Plato") and empty input have no surname to move.
		if (tokens.Length < 2)
			return name;

		if (IsOrganization(tokens))
			return name;

		// Peel trailing suffixes: "Martin Luther King Jr." -> surname is "King".
		var end = tokens.Length - 1;
		var suffixes = new List<string>();
		while (end > 0 && Suffixes.Contains(tokens[end].TrimEnd('.')))
		{
			suffixes.Insert(0, tokens[end]);
			end--;
		}

		if (end < 1)
			return name;

		// Absorb particles: "Ursula K. Le Guin" -> surname is "Le Guin".
		var surnameStart = end;
		while (surnameStart > 1 && Particles.Contains(tokens[surnameStart - 1]))
			surnameStart--;

		var surname = string.Join(' ', tokens[surnameStart..(end + 1)]);
		var given = string.Join(' ', tokens[..surnameStart]);

		if (suffixes.Count > 0)
			surname = $"{surname} {string.Join(' ', suffixes)}";

		return string.IsNullOrWhiteSpace(given) ? surname : $"{surname}, {given}";
	}

	private static bool IsOrganization(IEnumerable<string> tokens)
		=> tokens.Any(t => OrganizationMarkers.Contains(t.TrimEnd('.', ','), StringComparer.OrdinalIgnoreCase));
}
