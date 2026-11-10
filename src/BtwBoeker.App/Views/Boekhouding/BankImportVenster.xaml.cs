using System.ComponentModel;
using System.Windows;
using BtwBoeker.App.ViewModels.Boekhouding;

namespace BtwBoeker.App.Views.Boekhouding;

public partial class BankImportVenster : Window
{
    private readonly BankImportViewModel _viewModel;

    public BankImportVenster(BankImportViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        _viewModel.PropertyChanged += ViewModelGewijzigd;
        MarkeerStap();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        // Niet sluiten terwijl de import loopt.
        e.Cancel = _viewModel.IsBezig;
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PropertyChanged -= ViewModelGewijzigd;
        base.OnClosed(e);
    }

    private void ViewModelGewijzigd(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BankImportViewModel.Stap))
        {
            MarkeerStap();
        }
    }

    private void MarkeerStap()
    {
        var stappen = new[] { Stap1, Stap2, Stap3 };
        for (var i = 0; i < stappen.Length; i++)
        {
            var actief = i == (int)_viewModel.Stap;
            stappen[i].FontWeight = actief ? FontWeights.SemiBold : FontWeights.Normal;
            stappen[i].SetResourceReference(ForegroundProperty, actief ? "TextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush");
        }
    }

    private void Sluiten_Click(object sender, RoutedEventArgs e) => DialogResult = _viewModel.IsKlaar;
}
