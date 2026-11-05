using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Verenigd.App.Diensten;
using Verenigd.App.ViewModels;
using Verenigd.App.Views.Thema;

namespace Verenigd.App.Views;

public partial class ShellWindow : Window
{
    private readonly ShellViewModel _viewModel;
    private bool _magSluiten;

    public ShellWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        _viewModel.PropertyChanged += ViewModelGewijzigd;
        Accentkleuren.PasToe(Resources, _viewModel.GekozenAdministratie?.Accentkleur);
    }

    /// <summary>Besluit 216: de accentkleur (knoppen, streepje, tellers) volgt de gekozen administratie.</summary>
    private void ViewModelGewijzigd(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.GekozenAdministratie))
        {
            if (_gevolgd is not null)
            {
                _gevolgd.PropertyChanged -= AdministratieGewijzigd;
            }

            _gevolgd = _viewModel.GekozenAdministratie;
            if (_gevolgd is not null)
            {
                _gevolgd.PropertyChanged += AdministratieGewijzigd;
            }

            Accentkleuren.PasToe(Resources, _gevolgd?.Accentkleur);
        }
    }

    private AdministratieViewModel? _gevolgd;

    private void AdministratieGewijzigd(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AdministratieViewModel.Accentkleur))
        {
            Accentkleuren.PasToe(Resources, _gevolgd?.Accentkleur);
        }
    }

    /// <summary>Besluit 221: een gemaximaliseerd venster met eigen titelbalk valt anders buiten het scherm; het pictogram wisselt.</summary>
    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        var rand = SystemParameters.WindowResizeBorderThickness;
        Wortel.Margin = WindowState == WindowState.Maximized
            ? new Thickness(rand.Left + 2, rand.Top + 2, rand.Right + 2, rand.Bottom + 2)
            : new Thickness(0);
        Maximaliseerknop.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
        System.Windows.Automation.AutomationProperties.SetName(Maximaliseerknop, WindowState == WindowState.Maximized ? "Herstellen" : "Maximaliseren");
        Maximaliseerknop.ToolTip = WindowState == WindowState.Maximized ? "Herstellen" : "Maximaliseren";
    }

    private void Minimaliseren_Click(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    private void Maximaliseren_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            SystemCommands.RestoreWindow(this);
        }
        else
        {
            SystemCommands.MaximizeWindow(this);
        }
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

    /// <summary>Besluit 273: een klik op <i>Meer…</i> klapt om; hij selecteert de regel niet.</summary>
    private void Navigatiebalk_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement(Navigatiebalk, (DependencyObject)e.OriginalSource) is ListBoxItem { DataContext: NavigatieItem { IsMeer: true } meer } regel)
        {
            regel.Focus();
            _viewModel.MeerWisselenCommand.Execute(meer);
            e.Handled = true;
        }
    }

    /// <summary>Besluit 273: Enter en Spatie klappen <i>Meer…</i> om; de pijltjes lopen er alleen overheen.</summary>
    private void Navigatiebalk_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space && Keyboard.FocusedElement is ListBoxItem { DataContext: NavigatieItem { IsMeer: true } meer })
        {
            _viewModel.MeerWisselenCommand.Execute(meer);
            e.Handled = true;
        }
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
