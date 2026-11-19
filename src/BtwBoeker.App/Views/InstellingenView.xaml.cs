using System.Windows;
using System.Windows.Controls;
using BtwBoeker.App.ViewModels;

namespace BtwBoeker.App.Views;

public partial class InstellingenView : UserControl
{
    public InstellingenView() => InitializeComponent();

    /// <summary>Besluit 42: de naam wordt bewaard zodra het veld de focus verliest.</summary>
    private void Naamveld_LostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is InstellingenViewModel vm && vm.BevestigNaamCommand.CanExecute(null))
        {
            vm.BevestigNaamCommand.Execute(null);
        }
    }
}
