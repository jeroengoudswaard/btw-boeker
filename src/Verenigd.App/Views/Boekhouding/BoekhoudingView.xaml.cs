using System.Windows.Controls;
using System.Windows.Input;
using Verenigd.App.ViewModels.Boekhouding;

namespace Verenigd.App.Views.Boekhouding;

public partial class BoekhoudingView : UserControl
{
    public BoekhoudingView()
    {
        InitializeComponent();
    }

    private void Zoeken_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        Zoekveld.Focus();
        Zoekveld.SelectAll();
    }

    /// <summary>Ctrl+Enter boekt de post, ook vanuit een cel van de regels (het DataGrid handelt Enter zelf af).</summary>
    private void Tabblad_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control && DataContext is BoekhoudingViewModel vm && vm.BoekenCommand.CanExecute(null))
        {
            e.Handled = true;
            vm.BoekenCommand.Execute(null);
        }
    }
}
