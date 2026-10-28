using System.Windows;
using System.Windows.Controls;
using Verenigd.App.Diensten;
using Verenigd.App.ViewModels;
using Verenigd.Application.Instellingen;

namespace Verenigd.App.Tests.UI;

/// <summary>De app-resources T. en U. wisselen mee met de weergave (besluit 127; alleen op Windows, WPF).</summary>
public sealed class WoordenResourcesTests
{
    [Fact]
    public void DynamicResource_wisselt_direct_mee_met_de_weergave()
    {
        Exception? fout = null;
        var draad = new Thread(() =>
        {
            try
            {
                var woorden = new Woordenboek();
                var resources = new ResourceDictionary();
                WoordenResources.Koppel(resources, woorden);
                var tab = new TabItem { Resources = resources };
                tab.SetResourceReference(HeaderedContentControl.HeaderProperty, "T.Tab.Journaal");
                tab.SetResourceReference(FrameworkElement.ToolTipProperty, "U.Journaal");

                Assert.Equal("_Journaal", tab.Header);
                Assert.Null(tab.ToolTip);

                woorden.Weergave = Weergavemodus.Eenvoudig;

                Assert.Equal("_Alle boekingen", tab.Header);
                Assert.NotNull(tab.ToolTip);
            }
            catch (Exception ex)
            {
                fout = ex;
            }
        });
        draad.SetApartmentState(ApartmentState.STA);
        draad.Start();
        draad.Join();
        if (fout is not null)
        {
            throw new InvalidOperationException("Fout op de STA-thread.", fout);
        }
    }
}
