using System.ComponentModel;
using System.Windows;
using Verenigd.App.ViewModels.Boekhouding;

namespace Verenigd.App.Views.Boekhouding;

/// <summary>Venster <i>Indeling bewerken</i> van de jaarrekening (besluiten 263–268).</summary>
public partial class JaarrekeningIndelingVenster : Window
{
    private readonly JaarrekeningIndelingViewModel _viewModel;

    public JaarrekeningIndelingVenster(JaarrekeningIndelingViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        _viewModel.Gesloten += Opgeslagen;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        e.Cancel = _viewModel.IsBezig;
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Gesloten -= Opgeslagen;
        base.OnClosed(e);
    }

    private void Opgeslagen(object? sender, EventArgs e) => DialogResult = true;
}
