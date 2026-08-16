using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using LibationFileManager;
using LibationUiBase.Forms;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;

namespace LibationAvalonia.ViewModels;

partial class MainVM
{
	private string lastGoodSearch = string.Empty;
	private QuickFilters.NamedFilter? lastGoodFilter => new(lastGoodSearch, null);

	/// <summary> Library filterting query </summary>
	public QuickFilters.NamedFilter? SelectedNamedFilter { get => field; set => this.RaiseAndSetIfChanged(ref field, value); } = new(string.Empty, null);
	public AvaloniaList<Control> QuickFilterMenuItems { get; } = new();

	/// <summary>
	/// How many saved quick filters get a ribbon button. They sit at the end of the ribbon so
	/// that if the window is too narrow for all of them, these clip rather than Scan or Download.
	/// Drop this to 5 if ten is too crowded.
	/// </summary>
	public const int RibbonQuickFilterCount = 10;

	/// <summary>The first <see cref="RibbonQuickFilterCount"/> saved filters, as ribbon buttons.</summary>
	public AvaloniaList<QuickFilterButton> RibbonQuickFilters { get; } = new();

	/// <summary>Hides the ribbon separator when there are no saved filters, so it does not sit
	/// there on its own. A bool rather than binding Count directly — Avalonia will not coerce an
	/// int to IsVisible.</summary>
	public bool AnyRibbonQuickFilters => RibbonQuickFilters.Count > 0;
	/// <summary> Indicates if the first quick filter is the default filter </summary>
	public bool FirstFilterIsDefault { get => field; set => QuickFilters.UseDefault = this.RaiseAndSetIfChanged(ref field, value); }

	private void Configure_Filters()
	{
		FirstFilterIsDefault = QuickFilters.UseDefault;
		MainWindow.Loaded += updateFiltersMenu;
		QuickFilters.Updated += updateFiltersMenu;

		//We need to be able to dynamically add and remove menu items from the Quick Filters menu.
		//To do that, we need quick filter's menu items source to be writable, which we can only
		//achieve by creating the list ourselves (instead of allowing Avalonia to create it from the xaml)

		QuickFilterMenuItems.Add(new MenuItem
		{

			Header = "Start Libation with 1st filter _Default",
			Command = ReactiveCommand.Create(ToggleFirstFilterIsDefault),
			Icon = new CheckBox
			{
				BorderThickness = new Thickness(0),
				IsHitTestVisible = false,
				[!CheckBox.IsCheckedProperty] = new Binding(nameof(FirstFilterIsDefault))
			}
		});
		QuickFilterMenuItems.Add(new MenuItem { Header = "_Edit quick filters...", Command = ReactiveCommand.Create(EditQuickFiltersAsync) });
		QuickFilterMenuItems.Add(new Separator());
	}

	public void AddQuickFilterBtn() { if (SelectedNamedFilter != null) QuickFilters.Add(SelectedNamedFilter); }
	public async Task FilterBtn(string filterString) => await PerformFilter(new(filterString, null));
	public void FilterHelpBtn() => MainWindow.ShowSearchSyntaxDialog();
	public void ToggleFirstFilterIsDefault() => FirstFilterIsDefault = !FirstFilterIsDefault;
	public async Task EditQuickFiltersAsync() => await new LibationAvalonia.Dialogs.EditQuickFilters().ShowDialog(MainWindow);
	public async Task PerformFilter(QuickFilters.NamedFilter? namedFilter)
	{
		SelectedNamedFilter = namedFilter;
		var tryFilter = namedFilter?.Filter;

		try
		{
			await ProductsDisplay.Filter(tryFilter);
			lastGoodSearch = namedFilter?.Filter ?? "";
		}
		catch (Exception ex)
		{
			Serilog.Log.Logger.Error(ex, "Error performing filtering. {@namedFilter} {@lastGoodFilter}", namedFilter, lastGoodFilter);
			await MessageBox.Show($"Bad filter string: \"{tryFilter}\"\r\n\r\n{ex.Message}", "Bad filter string", MessageBoxButtons.OK, MessageBoxIcon.Error);

			// re-apply last good filter
			namedFilter = (namedFilter ?? new(string.Empty, null)) with { Filter = lastGoodSearch };
			await PerformFilter(namedFilter);
		}
	}

	private void updateFiltersMenu(object? _ = null, object? __ = null)
	{
		if (NativeMenu.GetMenu(MainWindow)?.Items[3] is not NativeMenuItem ss ||
			ss.Menu is not NativeMenu quickFilterNativeMenu)
		{
			Serilog.Log.Logger.Error($"Unable to find {nameof(quickFilterNativeMenu)}");
			return;
		}

		//Clear all filters
		for (int i = quickFilterNativeMenu.Items.Count - 1; i >= 3; i--)
		{
			var command = ((NativeMenuItem)quickFilterNativeMenu.Items[i]).Command as IDisposable;
			if (command != null)
			{
				var existingBinding = MainWindow.KeyBindings.FirstOrDefault(kb => kb.Command == command);
				if (existingBinding != null)
					MainWindow.KeyBindings.Remove(existingBinding);

				command.Dispose();
			}

			quickFilterNativeMenu.Items.RemoveAt(i);
			QuickFilterMenuItems.RemoveAt(i);
		}

		// re-populate
		var index = 0;
		foreach (var filter in QuickFilters.Filters)
		{
			var command = ReactiveCommand.Create(async () => await PerformFilter(filter));

			var menuItem = new MenuItem { Header = $"{++index}: {(string.IsNullOrWhiteSpace(filter.Name) ? filter.Filter : filter.Name)}", Command = command };
			var nativeMenuItem = new NativeMenuItem { Header = $"{index}: {(string.IsNullOrWhiteSpace(filter.Name) ? filter.Filter : filter.Name)}", Command = command };

			if (Configuration.IsMacOs && index <= 10)
			{
				//Register hotkeys Command + 1 - 0 for quick filters
				var key = index == 10 ? Key.D0 : Key.D0 + index;
				nativeMenuItem.Gesture = new KeyGesture(key, KeyGestureHelper.CommandModifier);
			}
			else if (!Configuration.IsMacOs && index <= 12)
			{
				//Register hotkeys F1 - F12 for quick filters
				menuItem.InputGesture = new KeyGesture(Key.F1 + index - 1);
				MainWindow.KeyBindings.Add(new KeyBinding { Command = command, Gesture = menuItem.InputGesture });
			}

			QuickFilterMenuItems.Add(menuItem);
			quickFilterNativeMenu.Items.Add(nativeMenuItem);
		}

		rebuildRibbonQuickFilters();
	}

	/// <summary>
	/// Mirrors the first few saved filters onto the ribbon. Rebuilt wholesale rather than
	/// diffed: the list is at most ten items and only changes when the user edits their filters.
	/// </summary>
	private void rebuildRibbonQuickFilters()
	{
		RibbonQuickFilters.Clear();

		var index = 0;
		foreach (var filter in QuickFilters.Filters.Take(RibbonQuickFilterCount))
		{
			index++;

			// The hotkey differs by platform and is the same one the Quick Filters menu shows,
			// so the tooltip teaches the shortcut rather than leaving it buried in the menu.
			var hotkey
				= Configuration.IsMacOs ? $"{(index == 10 ? "Cmd+0" : $"Cmd+{index}")}"
				: index <= 12 ? $"F{index}"
				: null;

			RibbonQuickFilters.Add(new QuickFilterButton(filter, hotkey, () => PerformFilter(filter)));
		}

		this.RaisePropertyChanged(nameof(AnyRibbonQuickFilters));
	}
}

/// <summary>
/// One saved quick filter as a ribbon button. Exists because the ribbon needs a per-item command
/// — binding a single command with a parameter through a DataTemplate is harder to follow than
/// giving each button its own.
/// </summary>
public class QuickFilterButton
{
	public string Label { get; }
	public string Tip { get; }
	public ReactiveCommand<Unit, Unit> Apply { get; }

	public QuickFilterButton(QuickFilters.NamedFilter filter, string? hotkey, Func<Task> apply)
	{
		var name = string.IsNullOrWhiteSpace(filter.Name) ? filter.Filter : filter.Name;

		// Unnamed filters fall back to the raw query, which can be long. Truncate so one
		// verbose filter cannot push the rest of the ribbon off the window.
		Label = name.Length > 16 ? name[..15].TrimEnd() + "…" : name;

		Tip = string.IsNullOrWhiteSpace(filter.Name)
			? filter.Filter
			: $"{filter.Name}\n{filter.Filter}";

		if (hotkey is not null)
			Tip += $"\n({hotkey})";

		Apply = ReactiveCommand.CreateFromTask(apply);
	}
}
