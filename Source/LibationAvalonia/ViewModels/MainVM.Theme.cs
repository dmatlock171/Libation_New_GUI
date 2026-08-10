using Avalonia.Threading;
using LibationFileManager;
using ReactiveUI;

namespace LibationAvalonia.ViewModels;

public partial class MainVM
{
	/// <summary> Wrap grid text that's too wide for its column, instead of clipping it. </summary>
	public bool GridTextWrapping => Configuration.Instance.GridTextWrapping;

	public void ToggleGridTextWrapping()
	{
		Configuration.Instance.GridTextWrapping = !Configuration.Instance.GridTextWrapping;
		this.RaisePropertyChanged(nameof(GridTextWrapping));
	}

	/// <summary> Hide imprints and brands from the last-name author columns. </summary>
	public bool StripNonPersonAuthors => Configuration.Instance.StripNonPersonAuthors;

	public void ToggleStripNonPersonAuthors()
	{
		Configuration.Instance.StripNonPersonAuthors = !Configuration.Instance.StripNonPersonAuthors;
		this.RaisePropertyChanged(nameof(StripNonPersonAuthors));
	}

	public bool ThemeIsSystem => Configuration.Instance.ThemeVariant is Configuration.Theme.System;
	public bool ThemeIsLight => Configuration.Instance.ThemeVariant is Configuration.Theme.Light;
	public bool ThemeIsDark => Configuration.Instance.ThemeVariant is Configuration.Theme.Dark;

	public void SetThemeSystem() => SetTheme(Configuration.Theme.System);
	public void SetThemeLight() => SetTheme(Configuration.Theme.Light);
	public void SetThemeDark() => SetTheme(Configuration.Theme.Dark);

	/// <summary>
	/// Applies a theme variant. App.axaml.cs listens for the config change and
	/// re-applies control templates app-wide.
	/// <para/>
	/// The write is deferred to the next dispatcher pass: changing the theme
	/// synchronously from a control's own event handler re-templates that control
	/// while it is still mid-event, which crashes with "already has a visual parent".
	/// </summary>
	private void SetTheme(Configuration.Theme theme)
	{
		if (Configuration.Instance.ThemeVariant == theme)
			return;

		Dispatcher.UIThread.Post(() =>
		{
			Configuration.Instance.ThemeVariant = theme;

			this.RaisePropertyChanged(nameof(ThemeIsSystem));
			this.RaisePropertyChanged(nameof(ThemeIsLight));
			this.RaisePropertyChanged(nameof(ThemeIsDark));
		}, DispatcherPriority.Background);
	}
}
