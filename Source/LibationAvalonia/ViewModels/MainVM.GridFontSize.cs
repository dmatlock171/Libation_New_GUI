using LibationFileManager;
using ReactiveUI;

namespace LibationAvalonia.ViewModels;

public partial class MainVM
{
	// The sizes, the range and the stepping all live in GridScaling, which the Ctrl+wheel
	// handler in ProductsDisplay uses too. These are just the commands the UI binds to.

	/// <summary> Text size only. Row height is left exactly as it was. </summary>
	public void IncreaseTextOnly() => GridScaling.StepFontSize(1);
	/// <summary> Text size only. Row height is left exactly as it was. </summary>
	public void DecreaseTextOnly() => GridScaling.StepFontSize(-1);
	/// <summary> Restores text size to the default, leaving the row height setting untouched. </summary>
	public void ResetTextOnly() => GridScaling.ResetFontSize();

	/// <summary> Row height only. Leaves text size untouched. </summary>
	public void IncreaseRowHeight() => GridScaling.StepRowHeight(1);
	/// <summary> Row height only. Leaves text size untouched. </summary>
	public void DecreaseRowHeight() => GridScaling.StepRowHeight(-1);
	/// <summary> Restores row height to the default, leaving text size untouched. </summary>
	public void ResetRowHeight() => GridScaling.ResetRowHeight();

	/// <summary>
	/// Steps both — the "zoom everything" gesture behind Ctrl+Plus, Ctrl+Minus and Ctrl+wheel,
	/// matching what those shortcuts do elsewhere.
	/// <para>
	/// The toolbar's A and R buttons deliberately do not use this: they step one each and are
	/// fully decoupled. Text large enough to outgrow its row will be clipped; use the R controls
	/// to make room. That is the accepted trade for each control doing one thing.
	/// </para>
	/// </summary>
	public void IncreaseGridFontSize() => StepBoth(1);
	public void DecreaseGridFontSize() => StepBoth(-1);

	/// <summary> Restores both text size and row height to the application default. </summary>
	public void ResetGridFontSize()
	{
		GridScaling.ResetFontSize();
		GridScaling.ResetRowHeight();
	}

	private static void StepBoth(int direction)
	{
		GridScaling.StepFontSize(direction);
		GridScaling.StepRowHeight(direction);
	}

	/// <summary>
	/// The sizes actually in effect, for the readouts beside the A and R buttons. Both are in
	/// device-independent pixels, which is what Avalonia's FontSize and Height are measured in.
	/// </summary>
	public string GridFontSizeText => $"{GridScaling.FontSize:0.#}px";
	public string GridRowHeightText => $"{GridScaling.RowHeight:0}px";

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
}
