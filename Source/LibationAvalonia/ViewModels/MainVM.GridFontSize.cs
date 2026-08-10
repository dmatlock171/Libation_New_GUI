using LibationFileManager;

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

	public void IncreaseGridFontSize() => StepGridFontScale(FontScaleStep);
	public void DecreaseGridFontSize() => StepGridFontScale(-FontScaleStep);

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
	/// Steps the font scale and the row-height scale together. Row height is a fixed
	/// DataGridCell.Height derived from GridScaleFactor, so growing the font on its own
	/// clips the text inside rows that don't grow with it.
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
