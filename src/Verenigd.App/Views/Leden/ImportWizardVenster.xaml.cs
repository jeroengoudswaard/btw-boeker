using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Verenigd.App.ViewModels.Leden;

namespace Verenigd.App.Views.Leden;

public partial class ImportWizardVenster : Window
{
    private readonly ImportWizardViewModel _viewModel;

    public ImportWizardVenster(ImportWizardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        _viewModel.PropertyChanged += ViewModelGewijzigd;
        MarkeerStap();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        // Tijdens het importeren niet sluiten: de transactie moet eerst klaar zijn.
        e.Cancel = _viewModel.IsBezig;
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PropertyChanged -= ViewModelGewijzigd;
        base.OnClosed(e);
    }

    private void ViewModelGewijzigd(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ImportWizardViewModel.Stap))
        {
            MarkeerStap();
        }
    }

    private void MarkeerStap()
    {
        var stappen = new[] { Stap1, Stap2, Stap3, Stap4 };
        for (var i = 0; i < stappen.Length; i++)
        {
            var actief = i == (int)_viewModel.Stap;
            stappen[i].FontWeight = actief ? FontWeights.SemiBold : FontWeights.Normal;
            if (actief)
            {
                stappen[i].ClearValue(ForegroundProperty);
                stappen[i].SetResourceReference(ForegroundProperty, "TextFillColorPrimaryBrush");
            }
            else
            {
                stappen[i].SetResourceReference(ForegroundProperty, "TextFillColorSecondaryBrush");
            }
        }
    }

    private void Venster_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) && _viewModel.Stap == ImportStap.Bestanden
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void Venster_Drop(object sender, DragEventArgs e)
    {
        if (_viewModel.Stap == ImportStap.Bestanden && e.Data.GetData(DataFormats.FileDrop) is string[] paden)
        {
            _viewModel.VoegBestandenToe(paden);
        }
    }

    private void Sluiten_Click(object sender, RoutedEventArgs e) => DialogResult = _viewModel.IsKlaar;
}
