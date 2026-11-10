using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using BtwBoeker.App.Views.Toegankelijkheid;

namespace BtwBoeker.App.Tests.UI;

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

    /// <summary>
    /// Toont het element in een onzichtbaar venster buiten beeld: <c>IsControlElement</c> volgt in WPF (sinds 4.7.1)
    /// <c>IsVisible</c>, en dat vraagt een getoond venster.
    /// </summary>
    private static void Toon(FrameworkElement element, Action<AutomationPeer> controleer)
    {
        var venster = new Window
        {
            WindowStyle = WindowStyle.None, ShowInTaskbar = false, ShowActivated = false, Left = -10000, Top = -10000, Width = 200, Height = 100,
            Content = new System.Windows.Controls.StackPanel { Children = { element } },
        };
        try
        {
            venster.Show();
            venster.UpdateLayout();
            var peer = UIElementAutomationPeer.CreatePeerForElement(element);
            controleer(peer);
        }
        finally
        {
            venster.Close();
        }
    }

    [Fact]
    public void Groepscontainers_hebben_een_peer_van_het_type_groep_met_naam_en_automationid() =>
        OpSta(() =>
        {
            foreach (FrameworkElement element in new FrameworkElement[] { new Groepskader(), new Groepspaneel(), new Groepsraster() })
            {
                AutomationProperties.SetName(element, "Journaalpost");
                AutomationProperties.SetAutomationId(element, "PostEditor");

                Toon(element, peer =>
                {
                    Assert.NotNull(peer);
                    Assert.Equal(AutomationControlType.Group, peer.GetAutomationControlType());
                    Assert.Equal("Journaalpost", peer.GetName());
                    Assert.Equal("PostEditor", peer.GetAutomationId());
                    Assert.True(peer.IsControlElement());
                });
            }

            System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
        });

    [Fact]
    public void Een_ingeklapte_groep_is_geen_control_element_voor_schermlezers() =>
        OpSta(() =>
        {
            var element = new Groepskader { Visibility = Visibility.Collapsed };

            Toon(element, peer => Assert.False(peer.IsControlElement()));

            System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown();
        });
}
