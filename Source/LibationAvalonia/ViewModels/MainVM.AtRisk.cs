using LibationFileManager;
using System;
using System.Threading.Tasks;

namespace LibationAvalonia.ViewModels;

public partial class MainVM
{
	/// <summary>
	/// How far ahead "at risk" looks. Long enough to be worth acting on, short enough that the
	/// result stays a to-do list rather than a second copy of the library.
	/// </summary>
	public const int AtRiskDays = 90;

	/// <summary>
	/// Audible Plus titles that are about to leave the catalogue and have not been downloaded.
	/// <para>
	/// This is the one query worth a button. A Plus title that expires while undownloaded is
	/// simply gone, and nothing in the UI says so beforehand — it just stops being in the
	/// library. Owned titles are never at risk, which is why the filter is restricted to Plus.
	/// </para>
	/// </summary>
	public Task FilterAtRisk() => PerformFilter(new(BuildAtRiskFilter(), "At risk"));

	public string AtRiskTip
		=> $"Show Audible Plus titles you haven't downloaded that expire within {AtRiskDays} days.\n"
		+ "Once a Plus title expires it leaves your library whether or not you kept a copy.";

	/// <summary>
	/// Built fresh on every click so the window moves with the calendar rather than being frozen
	/// at whatever date it was saved.
	/// <para>
	/// Explicit +/- rather than bare terms: Lucene's default operator here is OR, so
	/// "Plus IncludedUntil:[...]" would return everything Plus <em>or</em> everything expiring.
	/// The prefixes force each clause to be required regardless of the default.
	/// </para>
	/// <para>
	/// <c>-Absent</c> drops titles already gone from Audible — they cannot be downloaded now, so
	/// listing them as at risk would be pointing at a fire that has already burnt out.
	/// </para>
	/// </summary>
	internal static string BuildAtRiskFilter(int days = AtRiskDays)
	{
		var from = DateTime.Today;
		var until = DateTime.Today.AddDays(days);

		return $"+Plus +{nameof(DataLayer.LibraryBook.IncludedUntil)}:[{from:yyyyMMdd} TO {until:yyyyMMdd}] -Liberated -Absent";
	}
}
