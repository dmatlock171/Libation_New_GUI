using System.Threading.Tasks;

namespace LibationAvalonia.ViewModels;

public partial class MainVM
{
	/// <summary>
	/// Every Audible Plus title you have not downloaded.
	/// <para>
	/// The first version of this required an expiry date within 90 days, on the assumption that
	/// a known deadline was what made a book at risk. The library disproved it: of the 95 titles
	/// that had already vanished, <em>none</em> carried an expiry date. Not one. So that filter
	/// would have caught none of the books it was built to save, while the 143 titles with dates
	/// — the only ones it did match — have never yet been lost.
	/// </para>
	/// <para>
	/// A missing date does not mean a title is safe. It means Audible has not announced when it
	/// goes, which is the normal case and evidently the dangerous one. So the filter no longer
	/// asks for a date; it asks the question that actually predicts loss — is this Plus, and is
	/// it still only on Audible's servers. The dates are used for ordering instead, so anything
	/// with a known deadline surfaces first.
	/// </para>
	/// </summary>
	public Task FilterAtRisk() => PerformFilter(new(BuildAtRiskFilter(), "At risk"));

	public string AtRiskTip
		=> "Show Audible Plus titles you haven't downloaded.\n"
		+ "Plus titles leave the catalogue on Audible's schedule, and most carry no published "
		+ "expiry date — every title lost from this library so far had none.\n"
		+ "Sort by Included Until to bring the ones with a known deadline to the top.";

	/// <summary>
	/// Explicit +/- rather than bare terms: Lucene's default operator here is OR, so
	/// "Plus -Liberated" would return everything Plus <em>or</em> everything not downloaded.
	/// The prefixes force each clause to be required regardless of the default.
	/// <para>
	/// <c>-Absent</c> drops titles already gone from Audible. They cannot be downloaded now, so
	/// listing them would be pointing at a fire that has already burnt out.
	/// </para>
	/// </summary>
	internal static string BuildAtRiskFilter()
		=> "+Plus -Liberated -Absent";
}
