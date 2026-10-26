using System.Windows;
using Verenigd.App.ViewModels.Dialogen;

namespace Verenigd.App.Views.Dialogen;

public partial class HerstelVenster : Window
{
    public HerstelVenster(string databaseNaam)
    {
        InitializeComponent();
        DataContext = ViewModel;
        Uitleg.Text = $"Voer de herstelsleutel van '{databaseNaam}' in en kies een nieuw wachtwoord. "
            + "De herstelsleutel kreeg u bij het aanmaken van de database.";
    }

    public HerstelViewModel ViewModel { get; } = new();

    private void Wachtwoord_PasswordChanged(object sender, RoutedEventArgs e) => ViewModel.Wachtwoord.Wachtwoord = Wachtwoord.Password;

    private void Bevestiging_PasswordChanged(object sender, RoutedEventArgs e) => ViewModel.Wachtwoord.Bevestiging = Bevestiging.Password;

    private void Herstellen_Click(object sender, RoutedEventArgs e) => DialogResult = ViewModel.KanBevestigen;
}
