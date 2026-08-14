using AudibleUtilities;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using DataLayer;
using FileManager;
using LibationAvalonia.Dialogs;
using LibationAvalonia.ViewModels;
using LibationFileManager;
using LibationUiBase.Forms;
using LibationUiBase.GridView;
using ReactiveUI;
using ReactiveUI.Avalonia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LibationAvalonia.Views;

public partial class MainWindow : ReactiveWindow<MainVM>
{
	public MainWindow()
	{
		if (Design.IsDesignMode)
			Configuration.CreateMockInstance();

		ApiExtended.LoginChoiceFactory = account => Dispatcher.UIThread.Invoke(() => new Dialogs.Login.AvaloniaLoginChoiceEager(account));

		AudibleApiStorage.LoadError += AudibleApiStorage_LoadError;
		InitializeComponent();
		Configure_Upgrade();

		Opened += MainWindow_Opened;
		Closing += MainWindow_Closing;

		KeyBindings.Add(new KeyBinding { Command = ReactiveCommand.Create(selectAndFocusSearchBox), Gesture = new KeyGesture(Key.F, KeyGestureHelper.CommandModifier) });

		if (!Configuration.IsMacOs && ViewModel is MainVM vm)
		{
			KeyBindings.Add(new KeyBinding { Command = ReactiveCommand.Create(vm.ShowSettingsAsync), Gesture = new KeyGesture(Key.P, KeyGestureHelper.CommandModifier) });
			KeyBindings.Add(new KeyBinding { Command = ReactiveCommand.Create(vm.ShowAccountsAsync), Gesture = new KeyGesture(Key.A, KeyGestureHelper.CommandModifier | KeyModifiers.Shift) });
			KeyBindings.Add(new KeyBinding { Command = ReactiveCommand.Create(vm.ExportLibraryAsync), Gesture = new KeyGesture(Key.S, KeyGestureHelper.CommandModifier) });
		}

		Configuration.Instance.PropertyChanged += Settings_PropertyChanged;
		Settings_PropertyChanged(this, null);
		DataContext = new MainVM(this);
		Configure_QueuePaneWidth();
#if DEBUG
		Configure_DebugMenu();
#endif
	}

	/// <summary>Width of the process queue pane, in pixels, persisted across restarts.</summary>
	private const string QueuePaneWidthSetting = "QueuePaneWidth";
	private const double DefaultQueuePaneWidth = 400;

	/// <summary>
	/// The queue pane's column in <c>queueSplitGrid</c>. Reached through the grid because
	/// Avalonia does not generate a field for a named ColumnDefinition the way it does for
	/// named controls.
	/// </summary>
	private ColumnDefinition QueuePaneColumn => queueSplitGrid.ColumnDefinitions[2];

	/// <summary>The width to give the pane column when the queue is open.</summary>
	private double openQueuePaneWidth = DefaultQueuePaneWidth;

	/// <summary>
	/// Restores the saved queue pane width and keeps the pane column in step with
	/// <see cref="MainVM.QueueOpen"/>.
	/// </summary>
	private void Configure_QueuePaneWidth()
	{
		var saved = Configuration.Instance.GetNonString(defaultValue: DefaultQueuePaneWidth, QueuePaneWidthSetting);

		// Window.Width is NaN until the window is sized, and this runs from the constructor.
		// Feeding NaN through Clamp into GridLength throws, so only apply the "leave room for
		// the grid" guard once there is a real width to measure against.
		var maxSensible = double.IsNaN(Width) ? double.MaxValue : Math.Max(queuePane.MinWidth, Width - 200);

		var width = Math.Clamp(saved, queuePane.MinWidth, maxSensible);
		if (double.IsNaN(width) || double.IsInfinity(width))
			width = DefaultQueuePaneWidth;

		openQueuePaneWidth = width;

		if (DataContext is MainVM vm)
			vm.PropertyChanged += QueueOpen_PropertyChanged;

		ApplyQueuePaneWidth();
	}

	private void QueueOpen_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (e.PropertyName is null or nameof(MainVM.QueueOpen))
			ApplyQueuePaneWidth();
	}

	/// <summary>
	/// Collapsing the pane means collapsing its <em>column</em>, not just hiding the Border.
	/// A column with an explicit pixel width keeps that width whether or not its child is
	/// visible, which would leave an empty gap where the queue used to be.
	/// </summary>
	private void ApplyQueuePaneWidth()
	{
		var open = (DataContext as MainVM)?.QueueOpen ?? true;

		QueuePaneColumn.Width = open
			? new GridLength(openQueuePaneWidth, GridUnitType.Pixel)
			: new GridLength(0, GridUnitType.Pixel);
	}

	private void QueueSplitter_DragCompleted(object? sender, Avalonia.Input.VectorEventArgs e)
	{
		// Bounds is the rendered width of the pane itself, which is what we want to restore.
		var width = queuePane.Bounds.Width;
		if (width < queuePane.MinWidth)
			return;

		openQueuePaneWidth = width;
		Configuration.Instance.SetNonString(width, QueuePaneWidthSetting);
	}

#if DEBUG
	private void Configure_DebugMenu()
	{
		var simulateItem = new MenuItem { Header = "Simulate bad book failures (test dialog)..." };
		simulateItem.Click += async (_, _) =>
		{
			if (ViewModel is MainVM vm)
				await vm.SimulateBadBookFailuresAsync();
		};

		// Insert before Tour; the Separator above Tour in axaml already provides the divider.
		var items = settingsToolStripMenuItem.Items;
		var insertIndex = -1;
		for (var i = 0; i < items.Count; i++)
		{
			if (items[i] is MenuItem menuItem
				&& menuItem.Header?.ToString()?.Contains("Tour", StringComparison.OrdinalIgnoreCase) == true)
			{
				insertIndex = i;
				break;
			}
		}

		if (insertIndex < 0)
			insertIndex = items.Count;

		items.Insert(insertIndex, simulateItem);
	}
#endif

	[Dinah.Core.PropertyChangeFilter(nameof(Configuration.Books))]
	private void Settings_PropertyChanged(object? sender, Dinah.Core.PropertyChangedEventArgsEx? e)
	{
		if (!Configuration.IsWindows)
		{
			//The books directory does not support filenames with windows' invalid characters.
			//Tell the ReplacementCharacters configuration to treat those characters as invalid.
			ReplacementCharacters.AdditionalInvalidFilenameCharacters
				= Configuration.Instance.BooksCanWriteWindowsInvalidChars ? []
				: FileSystemTest.AdditionalInvalidWindowsFilenameCharacters.ToArray();
		}
	}

	private void AudibleApiStorage_LoadError(object? sender, AccountSettingsLoadErrorEventArgs e)
	{
		try
		{
			//Backup AccountSettings.json and create a new, empty file.
			var backupFile =
				FileUtility.SaferMoveToValidPath(
					e.SettingsFilePath,
					e.SettingsFilePath,
					Configuration.Instance.ReplacementCharacters,
					"bak");
			AudibleApiStorage.EnsureAccountsSettingsFileExists();
			e.Handled = true;

			showAccountSettingsRecoveredMessage(backupFile);
		}
		catch
		{
			showAccountSettingsUnrecoveredMessage();
		}

		async void showAccountSettingsRecoveredMessage(LongPath backupFile)
		{
			var ex = e.GetException();
			var body = AccountSettingsDecryptFailure.TryFindInTree(ex, out _)
				? AccountSettingsDecryptFailure.GetRecoveredDialogBody(ex, backupFile.PathWithoutPrefix)
				: $"""
					Libation could not load your account settings, so it had created a new, empty account settings file.

					You will need to re-add you Audible account(s) before scanning or downloading.

					The old account settings file has been archived at '{backupFile.PathWithoutPrefix}'

					{ex}
					""";

			await MessageBox.Show(
				this,
				body,
				AccountSettingsDecryptFailure.LoadErrorCaption,
				MessageBoxButtons.OK,
				MessageBoxIcon.Warning);
		}

		void showAccountSettingsUnrecoveredMessage()
		{
			var messageBoxWindow = MessageBox.Show(this, $"""
			Libation could not load your account settings. The file may be corrupted, but Libation is unable to delete it.

			Please move or delete the account settings file '{e.SettingsFilePath}'

			{e.GetException().ToString()}
			""",
			"Error Loading Account Settings",
			MessageBoxButtons.OK);

			//Force the message box to show synchronously because we're not handling the exception
			//and libation will crash after the event handler returns
			var frame = new DispatcherFrame();
			_ = messageBoxWindow.ContinueWith(static (_, s) => (s as DispatcherFrame)?.Continue = false, frame);
			Dispatcher.UIThread.PushFrame(frame);
			messageBoxWindow.GetAwaiter().GetResult();
		}
	}

	private async void MainWindow_Opened(object? sender, EventArgs e)
	{
		WindowChrome.ApplyThemeToTitleBar(this);

		await MessageBox.VerboseLoggingWarning_ShowIfTrue();

		if (AudibleFileStorage.BooksDirectory is null)
		{
			var result = await MessageBox.Show(
				this,
				"Please set a valid Books location in the settings dialog.",
				"Books Directory Not Set",
				MessageBoxButtons.OKCancel,
				MessageBoxIcon.Warning,
				MessageBoxDefaultButton.Button1);

			if (result is DialogResult.OK)
				await new SettingsDialog().ShowDialog(this);
		}

		if (Configuration.Instance.FirstLaunch)
		{
			var result = await MessageBox.Show(this, "Would you like a guided tour to get started?", "Libation Walkthrough", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);

			if (result is DialogResult.Yes)
			{
				await new Walkthrough(this).RunAsync();
			}

			Configuration.Instance.FirstLaunch = false;
		}
	}

	private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
	{
		productsDisplay?.CloseImageDisplay();
		this.SaveSizeAndLocation(Configuration.Instance);
		//This is double firing with 11.3.9
		Closing -= MainWindow_Closing;
	}

	private void selectAndFocusSearchBox()
	{
		filterSearchTb.SelectAll();
		filterSearchTb.Focus();
	}

	public async Task OnLibraryLoadedAsync(List<LibraryBook> initialLibrary)
	{
		//Get the ViewModel before crossing the await boundary
		if (ViewModel is not MainVM vm)
			return;

		if (QuickFilters.UseDefault)
			await vm.PerformFilter(QuickFilters.Filters.FirstOrDefault());

		vm.BindToGridTask = Task.WhenAll(
			vm.SetBackupCountsAsync(initialLibrary),
			Task.Run(() => vm.ProductsDisplay.BindToGridAsync(initialLibrary)));

		await vm.BindToGridTask;
	}

	public void ProductsDisplay_LiberateClicked(object _, IList<LibraryBook> libraryBook, Configuration config) => ViewModel?.LiberateClicked(libraryBook, config);
	public void ProductsDisplay_LiberateSeriesClicked(object _, SeriesEntry series) => ViewModel?.LiberateSeriesClicked(series);
	public void ProductsDisplay_ConvertToMp3Clicked(object _, LibraryBook[] libraryBook) => ViewModel?.ConvertToMp3Clicked(libraryBook);

	BookDetailsDialog? bookDetailsForm;
	public void ProductsDisplay_TagsButtonClicked(object _, LibraryBook libraryBook)
	{
		if (bookDetailsForm is null || !bookDetailsForm.IsVisible)
		{
			bookDetailsForm = new BookDetailsDialog(libraryBook);
			bookDetailsForm.Show(this);
		}
		else
			bookDetailsForm.LibraryBook = libraryBook;
	}

	public async void filterSearchTb_KeyPress(object _, KeyEventArgs e)
	{
		if (e.Key == Key.Return && ViewModel is not null)
		{
			await ViewModel.FilterBtn(filterSearchTb.Text ?? string.Empty);

			// silence the 'ding'
			e.Handled = true;
		}
	}

	private async void ClearFilterButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		if (ViewModel is null)
			return;
		await ViewModel.FilterBtn(string.Empty);
		// Typed text lives only in the TextBox (OneWay binding). If the VM filter was already empty,
		// PerformFilter does not refresh the binding, so clear the control explicitly (WinForms sets Text in performFilter).
		filterSearchTb.Text = string.Empty;
	}

	private void Configure_Upgrade()
	{
		setProgressVisible(false);
#pragma warning disable CS8321 // Local function is declared but never used
		async Task upgradeAvailable(LibationUiBase.UpgradeEventArgs e)
		{
			var notificationResult = await new UpgradeNotificationDialog(e.UpgradeProperties, e.CapUpgrade).ShowDialogAsync(this);

			e.Ignore = notificationResult == DialogResult.Ignore;
			e.InstallUpgrade = notificationResult == DialogResult.OK;
		}
#pragma warning restore CS8321 // Local function is declared but never used

		var upgrader = new LibationUiBase.Upgrader();
		upgrader.DownloadProgress += async (_, e) => await Dispatcher.UIThread.InvokeAsync(() => ViewModel?.DownloadProgress = e.ProgressPercentage);
		upgrader.DownloadBegin += async (_, _) => await Dispatcher.UIThread.InvokeAsync(() => setProgressVisible(true));
		upgrader.DownloadCompleted += async (_, _) => await Dispatcher.UIThread.InvokeAsync(() => setProgressVisible(false));
		upgrader.UpgradeFailed += async (_, message) => await Dispatcher.UIThread.InvokeAsync(() => { setProgressVisible(false); MessageBox.Show(this, message, "Upgrade Failed", MessageBoxButtons.OK, MessageBoxIcon.Error); });

#if !DEBUG
		Opened += async (_, _) => await upgrader.CheckForUpgradeAsync(upgradeAvailable);
#endif
	}

	private void setProgressVisible(bool visible) => ViewModel?.DownloadProgress = visible ? 0 : null;

	public SearchSyntaxDialog ShowSearchSyntaxDialog()
	{
		var dialog = new SearchSyntaxDialog();
		dialog.TagDoubleClicked += Dialog_TagDoubleClicked;
		dialog.Closed += Dialog_Closed;
		filterHelpBtn.IsEnabled = false;
		dialog.Show(this);
		return dialog;

		void Dialog_Closed(object? sender, EventArgs e)
		{
			dialog.TagDoubleClicked -= Dialog_TagDoubleClicked;
			filterHelpBtn.IsEnabled = true;
		}
		void Dialog_TagDoubleClicked(object? sender, string tag)
		{
			var text = filterSearchTb.Text;
			filterSearchTb.Text = text?.Insert(Math.Min(Math.Max(0, filterSearchTb.CaretIndex), text.Length), tag);
			filterSearchTb.CaretIndex += tag.Length;
			filterSearchTb.Focus();
		}
	}
}
