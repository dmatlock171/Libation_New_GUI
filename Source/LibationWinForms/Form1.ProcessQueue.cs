using DataLayer;
using Dinah.Core;
using LibationFileManager;
using LibationUiBase.GridView;
using LibationWinForms.ProcessQueue;
using System;
using System.Linq;
using System.Windows.Forms;

namespace LibationWinForms;

public partial class Form1
{
	int WidthChange = 0;

	/// <summary>
	/// True while the queue is detached into its own <see cref="ProcessBookForm"/>.
	/// <para/>
	/// Tracked explicitly rather than inferred from <c>PopoutButton.Visible</c>. Collapsing the
	/// queue removes the control from Panel2, and a parentless WinForms control reports
	/// Visible == false - so the old check could not tell "popped out" from "collapsed", and
	/// treated a collapsed queue as popped out. That made re-opening a no-op: the panel stayed
	/// shut and the new state was never saved, so it came back closed on every launch.
	/// </summary>
	private bool queueIsPoppedOut;

	/// <summary>Width of the process queue panel, in pixels, persisted across restarts.</summary>
	private const string QueuePaneWidthSetting = "QueuePaneWidth";

	private void Configure_ProcessQueue()
	{
		processBookQueue1.PopoutButton.Click += ProcessBookQueue1_PopOut;

		RestoreQueuePaneWidth();
		splitContainer1.SplitterMoved += SplitContainer1_SplitterMoved;

		WidthChange = splitContainer1.Panel2.Width + splitContainer1.SplitterWidth;
		int width = this.Width;
		var coppalseState = Configuration.Instance.GetNonString(defaultValue: false, nameof(splitContainer1.Panel2Collapsed));
		SetQueueCollapseState(coppalseState);
		this.Width = width;
	}

	/// <summary>
	/// SplitterDistance is measured from the left, but the queue lives in Panel2 on the right,
	/// so the saved value is the panel's own width and the distance is derived from it. That
	/// keeps the queue the same width when the window is resized between runs.
	/// </summary>
	private void RestoreQueuePaneWidth()
	{
		var saved = Configuration.Instance.GetNonString(defaultValue: splitContainer1.Panel2.Width, QueuePaneWidthSetting);
		if (saved <= 0)
			return;

		var available = splitContainer1.Width - splitContainer1.SplitterWidth;
		var distance = available - saved;

		// Panel1MinSize / Panel2MinSize make out-of-range assignments throw.
		var min = splitContainer1.Panel1MinSize;
		var max = available - splitContainer1.Panel2MinSize;
		if (max <= min)
			return;

		splitContainer1.SplitterDistance = Math.Clamp(distance, min, max);
	}

	private void SplitContainer1_SplitterMoved(object? sender, SplitterEventArgs e)
	{
		if (splitContainer1.Panel2Collapsed)
			return;

		Configuration.Instance.SetNonString(splitContainer1.Panel2.Width, QueuePaneWidthSetting);
	}

	private async void ProductsDisplay_LiberateClicked(object sender, System.Collections.Generic.IList<LibraryBook> libraryBooks, Configuration config)
	{
		try
		{
			if (await processBookQueue1.ViewModel.QueueDownloadDecryptAsync(libraryBooks, config))
				SetQueueCollapseState(false);
			else if (libraryBooks.Count == 1 && libraryBooks[0].Book.AudioExists)
			{
				// liberated: open explorer to file
				var filePath = AudibleFileStorage.Audio.GetPath(libraryBooks[0].Book.AudibleProductId);
				if (!Go.To.File(filePath?.ShortPathName))
				{
					var suffix = string.IsNullOrWhiteSpace(filePath) ? "" : $":\r\n{filePath}";
					MessageBox.Show($"File not found" + suffix);
				}
			}
		}
		catch (Exception ex)
		{
			Serilog.Log.Logger.Error(ex, "An error occurred while handling the stop light button click for {libraryBook}", libraryBooks);
		}
	}

	private async void ProductsDisplay_LiberateSeriesClicked(object sender, SeriesEntry series)
	{
		try
		{
			Serilog.Log.Logger.Information("Begin backing up all {series} episodes", series.LibraryBook);

			if (await processBookQueue1.ViewModel.QueueDownloadDecryptAsync(series.Children.Select(c => c.LibraryBook).UnLiberated().ToArray()))
				SetQueueCollapseState(false);
		}
		catch (Exception ex)
		{
			Serilog.Log.Logger.Error(ex, "An error occurred while backing up {series} episodes", series.LibraryBook);
		}
	}

	private async void ProductsDisplay_ConvertToMp3Clicked(object sender, LibraryBook[] libraryBooks)
	{
		try
		{
			if (await processBookQueue1.ViewModel.QueueConvertToMp3Async(libraryBooks))
				SetQueueCollapseState(false);
		}
		catch (Exception ex)
		{
			Serilog.Log.Logger.Error(ex, "An error occurred while handling the stop light button click for {libraryBook}", libraryBooks);
		}
	}

	private void SetQueueCollapseState(bool collapsed)
	{
		if (collapsed && !splitContainer1.Panel2Collapsed)
		{
			WidthChange = splitContainer1.Panel2.Width + splitContainer1.SplitterWidth;
			splitContainer1.Panel2.Controls.Remove(processBookQueue1);
			splitContainer1.Panel2Collapsed = true;
			Width -= WidthChange;
		}
		else if (!collapsed && splitContainer1.Panel2Collapsed)
		{
			if (queueIsPoppedOut)
				//Queue is in popout mode. Do nothing.
				return;

			Width += WidthChange;
			splitContainer1.Panel2.Controls.Add(processBookQueue1);
			splitContainer1.Panel2Collapsed = false;
			processBookQueue1.PopoutButton.Visible = true;
		}

		Configuration.Instance.SetNonString(splitContainer1.Panel2Collapsed, nameof(splitContainer1.Panel2Collapsed));
		toggleQueueHideBtn.Text = splitContainer1.Panel2Collapsed ? "❰❰❰" : "❱❱❱";
	}

	private void ToggleQueueHideBtn_Click(object sender, EventArgs e)
	{
		SetQueueCollapseState(!splitContainer1.Panel2Collapsed);
	}

	private void ProcessBookQueue1_PopOut(object? sender, EventArgs e)
	{
		ProcessBookForm dockForm = new();
		dockForm.WidthChange = splitContainer1.Panel2.Width + splitContainer1.SplitterWidth;
		dockForm.RestoreSizeAndLocation(Configuration.Instance);
		dockForm.FormClosing += DockForm_FormClosing;
		splitContainer1.Panel2.Controls.Remove(processBookQueue1);
		splitContainer1.Panel2Collapsed = true;
		processBookQueue1.PopoutButton.Visible = false;
		queueIsPoppedOut = true;
		dockForm.PassControl(processBookQueue1);
		dockForm.Show();
		this.Width -= dockForm.WidthChange;
		toggleQueueHideBtn.Visible = false;
		int deltax = filterBtn.Margin.Right + toggleQueueHideBtn.Width + toggleQueueHideBtn.Margin.Left;
		filterBtn.Location = new System.Drawing.Point(filterBtn.Location.X + deltax, filterBtn.Location.Y);
		filterSearchTb.Location = new System.Drawing.Point(filterSearchTb.Location.X + deltax, filterSearchTb.Location.Y);
	}

	private void DockForm_FormClosing(object? sender, FormClosingEventArgs e)
	{
		if (sender is ProcessBookForm dockForm)
		{
			this.Width += dockForm.WidthChange;
			splitContainer1.Panel2.Controls.Add(dockForm.RegainControl());
			splitContainer1.Panel2Collapsed = false;
			processBookQueue1.PopoutButton.Visible = true;
			queueIsPoppedOut = false;
			dockForm.SaveSizeAndLocation(Configuration.Instance);
			this.Focus();
			toggleQueueHideBtn.Visible = true;
			int deltax = filterBtn.Margin.Right + toggleQueueHideBtn.Width + toggleQueueHideBtn.Margin.Left;
			filterBtn.Location = new System.Drawing.Point(filterBtn.Location.X - deltax, filterBtn.Location.Y);
			filterSearchTb.Location = new System.Drawing.Point(filterSearchTb.Location.X - deltax, filterSearchTb.Location.Y);
		}
	}
}
