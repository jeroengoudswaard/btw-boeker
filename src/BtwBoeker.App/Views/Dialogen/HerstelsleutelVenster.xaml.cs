using System.ComponentModel;
using System.Windows;
using Microsoft.Win32;
using BtwBoeker.Application.Beveiliging;

namespace BtwBoeker.App.Views.Dialogen;

/// <summary>Toont de herstelsleutel. Sluiten kan pas na bevestiging dat de sleutel bewaard is.</summary>
public partial class HerstelsleutelVenster : Window
{
    private readonly string _sleutel;

    public HerstelsleutelVenster(Herstelsleutel herstelsleutel)
    {
        InitializeComponent();
        _sleutel = herstelsleutel.ToString();
        Sleutel.Text = _sleutel;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (Bewaard.IsChecked != true)
        {
            MessageBox.Show(
                this,
                "Bevestig eerst dat u de herstelsleutel veilig hebt bewaard. Zonder deze sleutel is een vergeten wachtwoord niet te herstellen.",
                "Herstelsleutel",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            e.Cancel = true;
        }

        base.OnClosing(e);
    }

    private void Kopieren_Click(object sender, RoutedEventArgs e) => Clipboard.SetText(_sleutel);

    private void Opslaan_Click(object sender, RoutedEventArgs e)
    {
        var dialoog = new SaveFileDialog
        {
            Title = "Herstelsleutel opslaan",
            Filter = "Tekstbestand (*.txt)|*.txt",
            FileName = "BTW-Boeker herstelsleutel.txt",
        };
        if (dialoog.ShowDialog(this) == true)
        {
            File.WriteAllText(
                dialoog.FileName,
                $"Herstelsleutel BTW-Boeker\r\n\r\n{_sleutel}\r\n\r\n"
                    + "Gebruik deze sleutel via 'Wachtwoord vergeten?' bij het openen van de database.\r\n"
                    + "Bewaar dit bestand NIET in dezelfde map als de database.\r\n");
        }
    }

    private void Bewaard_Changed(object sender, RoutedEventArgs e) => Doorgaan.IsEnabled = Bewaard.IsChecked == true;

    private void Doorgaan_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
