using ApplicationServices;
using Avalonia.Platform.Storage;
using LibationUiBase.Forms;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace LibationAvalonia.ViewModels;

public partial class MainVM
{
	/// <summary>
	/// Adds audiobooks already on disk: bought DRM-free from Libro.fm, Downpour or Authors
	/// Direct, ripped from CD, fetched from LibriVox, or inherited from another library manager.
	/// <para>
	/// Nothing is downloaded and nothing is decrypted — the files are already yours. Import also
	/// never moves or renames them, since they are organised the way you wanted them.
	/// </para>
	/// </summary>
	public async Task ImportLocalFolderAsync()
	{
		var folders = await MainWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
		{
			Title = "Choose a folder of audiobooks to add",
			AllowMultiple = false
		});

		if (folders.FirstOrDefault()?.TryGetLocalPath() is not string folder)
			return;

		try
		{
			var result = await LocalImportCommands.ImportFolderAsync(folder);

			var message
				= result.Imported > 0
				? $"Added {result.Imported} audiobook{(result.Imported == 1 ? "" : "s")} to your library."
				: "No new audiobooks found.\n\n"
				+ "Files already in your library are skipped, as are files carrying an Audible ID — "
				+ "those belong to Locate Audiobooks, which matches them to books you already have.";

			await MessageBox.Show(MainWindow, message, "Import complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}
		catch (Exception ex)
		{
			Serilog.Log.Logger.Error(ex, "Failed to import audiobooks from {Folder}", folder);
			await MessageBox.ShowAdminAlert(MainWindow, "Error importing audiobooks from that folder.", "Import failed", ex);
		}
	}

	/// <summary>
	/// Records books you own but have no file for, so the shelf is complete even for stores whose
	/// audio cannot legitimately leave their own app.
	/// </summary>
	public async Task AddCatalogueEntriesAsync()
	{
		var dialog = new LibationAvalonia.Dialogs.CatalogueEntriesDialog();

		if (await dialog.ShowDialogAsync(MainWindow) is not DialogResult.OK || dialog.Entries.Count == 0)
			return;

		try
		{
			var added = await LocalImportCommands.AddCatalogueEntriesAsync(dialog.Entries);

			await MessageBox.Show(
				MainWindow,
				$"Added {added} catalogue entr{(added == 1 ? "y" : "ies")} to your library.",
				"Entries added",
				MessageBoxButtons.OK,
				MessageBoxIcon.Information);
		}
		catch (Exception ex)
		{
			Serilog.Log.Logger.Error(ex, "Failed to add catalogue entries");
			await MessageBox.ShowAdminAlert(MainWindow, "Error adding catalogue entries.", "Could not add entries", ex);
		}
	}
}
