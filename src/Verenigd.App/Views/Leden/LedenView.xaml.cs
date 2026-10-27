using System.Windows.Controls;
using System.Windows.Input;

namespace Verenigd.App.Views.Leden;

public partial class LedenView : UserControl
{
    public LedenView()
    {
        InitializeComponent();
    }

    // Ctrl+F: naar het zoekveld, zoals in andere Windows-applicaties.
    private void Zoeken_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        Zoekveld.Focus();
        Zoekveld.SelectAll();
    }
}
