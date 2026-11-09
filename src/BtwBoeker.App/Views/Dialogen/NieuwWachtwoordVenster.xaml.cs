using System.Windows;
using BtwBoeker.App.ViewModels.Dialogen;

namespace BtwBoeker.App.Views.Dialogen;

public partial class NieuwWachtwoordVenster : Window
{
    public NieuwWachtwoordVenster()
    {
        InitializeComponent();
        DataContext = ViewModel;
    }

    public NieuwWachtwoordViewModel ViewModel { get; } = new();

    // PasswordBox ondersteunt bewust geen binding; het wachtwoord gaat via code-behind naar het ViewModel.
    private void Wachtwoord_PasswordChanged(object sender, RoutedEventArgs e) => ViewModel.Wachtwoord = Wachtwoord.Password;

    private void Bevestiging_PasswordChanged(object sender, RoutedEventArgs e) => ViewModel.Bevestiging = Bevestiging.Password;

    private void Aanmaken_Click(object sender, RoutedEventArgs e) => DialogResult = ViewModel.KanBevestigen;
}
