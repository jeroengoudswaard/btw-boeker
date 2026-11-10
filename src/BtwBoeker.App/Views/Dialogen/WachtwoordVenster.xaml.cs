using System.Windows;
using BtwBoeker.App.Diensten;

namespace BtwBoeker.App.Views.Dialogen;

public partial class WachtwoordVenster : Window
{
    public WachtwoordVenster(string databaseNaam, string? foutmelding)
    {
        InitializeComponent();
        Uitleg.Text = $"Voer het wachtwoord in voor '{databaseNaam}'.";
        if (foutmelding is not null)
        {
            Fout.Text = foutmelding;
            Fout.Visibility = Visibility.Visible;
        }
    }

    public WachtwoordInvoer? Invoer { get; private set; }

    private void Openen_Click(object sender, RoutedEventArgs e)
    {
        Invoer = new WachtwoordInvoer(Wachtwoord.Password, HerstelGevraagd: false);
        DialogResult = true;
    }

    private void Vergeten_Click(object sender, RoutedEventArgs e)
    {
        Invoer = new WachtwoordInvoer(null, HerstelGevraagd: true);
        DialogResult = true;
    }
}
