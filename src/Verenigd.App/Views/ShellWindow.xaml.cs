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

        // Zonder toetsenbordfocus in het venster werken de sneltoetsen (Ctrl+1...6, Ctrl+Tab) niet.
        if (!IsKeyboardFocusWithin && !Navigatiebalk.Focus())
        {
            MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        }
    }

    /// <summary>Besluit 179: in Eenvoudig toont Alt de menubalk tijdelijk, zodat ook Alt+letter werkt.</summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Key == Key.System && e.SystemKey is (Key.LeftAlt or Key.RightAlt) && !_viewModel.IsMenubalkZichtbaar)
        {
            _viewModel.ToonMenubalkTijdelijk();
            UpdateLayout();
        }

        base.OnPreviewKeyDown(e);
    }

    /// <summary>Verlaat de gebruiker het menu (Escape, klik elders of een keuze), dan verdwijnt de tijdelijke menubalk weer.</summary>
    private void Menubalk_IsKeyboardFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false)
        {
            _viewModel.VerbergTijdelijkeMenubalk();
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
