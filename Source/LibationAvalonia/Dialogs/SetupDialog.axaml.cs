using Avalonia.Controls;
using LibationFileManager;
using LibationUiBase;
using LibationUiBase.Forms;

namespace LibationAvalonia.Dialogs;

public partial class SetupDialog : Window, ILibationSetup
{
	public bool IsNewUser { get; private set; }
	public bool IsReturningUser { get; private set; }
	public ComboBoxItem? SelectedTheme { get; set; }
	public SetupDialog()
	{
		InitializeComponent();
		DataContext = this;
	}

	public void NewUser_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		IsNewUser = true;
		ApplySelectedTheme();
		Close(DialogResult.OK);
	}

	public void ReturningUser_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		IsReturningUser = true;
		ApplySelectedTheme();
		Close(DialogResult.OK);
	}

	/// <summary>
	/// Saves the theme picked on the setup screen.
	/// <para>
	/// SelectedTheme was bound OneWayToSource and then read by nothing, so choosing Dark during
	/// setup had no effect and the app opened light. Persisting it here is enough: ShowMainWindow
	/// applies Configuration.ThemeVariant once setup returns.
	/// </para>
	/// </summary>
	private void ApplySelectedTheme()
	{
		try
		{
			if (SelectedTheme?.Content?.ToString() is not string name
				|| !System.Enum.TryParse<Configuration.Theme>(name, ignoreCase: true, out var theme))
				return;

			if (Configuration.Instance.ThemeVariant != theme)
				Configuration.Instance.ThemeVariant = theme;
		}
		catch (System.Exception ex)
		{
			// A theme is not worth failing setup over.
			Serilog.Log.Logger.Warning(ex, "Could not apply the theme chosen during setup");
		}
	}
}
