using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LibationAvalonia.Themes;
using LibationFileManager;
using ReactiveUI;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace LibationAvalonia.ViewModels;

public partial class MainVM
{
	/// <summary> Contents of View > Theme > Saved schemes. Rebuilt whenever the folder changes. </summary>
	public AvaloniaList<Control> SavedThemeMenuItems { get; } = new();

	private void Configure_ThemeLibrary() => RefreshSavedThemesMenu();

	private void RefreshSavedThemesMenu()
	{
		SavedThemeMenuItems.Clear();

		foreach (var name in ThemeLibrary.ListThemeNames())
		{
			var themeName = name;
			SavedThemeMenuItems.Add(new MenuItem
			{
				Header = themeName,
				Command = ReactiveCommand.Create(() => ApplyNamedTheme(themeName)),
			});
		}

		if (SavedThemeMenuItems.Count == 0)
			SavedThemeMenuItems.Add(new MenuItem { Header = "(no saved schemes yet)", IsEnabled = false });

		SavedThemeMenuItems.Add(new Separator());

		SavedThemeMenuItems.Add(new MenuItem
		{
			Header = "_Save current colors as...",
			Command = ReactiveCommand.CreateFromTask(SaveCurrentThemeAsync),
		});

		SavedThemeMenuItems.Add(new MenuItem
		{
			Header = "_Open themes folder",
			Command = ReactiveCommand.Create(OpenThemesFolder),
		});
	}

	/// <summary>
	/// Copies the named scheme over ChardonnayTheme.json and re-applies it.
	/// Deferred to the next dispatcher pass: re-theming from inside a menu item's own
	/// command handler re-templates controls that are still mid-event.
	/// </summary>
	private void ApplyNamedTheme(string themeName)
	{
		Dispatcher.UIThread.Post(() =>
		{
			if (!ThemeLibrary.MakeCurrent(themeName))
				return;

			using var persister = ChardonnayThemePersister.Create();
			persister?.Target.ApplyTheme(Configuration.Instance.ThemeVariant);
		}, DispatcherPriority.Background);
	}

	/// <summary>
	/// Names the scheme with the native save dialog rather than a bespoke prompt,
	/// which also gives overwrite confirmation for free.
	/// </summary>
	private async Task SaveCurrentThemeAsync()
	{
		try
		{
			var folder = ThemeLibrary.EnsureThemesFolder();

			var startIn = await MainWindow.StorageProvider.TryGetFolderFromPathAsync(folder);

			var file = await MainWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
			{
				Title = "Save color scheme",
				SuggestedStartLocation = startIn,
				SuggestedFileName = "My scheme",
				DefaultExtension = "json",
				ShowOverwritePrompt = true,
				FileTypeChoices = [new FilePickerFileType("Color scheme") { Patterns = ["*.json"] }],
			});

			if (file?.TryGetLocalPath() is not string path)
				return;

			var themeName = ThemeLibrary.SanitizeName(
				System.IO.Path.GetFileNameWithoutExtension(path));

			ThemeLibrary.SaveCurrentAs(themeName);
			RefreshSavedThemesMenu();
		}
		catch (Exception ex)
		{
			Serilog.Log.Logger.Error(ex, "Failed to save color scheme");
		}
	}

	/// <summary>
	/// Opens the colour editor. Shown as a window rather than a dialog so changes can
	/// be previewed against the whole app, matching how Settings opens it.
	/// </summary>
	public void ShowThemeEditor()
	{
		if (Avalonia.Application.Current?.ApplicationLifetime
			is not Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime lifetime)
			return;

		if (lifetime.Windows.OfType<LibationAvalonia.Dialogs.ThemePickerDialog>().FirstOrDefault()
			is LibationAvalonia.Dialogs.ThemePickerDialog existing)
			existing.BringIntoView();
		else
			new LibationAvalonia.Dialogs.ThemePickerDialog().Show();
	}

	private void OpenThemesFolder()
	{
		try
		{
			Dinah.Core.Go.To.Folder(ThemeLibrary.EnsureThemesFolder());
		}
		catch (Exception ex)
		{
			Serilog.Log.Logger.Error(ex, "Failed to open themes folder");
		}
	}
}
