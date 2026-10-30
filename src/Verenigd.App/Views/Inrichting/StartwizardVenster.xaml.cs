using System.ComponentModel;
using System.Windows;
using Verenigd.App.ViewModels.Inrichting;

namespace Verenigd.App.Views.Inrichting;

public partial class StartwizardVenster : Window
{
    private readonly StartwizardViewModel _viewModel;

    public StartwizardVenster(StartwizardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        _viewModel.SluitenGevraagd += SluitenGevraagd;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (_viewModel.IsGesloten)
        {
            return;
        }

        // ✕ of Alt+F4 werkt als "Later verder" (besluit 170); niet terwijl een stap bezig is.
        e.Cancel = true;
        if (!_viewModel.IsBezig)
        {
            _viewModel.LaterVerderCommand.Execute(null);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.SluitenGevraagd -= SluitenGevraagd;
        base.OnClosed(e);
    }

    /// <summary>Via de dispatcher: het verzoek kan binnen <see cref="OnClosing"/> komen, en dan mag het venster nog niet sluiten.</summary>
    private void SluitenGevraagd(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() => DialogResult = true);
}
