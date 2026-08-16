using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
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

	/// <summary>
	/// The shade is stored per theme variant, so switching themes changes which value is in
	/// force. The readout is computed from the live brush and would otherwise keep showing the
	/// old theme's number until a button was pressed.
	/// </summary>
	private void Configure_RowShade()
	{
		if (App.Current is { } app)
			app.ActualThemeVariantChanged += RowShade_ThemeVariantChanged;
	}

	private void RowShade_ThemeVariantChanged(object? sender, EventArgs e)
		// App's own handler reapplies the theme's brushes on this same event. Deferring a
		// priority lets that finish first, so the readout reports the theme that is now in
		// force rather than the one being replaced.
		=> Dispatcher.UIThread.Post(RaiseRowShadeChanged, DispatcherPriority.Background);

	private void RaiseRowShadeChanged()
	{
		this.RaisePropertyChanged(nameof(RowShadeText));
		this.RaisePropertyChanged(nameof(RowShadeTip));
	}

	public void DarkenAlternatingRows() => StepRowShade(RowShadeStep);
	public void LightenAlternatingRows() => StepRowShade(-RowShadeStep);

	/// <summary>
	/// The shipped shade for the current theme. Kept in step with the two
	/// AlternatingRowBackgroundBrush entries in App.axaml.
	/// </summary>
	private static Color DefaultRowShade(ThemeVariant variant)
		=> variant == ThemeVariant.Dark
		? Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF)
		: Color.FromArgb(0x14, 0x00, 0x00, 0x00);

	public void ResetAlternatingRows()
	{
		if (App.Current?.ActualThemeVariant is { } variant)
			ApplyRowShade(variant, DefaultRowShade(variant));
	}

	/// <summary>The colour in use, as stored: alpha first, then RGB.</summary>
	public string RowShadeText => $"#{CurrentRowShade().ToUInt32():X8}";

	public string RowShadeTip
	{
		get
		{
			var c = CurrentRowShade();
			return $"Alternating row shading: {c.A * 100 / 255}% ({c.A} of 255).\n"
				+ "Only the alpha changes; the colour stays whatever the theme uses.\n"
				+ "Saved per theme, so light and dark are set separately, and it appears in the "
				+ $"theme editor as {AlternatingRowBrushKey}.";
		}
	}

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

			ApplyRowShade(variant, Color.FromArgb((byte)alpha, current.R, current.G, current.B));
		}
		catch (Exception ex)
		{
			Serilog.Log.Logger.Error(ex, "Failed to change the alternating row shading");
		}
	}

	private void ApplyRowShade(ThemeVariant variant, Color color)
	{
		try
		{
			var theme = ChardonnayTheme.GetLiveTheme();
			theme.SetColor(variant, AlternatingRowBrushKey, color);
			theme.ApplyTheme(variant);

			// Persist. Goes to ChardonnayTheme.json alongside every other themed colour, so
			// the theme editor and these buttons stay two views of one setting.
			theme.Save();

			RaiseRowShadeChanged();
		}
		catch (Exception ex)
		{
			Serilog.Log.Logger.Error(ex, "Failed to apply the alternating row shading");
		}
	}
}
