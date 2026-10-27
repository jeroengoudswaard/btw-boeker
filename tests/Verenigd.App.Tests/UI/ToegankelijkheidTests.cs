using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using Verenigd.App.Views.Toegankelijkheid;

namespace Verenigd.App.Tests.UI;

/// <summary>De groepscontainers zijn zichtbaar voor UI Automation en schermlezers (huisregel 2). Alleen op Windows (WPF).</summary>
public sealed class ToegankelijkheidTests
{
    private static void OpSta(Action actie)
    {
        Exception? fout = null;
        var draad = new Thread(() =>
        {
            try
            {
                actie();
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

    [Fact]
    public void Groepscontainers_hebben_een_peer_van_het_type_groep_met_naam_en_automationid() =>
        OpSta(() =>
        {
            foreach (UIElement element in new UIElement[] { new Groepskader(), new Groepspaneel(), new Groepsraster() })
            {
                AutomationProperties.SetName(element, "Journaalpost");
                AutomationProperties.SetAutomationId(element, "PostEditor");

                var peer = UIElementAutomationPeer.CreatePeerForElement(element);

                Assert.NotNull(peer);
                Assert.Equal(AutomationControlType.Group, peer.GetAutomationControlType());
                Assert.Equal("Journaalpost", peer.GetName());
                Assert.Equal("PostEditor", peer.GetAutomationId());
                Assert.True(peer.IsControlElement());
            }
        });
}
