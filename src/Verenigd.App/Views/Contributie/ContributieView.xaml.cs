using System.Windows.Controls;
using System.Windows.Input;

namespace Verenigd.App.Views.Contributie;

public partial class ContributieView : UserControl
{
    public ContributieView()
    {
        InitializeComponent();
    }

    private void Zoeken_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        Zoekveld.Focus();
        Zoekveld.SelectAll();
    }
}
