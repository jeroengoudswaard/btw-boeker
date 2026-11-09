using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BtwBoeker.App.ViewModels.Boekhouding;

namespace BtwBoeker.App.Views.Boekhouding;

public partial class BoekhoudingView : UserControl
{
    public BoekhoudingView()
    {
        InitializeComponent();
    }

    private BoekhoudingViewModel? ViewModel => DataContext as BoekhoudingViewModel;

    /// <summary>Besluit 270: Excel en CSV samen onder één knop <i>Exporteren</i>.</summary>
    private void Exporteren_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } knop)
        {
            menu.PlacementTarget = knop;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }

    /// <summary>Alleen bestanden mogen worden neergezet (besluit 85).</summary>
    private void Bijlagen_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Postbijlagen_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paden && ViewModel?.BijlagenSlepenCommand.CanExecute(paden) == true)
        {
            ViewModel.BijlagenSlepenCommand.Execute(paden);
        }

        e.Handled = true;
    }

    private void Postvak_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paden)
        {
            ViewModel?.BonnenSlepenCommand.Execute(paden);
        }

        e.Handled = true;
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
