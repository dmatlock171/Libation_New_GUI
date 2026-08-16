using AppScaffolding;
using Avalonia.Controls;
// Avalonia 12 moved the clipboard convenience methods off IClipboard and onto
// ClipboardExtensions in this namespace; without it SetTextAsync does not resolve.
using Avalonia.Input.Platform;
using Avalonia.Media;
using LibationAvalonia.ViewModels;
using LibationFileManager;
using ReactiveUI;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LibationAvalonia.Dialogs;

/// <summary>
/// Read-only "where is everything and is it working" dialog. Everything here is also written
/// to the log at startup, which is exactly the problem: when logging itself is misconfigured
/// the log cannot tell you so. This dialog is the one place that reports its own failure.
/// </summary>
public partial class DiagnosticsDialog : DialogWindow
{
	private readonly DiagnosticsVM _viewModel;

	public DiagnosticsDialog() : base(saveAndRestorePosition: false)
	{
		InitializeComponent();

		DataContext = _viewModel = new DiagnosticsVM();
	}

	private async void Copy_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
			await clipboard.SetTextAsync(_viewModel.PlainText);
	}

	/// <summary>
	/// Mirrors the existing "open log folder" behaviour in ImportantSettingsVM: open the log
	/// file when there is one, otherwise fall back to the folder it should have been in.
	/// </summary>
	private void OpenLog_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		var logPath = LogFileFilter.LogFilePath;

		if (!string.IsNullOrWhiteSpace(logPath) && File.Exists(logPath))
			Dinah.Core.Go.To.File(logPath);
		else
			OpenLibationFiles_Click(sender, e);
	}

	private void OpenLibationFiles_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
		=> Dinah.Core.Go.To.Folder(Configuration.Instance.LibationFiles.Location.ShortPathName);

	private void Refresh_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
		=> _viewModel.Refresh();

	private void Close_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
		=> CancelAndClose();
}

public class DiagnosticsVM : ViewModelBase
{
	public List<DiagnosticSectionVM> Sections { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); } = [];
	public string PlainText { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); } = string.Empty;
	public bool HasProblems { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); }
	public string SummaryText { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); } = string.Empty;
	public IBrush SummaryBrush { get => field; private set => this.RaiseAndSetIfChanged(ref field, value); } = DiagnosticBrushes.Ok;

	public DiagnosticsVM() => Refresh();

	public void Refresh()
	{
		var sections = LibationDiagnostics.Collect();

		Sections = sections.Select(s => new DiagnosticSectionVM(s)).ToList();
		PlainText = LibationDiagnostics.ToPlainText(sections);

		var all = sections.SelectMany(s => s.Items).ToList();
		var errors = all.Count(i => i.Severity == DiagnosticSeverity.Error);
		var warnings = all.Count(i => i.Severity == DiagnosticSeverity.Warning);

		HasProblems = errors > 0 || warnings > 0;
		SummaryBrush = errors > 0 ? DiagnosticBrushes.Error : DiagnosticBrushes.Warning;
		SummaryText = (errors, warnings) switch
		{
			(0, 0) => string.Empty,
			(0, _) => $"{Plural(warnings, "warning")} — see the highlighted rows below.",
			(_, 0) => $"{Plural(errors, "problem")} found — see the highlighted rows below.",
			_ => $"{Plural(errors, "problem")} and {Plural(warnings, "warning")} found — see the highlighted rows below."
		};

		static string Plural(int count, string noun) => $"{count} {noun}{(count == 1 ? "" : "s")}";
	}
}

public class DiagnosticSectionVM(DiagnosticSection section)
{
	public string Name { get; } = section.Name;
	public List<DiagnosticItemVM> Items { get; } = section.Items.Select(i => new DiagnosticItemVM(i)).ToList();
}

public class DiagnosticItemVM(DiagnosticItem item)
{
	public string Label { get; } = item.Label;
	public string Value { get; } = item.Value;
	public string? Detail { get; } = item.Detail;
	public bool HasDetail { get; } = !string.IsNullOrWhiteSpace(item.Detail);

	/// <summary>Left accent bar and detail text. Transparent for healthy rows so only
	/// problems draw the eye.</summary>
	public IBrush AccentBrush { get; } = DiagnosticBrushes.For(item.Severity);
}

internal static class DiagnosticBrushes
{
	public static readonly IBrush Ok = Brushes.Transparent;
	public static readonly IBrush Warning = new SolidColorBrush(Color.FromRgb(0xE8, 0xA3, 0x17));
	public static readonly IBrush Error = new SolidColorBrush(Color.FromRgb(0xD6, 0x45, 0x41));

	public static IBrush For(DiagnosticSeverity severity) => severity switch
	{
		DiagnosticSeverity.Error => Error,
		DiagnosticSeverity.Warning => Warning,
		_ => Ok
	};
}
