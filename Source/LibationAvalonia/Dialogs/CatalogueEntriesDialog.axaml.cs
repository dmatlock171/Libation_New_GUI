using ApplicationServices;
using Avalonia.Controls;
using LibationUiBase.Forms;
using System.Collections.Generic;

namespace LibationAvalonia.Dialogs;

/// <summary>
/// Records books you own but have no file for, so the library shows everything rather than only
/// what Libation could download.
/// <para>
/// Typed or pasted by the user. This deliberately does not read any retailer account: doing so
/// would mean driving an authenticated session, which retailer terms commonly prohibit even for
/// content you have bought.
/// </para>
/// </summary>
public partial class CatalogueEntriesDialog : DialogWindow
{
	public string? PastedText { get; set; }

	/// <summary>Parsed on save, so the caller does the database work rather than the dialog.</summary>
	public IReadOnlyList<LocalImportCommands.CatalogueEntry> Entries { get; private set; } = [];

	public CatalogueEntriesDialog() : base(saveAndRestorePosition: false)
	{
		InitializeComponent();

		// The base class submits on Enter, which would make a multi-line box impossible to use:
		// the first newline would close the dialog.
		SaveOnEnter = false;

		ControlToFocusOnShow = this.FindControl<TextBox>(nameof(entriesTb));

		DataContext = this;
	}

	private void Save_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		Entries = LocalImportCommands.ParseCatalogueList(PastedText);

		if (Entries.Count == 0)
		{
			// Closing with OK and importing nothing looks like a failure. Say so and stay open.
			_ = MessageBox.Show(
				this,
				"Nothing to add. Enter at least one line with a title.",
				"No entries found",
				MessageBoxButtons.OK,
				MessageBoxIcon.Information);
			return;
		}

		SaveAndClose();
	}

	private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
		=> CancelAndClose();
}
