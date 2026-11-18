using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;

namespace BtwBoeker.App.Views.Gedeeld;

/// <summary>
/// UX-review stap 3 (besluit 38): het detail naast een lijst. Een kop met de titel en ✕ (<see cref="SluitOpdracht"/>), daaronder
/// de inhoud. Verschijnt alleen bij een selectie of <i>Nieuw</i>; voor schermlezers een groep met de naam van de kop.
/// </summary>
public sealed class Detailpaneel : HeaderedContentControl
{
    public static readonly DependencyProperty SluitOpdrachtProperty =
        DependencyProperty.Register(nameof(SluitOpdracht), typeof(ICommand), typeof(Detailpaneel));

    public ICommand? SluitOpdracht
    {
        get => (ICommand?)GetValue(SluitOpdrachtProperty);
        set => SetValue(SluitOpdrachtProperty, value);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new DetailpaneelPeer(this);

    private sealed class DetailpaneelPeer(Detailpaneel eigenaar) : FrameworkElementAutomationPeer(eigenaar)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string GetClassNameCore() => nameof(Detailpaneel);
    }
}
