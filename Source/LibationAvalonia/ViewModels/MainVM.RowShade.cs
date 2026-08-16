using Avalonia.Controls;
using Avalonia.Media;
using ReactiveUI;
using System;
using System.Linq;

namespace LibationAvalonia.ViewModels;

public partial class MainVM
{
	private const string AlternatingRowBrushKey = "AlternatingRowBackgroundBrush";

	/// <summary>
	/// Alpha steps, out of 255. Small enough that holding the button is a dimmer rather than
	/// a switch.
	/// </summary>
	private const int RowShadeStep = 6;

	/// <summary>
	/// Never 0: <see cref="ChardonnayTheme.ApplyTheme"/> skips any stored colour equal to
	/// default(Color), which is fully transparent black, so alpha 0 would silently mean
	/// "no override" instead of "no shading".
	/// </summary>
	private const int RowShadeMin = 2;

	/// <summary>Past this the shading stops being a stripe and starts being a second colour.</summary>
	private const int RowShadeMax = 0x70;

	public void DarkenAlternatingRows() => StepRowShade(RowShadeStep);
	public void LightenAlternatingRows() => StepRowShade(-RowShadeStep);

	public string RowShadeText => $"{CurrentRowShade().A * 100 / 255}%";

	public string RowShadeTip
		=> "Strength of the alternating row shading.\n"
		+ "Saved per theme, so light and dark are set separately, and it appears in the theme "
		+ $"editor as {AlternatingRowBrushKey}.";

	/// <summary>
	/// Reads the brush actually in use rather than the stored override, so the first press
	/// steps from what is on screen even when the user has never touched this before.
	/// </summary>
	private static Color CurrentRowShade()
	{
		try
		{
			// Same access pattern ChardonnayTheme itself uses.
			if (App.Current?.ActualThemeVariant is { } variant
				&& App.Current.Resources.ThemeDictionaries[variant] is ResourceDictionary themeBrushes
				&& themeBrushes[AlternatingRowBrushKey] is ISolidColorBrush brush)
				return brush.Color;
		}
		catch (Exception ex)
		{
			Serilog.Log.Logger.Debug(ex, "Could not read the alternating row brush");
		}

		return Colors.Transparent;
	}

	private void StepRowShade(int delta)
	{
		try
		{
			if (App.Current?.ActualThemeVariant is not { } variant)
				return;

			var current = CurrentRowShade();

			// Keep the hue the theme already chose — black on light, white on dark — and move
			// only the alpha. Stepping the colour instead would make one theme's shade drift
			// towards the other's.
			var alpha = Math.Clamp(current.A + delta, RowShadeMin, RowShadeMax);
			if (alpha == current.A)
				return;

			var theme = ChardonnayTheme.GetLiveTheme();
			theme.SetColor(variant, AlternatingRowBrushKey, Color.FromArgb((byte)alpha, current.R, current.G, current.B));
			theme.ApplyTheme(variant);

			// Persist. Goes to ChardonnayTheme.json alongside every other themed colour, so
			// the theme editor and these buttons stay two views of one setting.
			theme.Save();

			this.RaisePropertyChanged(nameof(RowShadeText));
		}
		catch (Exception ex)
		{
			Serilog.Log.Logger.Error(ex, "Failed to change the alternating row shading");
		}
	}
}
