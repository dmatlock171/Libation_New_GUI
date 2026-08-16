using LibationFileManager;
using ReactiveUI;

namespace LibationAvalonia.ViewModels;

public partial class MainVM
{
	// Configuration.GridFontScaleFactor is clamped to 0.5 - 2.0 and is persisted
	// to Settings.json. The Settings > Important slider writes the same value, so
	// the toolbar buttons and the slider stay in sync automatically.
	private const float MinFontScale = 0.5f;
	private const float MaxFontScale = 2f;
	private const float FontScaleStep = 0.1f;

	private const float DefaultScale = 1f;

	/// <summary>
	/// The sizes actually in effect, for the readouts beside the A and R buttons. Derived from
	/// ProductsDisplay's base constants rather than from a second copy of those numbers, so the
	/// readout cannot drift from what the grid is really doing. Both are in device-independent
	/// pixels, which is what Avalonia's FontSize and Height are measured in.
	/// </summary>
	public string GridFontSizeText => $"{LibationAvalonia.Views.ProductsDisplay.BaseTextFontSize * Configuration.Instance.GridFontScaleFactor:0.#}px";
	public string GridRowHeightText => $"{LibationAvalonia.Views.ProductsDisplay.BaseRowHeight * Configuration.Instance.GridScaleFactor:0}px";

	/// <summary>
	/// Watches the settings rather than the buttons, because the scale factors are also changed
	/// by Ctrl+wheel (which goes straight to ProductsDisplay) and by the Settings dialog sliders.
	/// Hooking the buttons alone would leave the readouts stale after either.
	/// </summary>
	private void Configure_GridScaling()
		=> Configuration.Instance.PropertyChanged += Configuration_GridScalingChanged;

	private void Configuration_GridScalingChanged(object? sender, Dinah.Core.PropertyChangedEventArgsEx? e)
	{
		if (e?.PropertyName is null or nameof(Configuration.GridFontScaleFactor))
			this.RaisePropertyChanged(nameof(GridFontSizeText));

		if (e?.PropertyName is null or nameof(Configuration.GridScaleFactor))
			this.RaisePropertyChanged(nameof(GridRowHeightText));
	}

	public void IncreaseGridFontSize() => StepGridFontScale(FontScaleStep);
	public void DecreaseGridFontSize() => StepGridFontScale(-FontScaleStep);

	/// <summary> Row height only. Leaves text size untouched. </summary>
	public void IncreaseRowHeight() => StepRowHeight(FontScaleStep);
	/// <summary> Row height only. Leaves text size untouched. </summary>
	public void DecreaseRowHeight() => StepRowHeight(-FontScaleStep);

	private static void StepRowHeight(float delta)
	{
		var config = Configuration.Instance;
		var gridScale = Clamp(config.GridScaleFactor + delta);

		if (gridScale != config.GridScaleFactor)
			config.GridScaleFactor = gridScale;
	}

	/// <summary> Restores row height to the default, leaving text size untouched. </summary>
	public void ResetRowHeight()
	{
		var config = Configuration.Instance;

		if (config.GridScaleFactor != DefaultScale)
			config.GridScaleFactor = DefaultScale;
	}

	/// <summary> Text size only. Row height is left exactly as it was. </summary>
	public void IncreaseTextOnly() => StepTextOnly(FontScaleStep);
	/// <summary> Text size only. Row height is left exactly as it was. </summary>
	public void DecreaseTextOnly() => StepTextOnly(-FontScaleStep);

	/// <summary> Restores text size to the default, leaving the row height setting untouched. </summary>
	public void ResetTextOnly()
	{
		var config = Configuration.Instance;

		if (config.GridFontScaleFactor != DefaultScale)
			config.GridFontScaleFactor = DefaultScale;
	}

	private static void StepTextOnly(float delta)
	{
		var config = Configuration.Instance;
		var fontScale = Clamp(config.GridFontScaleFactor + delta);

		if (fontScale != config.GridFontScaleFactor)
			config.GridFontScaleFactor = fontScale;
	}

	/// <summary> Restores both scale factors to the application default. </summary>
	public void ResetGridFontSize()
	{
		var config = Configuration.Instance;

		if (config.GridFontScaleFactor != DefaultScale)
			config.GridFontScaleFactor = DefaultScale;

		if (config.GridScaleFactor != DefaultScale)
			config.GridScaleFactor = DefaultScale;
	}

	/// <summary>
	/// Steps both scale factors together — the "zoom everything" gesture behind Ctrl+Plus,
	/// Ctrl+Minus and Ctrl+wheel, matching what those shortcuts do elsewhere.
	/// <para>
	/// The toolbar's A and R buttons deliberately do not use this: they step one factor each
	/// and are fully decoupled. Text large enough to outgrow the row will be clipped; use the
	/// R controls to make room. That is the accepted trade for each control doing one thing.
	/// </para>
	/// </summary>
	private static void StepGridFontScale(float delta)
	{
		var config = Configuration.Instance;

		var fontScale = Clamp(config.GridFontScaleFactor + delta);
		var gridScale = Clamp(config.GridScaleFactor + delta);

		// Avoid writing to Settings.json when already at a limit.
		if (fontScale != config.GridFontScaleFactor)
			config.GridFontScaleFactor = fontScale;

		if (gridScale != config.GridScaleFactor)
			config.GridScaleFactor = gridScale;
	}

	private static float Clamp(float scale)
		=> scale < MinFontScale ? MinFontScale
		: scale > MaxFontScale ? MaxFontScale
		: scale;
}
