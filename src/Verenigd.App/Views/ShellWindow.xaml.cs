using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using Verenigd.App.Diensten;
using Verenigd.App.ViewModels;

namespace Verenigd.App.Views;

public partial class ShellWindow : Window
{
    private readonly ShellViewModel _viewModel;
    private bool _magSluiten;

    public ShellWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (_magSluiten)
        {
            return;
        }

        var controle = _viewModel.MagAfsluitenAsync();
        if (controle.IsCompleted)
        {
            e.Cancel = !await controle;
            return;
        }

        // Asynchrone controle: nu annuleren en daarna opnieuw sluiten (buiten deze Closing-afhandeling).
        e.Cancel = true;
        if (await controle)
        {
            _magSluiten = true;
            _ = Dispatcher.BeginInvoke(Close);
        }
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);

        // Zonder toetsenbordfocus in het venster werken de sneltoetsen (Ctrl+1...6, Ctrl+W) niet.
        if (!IsKeyboardFocusWithin && !Tabbladen.Focus())
        {
            MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        }
    }

    private void Afsluiten_Click(object sender, RoutedEventArgs e) => Close();

    private void LogbestandenOpenen_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(Paden.Logmap);
        Process.Start(new ProcessStartInfo(Paden.Logmap) { UseShellExecute = true });
    }

    private void Over_Click(object sender, RoutedEventArgs e)
    {
        var versie = typeof(ShellWindow).Assembly.GetName().Version?.ToString(3);
        MessageBox.Show(
            this,
            $"Verenigd {versie}\n\nLeden-, contributie- en financiële administratie voor de scoutingvereniging en -stichting.",
            "Over Verenigd",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
