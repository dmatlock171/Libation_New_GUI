using Avalonia.Collections;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using LibationAvalonia.Themes;
using LibationUiBase.Forms;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LibationAvalonia.Dialogs;

public partial class ThemePickerDialog : DialogWindow
{
	public AvaloniaList<ThemeItemColor> ThemeColors { get; }
	private ChardonnayTheme ExistingTheme { get; } = ChardonnayTheme.GetLiveTheme();
	private ChardonnayTheme WorkingTheme { get; set; }

	public ThemePickerDialog()
	{
		InitializeComponent();
		CancelOnEscape = false;
		WorkingTheme = (ChardonnayTheme)ExistingTheme.Clone();
		ThemeColors = new(EnumerateThemeItemColors());

		foreach (var themeColor in ThemeColors)
			themeColor.ChangeRecorder = RecordColorChange;

		KeyBindings.Add(new Avalonia.Input.KeyBinding
		{
			Command = ReactiveUI.ReactiveCommand.Create(UndoLastColorChange),
			Gesture = new Avalonia.Input.KeyGesture(Avalonia.Input.Key.Z, Avalonia.Input.KeyModifiers.Control),
		});

		DataContext = this;
		Closing += ThemePickerDialog_Closing;
		Closed += ThemePickerDialog_Closed;

		//Rebuilding the FluentTheme re-templates every realised control. Doing that
		//while this window is open corrupts its visual tree, and the next time the
		//window re-templates (eg. reopening a colour picker) it throws
		//"Grid already has a visual parent". Palette changes are therefore batched
		//and applied once, after this window is gone.
		ChardonnayTheme.SuspendFluentRebuild = true;
	}

	private void ThemePickerDialog_Closed(object? sender, EventArgs e)
		=> ChardonnayTheme.SuspendFluentRebuild = false;

	private readonly Stack<(ThemeItemColor Item, Color Previous)> undoStack = new();
	private bool isUndoing;

	private void RecordColorChange(ThemeItemColor item, Color previous)
	{
		//Undoing sets a colour too; recording that would make undo a no-op loop.
		if (isUndoing)
			return;

		undoStack.Push((item, previous));
	}

	/// <summary> Ctrl+Z: reverts the most recent colour change. </summary>
	public void UndoLastColorChange()
	{
		if (undoStack.Count == 0)
			return;

		var (item, previous) = undoStack.Pop();

		isUndoing = true;
		try
		{
			item.ThemeColor = previous;
		}
		finally
		{
			isUndoing = false;
		}
	}

	private void ThemePickerDialog_Closing(object? sender, Avalonia.Controls.WindowClosingEventArgs e)
	{
		if (!e.IsProgrammatic)
		{
			CancelAndClose();
			e.Cancel = true;
		}
	}

	public async Task ImportTheme()
	{
		try
		{
			var openFileDialogOptions = new FilePickerOpenOptions
			{
				Title = $"Select the ChardonnayTheme.json file",
				AllowMultiple = false,
				FileTypeFilter =
				[
					new("JSON files (*.json)")
					{
						Patterns = ["*.json"],
						AppleUniformTypeIdentifiers  = ["public.json"]
					}
				]
			};

			var selectedFiles = await StorageProvider.OpenFilePickerAsync(openFileDialogOptions);
			var selectedFile = selectedFiles.SingleOrDefault()?.TryGetLocalPath();

			if (selectedFile is null) return;

			using (var theme = new ChardonnayThemePersister(selectedFile))
			{
				ResetTheme(theme.Target);
			}

			await MessageBox.Show(this, "Theme imported and applied", "Theme Imported");
		}
		catch (Exception ex)
		{
			await MessageBox.ShowAdminAlert(this, "Error attempting to import your chardonnay theme.", "Error Importing", ex);
		}
	}

	public async Task ExportTheme()
	{
		try
		{
			var options = new FilePickerSaveOptions
			{
				Title = "Where to export Library",
				SuggestedFileName = $"ChardonnayTheme",
				DefaultExtension = "json",
				ShowOverwritePrompt = true,
				FileTypeChoices =
				[
					new("JSON files (*.json)")
					{
						Patterns = ["*.json"],
						AppleUniformTypeIdentifiers = ["public.json"]
					},
					new("All files (*.*)") { Patterns = ["*"] }
				]
			};

			var selectedFile = (await StorageProvider.SaveFilePickerAsync(options))?.TryGetLocalPath();

			if (selectedFile is null) return;

			using (var theme = new ChardonnayThemePersister(WorkingTheme, selectedFile))
				theme.Target.Save();

			await MessageBox.Show(this, "Theme exported to:\r\n" + selectedFile, "Theme Exported");
		}
		catch (Exception ex)
		{
			await MessageBox.ShowAdminAlert(this, "Error attempting to export your chardonnay theme.", "Error Exporting", ex);
		}
	}

	public new void CancelAndClose()
	{
		ExistingTheme.ApplyTheme(ActualThemeVariant);
		base.CancelAndClose();
	}



	public void ResetColors()
		=> ResetTheme(ExistingTheme);

	public void LoadDefaultColors()
	{
		if (App.DefaultThemeColors is ChardonnayTheme defaults)
			ResetTheme(defaults);
	}

	public new async Task SaveAndCloseAsync()
	{
		using (var themePersister = ChardonnayThemePersister.Create())
		{
			if (themePersister is null)
			{
				await MessageBox.Show(this, "Failed to save the theme.", "Error saving theme", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
			else
			{
				foreach (var i in ThemeColors.OfType<ThemeItemColor>())
				{
					themePersister.Target.SetColor(ActualThemeVariant, i.ThemeItemName, i.ThemeColor);
				}
				themePersister.Target.Save();
			}
		}
		await base.SaveAndCloseAsync();
	}

	private void ResetTheme(ChardonnayTheme theme)
	{
		WorkingTheme = (ChardonnayTheme)theme.Clone();
		WorkingTheme.ApplyTheme(ActualThemeVariant);

		foreach (var i in ThemeColors.OfType<ThemeItemColor>())
		{
			i.ColorSetter = null;
			i.ThemeColor = WorkingTheme.GetColor(ActualThemeVariant, i.ThemeItemName);
			i.ColorSetter = ColorSetter;
		}
	}

	private IEnumerable<ThemeItemColor> EnumerateThemeItemColors()
		=> WorkingTheme
		.GetThemeColors(ActualThemeVariant)
		.Select(kvp => new ThemeItemColor
		{
			ThemeItemName = kvp.Key,
			ThemeColor = kvp.Value,
			ColorSetter = ColorSetter
		});

	private void ColorSetter(Color color, string colorName)
	{
		WorkingTheme.SetColor(ActualThemeVariant, colorName, color);
		WorkingTheme.ApplyTheme(ActualThemeVariant);
	}

	public class ThemeItemColor : ViewModels.ViewModelBase
	{
		public required string ThemeItemName { get; init; }
		public required Action<Color, string>? ColorSetter { get; set; }

		/// <summary> Called with the previous colour before each change, to build an undo stack. </summary>
		public Action<ThemeItemColor, Color>? ChangeRecorder { get; set; }

		/// <summary>
		/// User's own note about what this colour affects. Loaded lazily because
		/// ThemeItemName isn't available until after object initialization, and saved
		/// on every edit so notes aren't lost if the editor crashes.
		/// </summary>
		public string Description
		{
			get => field ??= Themes.ThemeDescriptions.Get(ThemeItemName);
			set
			{
				this.RaiseAndSetIfChanged(ref field, value ?? string.Empty);
				Themes.ThemeDescriptions.Set(ThemeItemName, field);
			}
		}

		public Color ThemeColor
		{
			get => field;
			set
			{
				var setColors = !field.Equals(value);
				var previous = field;
				this.RaiseAndSetIfChanged(ref field, value);
				if (setColors)
				{
					ChangeRecorder?.Invoke(this, previous);
					ColorSetter?.Invoke(field, ThemeItemName);
				}
			}
		}
	}
}
