using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using BtwBoeker.App.Diensten;
using BtwBoeker.App.ViewModels.Boekhouding;

namespace BtwBoeker.App.Views.Boekhouding;

/// <summary>Voorbeeld van een bijlage: PDF via Windows zelf, afbeeldingen direct.</summary>
public partial class BijlageVenster : Window
{
    private readonly BijlageVoorbeeldViewModel _viewModel;

    public BijlageVenster(BijlageVoorbeeldViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        Loaded += async (_, _) => await LaadPaginasAsync();
    }

    /// <summary>De getoonde pagina's; ook voor de Windows-test.</summary>
    public IReadOnlyList<ImageSource> Paginas { get; private set; } = [];

    public async Task LaadPaginasAsync()
    {
        Paginatekst.Text = "Laden...";
        try
        {
            Paginas = _viewModel.IsPdf ? await PdfPaginas.PdfAsync(_viewModel.Inhoud) : [PdfPaginas.Afbeelding(_viewModel.Inhoud)];
            Paginalijst.ItemsSource = Paginas;
            Paginatekst.Text = Paginas.Count == 1 ? "1 pagina" : $"{Paginas.Count} pagina's";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Paginatekst.Text = string.Empty;
            _viewModel.Foutmelding = "Het voorbeeld kan niet worden getoond. Met Opslaan als... kunt u het bestand in een ander programma openen.";
        }
    }

    private void Sluiten_Executed(object sender, ExecutedRoutedEventArgs e) => Close();
}
