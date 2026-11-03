using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Verenigd.App.ViewModels.Webapp;

namespace Verenigd.App.Views.Webapp;

public partial class WebappView : UserControl
{
    public WebappView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is WebappViewModel oud)
            {
                oud.PropertyChanged -= ViewModelGewijzigd;
            }

            if (e.NewValue is WebappViewModel nieuw)
            {
                nieuw.PropertyChanged += ViewModelGewijzigd;
            }
        };
    }

    /// <summary>Een PasswordBox kan niet binden; het wachtwoord gaat alleen naar het ViewModel en wordt na het inloggen gewist.</summary>
    private void Wachtwoord_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is WebappViewModel vm)
        {
            vm.Wachtwoord = Wachtwoord.Password;
        }
    }

    private void ViewModelGewijzigd(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WebappViewModel.Wachtwoord) && sender is WebappViewModel { Wachtwoord.Length: 0 } && Wachtwoord.Password.Length > 0)
        {
            Wachtwoord.Clear();
        }
    }
}
