using LibationFileManager;
using System;

namespace LibationAvalonia;

/// <summary>
/// Single home for the library grid's text size and row height: the base sizes, the allowed
/// range, and the stepping.
/// <para>
/// Configuration stores these as scale factors, but nobody thinks in scale factors. Stepping the
/// factor by a flat 0.1 produced sizes like 12.1px, which is both ugly and impossible to aim at.
/// So the arithmetic happens in real sizes — 0.5px for text, 5px for rows — and the factor is
/// derived at the end. Every landing point is a round number, and pressing + on an odd value
/// snaps to the next one rather than carrying the oddness forward.
/// </para>
/// <para>
/// This exists as one class because the same stepping is reachable from the toolbar buttons, the
/// keyboard shortcuts and Ctrl+wheel, and those had drifted into three separate copies.
/// </para>
/// </summary>
public static class GridScaling
{
	/// <summary>Body text size at scale 1.</summary>
	public const double BaseTextFontSize = 11;
	/// <summary>Row height at scale 1.</summary>
	public const double BaseRowHeight = 80;

	// Configuration clamps to this range too; kept here so the sizes can be clamped before
	// being converted back into a factor.
	public const float MinScale = 0.5f;
	public const float MaxScale = 2f;

	public const double FontSizeStep = 0.5;
	public const double RowHeightStep = 5;

	/// <summary>Text size currently in effect, in device-independent pixels.</summary>
	public static double FontSize => BaseTextFontSize * Configuration.Instance.GridFontScaleFactor;
	/// <summary>Row height currently in effect, in device-independent pixels.</summary>
	public static double RowHeight => BaseRowHeight * Configuration.Instance.GridScaleFactor;

	/// <param name="direction">Positive to enlarge, negative to shrink.</param>
	public static void StepFontSize(int direction)
	{
		var next = Step(FontSize, FontSizeStep, direction, BaseTextFontSize);
		SetScale(next / BaseTextFontSize, isFont: true);
	}

	/// <param name="direction">Positive for taller rows, negative for shorter.</param>
	public static void StepRowHeight(int direction)
	{
		var next = Step(RowHeight, RowHeightStep, direction, BaseRowHeight);
		SetScale(next / BaseRowHeight, isFont: false);
	}

	public static void ResetFontSize() => SetScale(1, isFont: true);
	public static void ResetRowHeight() => SetScale(1, isFont: false);

	/// <summary>
	/// Moves to the next multiple of <paramref name="step"/> in the given direction, then clamps
	/// to the range the base size allows. Snapping rather than adding means a value left on an
	/// odd number by an older build, or by the Settings slider, is tidied up on the first press
	/// instead of staying odd forever.
	/// </summary>
	private static double Step(double current, double step, int direction, double baseSize)
	{
		// Nudge before rounding so a value already sitting exactly on a step does not get
		// swallowed by floating point and produce a no-op press.
		const double epsilon = 1e-6;
		var units = current / step;

		var next = direction > 0
			? (Math.Floor(units + epsilon) + 1) * step
			: (Math.Ceiling(units - epsilon) - 1) * step;

		return Math.Clamp(next, baseSize * MinScale, baseSize * MaxScale);
	}

	/// <summary>
	/// Writes only when the value actually changes, so sitting on + at the maximum does not
	/// rewrite Settings.json on every press.
	/// </summary>
	private static void SetScale(double scale, bool isFont)
	{
		var config = Configuration.Instance;
		var clamped = Math.Clamp((float)scale, MinScale, MaxScale);

		if (isFont)
		{
			if (clamped != config.GridFontScaleFactor)
				config.GridFontScaleFactor = clamped;
		}
		else
		{
			if (clamped != config.GridScaleFactor)
				config.GridScaleFactor = clamped;
		}
	}
}
