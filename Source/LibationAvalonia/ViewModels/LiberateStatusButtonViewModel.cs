using ReactiveUI;

namespace LibationAvalonia.ViewModels;

public class LiberateStatusButtonViewModel : ViewModelBase
{
	public bool IsError { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
	public bool IsButtonEnabled { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
	public bool IsSeries { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
	public bool Expanded { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
	/// <summary>A file Libation did not download, or a book with no file at all. Either way the
	/// stoplight is meaningless, so the button shows its own glyph instead.</summary>
	public bool IsLocal { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
	public bool IsCatalogueOnly { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
	public bool StoplightVisible { get => field; set => this.RaiseAndSetIfChanged(ref field, value); } = true;
	public bool RedVisible { get => field; set => this.RaiseAndSetIfChanged(ref field, value); } = true;
	public bool YellowVisible { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
	public bool GreenVisible { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
	public bool PdfDownloadedVisible { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
	public bool PdfNotDownloadedVisible { get => field; set => this.RaiseAndSetIfChanged(ref field, value); }
}
