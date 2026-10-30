using System.Windows;
using System.Windows.Controls;
using Verenigd.Application.Import;

namespace Verenigd.App.Views.Gedeeld;

public partial class Sol3Uitleg : UserControl
{
    public Sol3Uitleg() => InitializeComponent();

    /// <summary>Alleen het klembord vullen: geen logica, dus geen ViewModel nodig.</summary>
    private void Kopieer_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: Sol3Exportbeschrijving export })
        {
            Clipboard.SetText(export.Kopieertekst);
        }
    }
}
