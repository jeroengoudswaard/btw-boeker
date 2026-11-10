using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace BtwBoeker.App.Views.Toegankelijkheid;

/// <summary>
/// Een <see cref="Border"/> die voor UI Automation en schermlezers een groep is. Een gewone Border (en een panel) heeft geen
/// automation peer, zodat een AutomationId of Name erop onzichtbaar blijft. Geef altijd een AutomationProperties.Name mee.
/// </summary>
public sealed class Groepskader : Border
{
    protected override AutomationPeer OnCreateAutomationPeer() => new GroepPeer(this, nameof(Groepskader));
}

/// <summary>Een <see cref="StackPanel"/> die voor UI Automation een groep is (zie <see cref="Groepskader"/>).</summary>
public sealed class Groepspaneel : StackPanel
{
    protected override AutomationPeer OnCreateAutomationPeer() => new GroepPeer(this, nameof(Groepspaneel));
}

/// <summary>Een <see cref="Grid"/> die voor UI Automation een groep is (zie <see cref="Groepskader"/>).</summary>
public sealed class Groepsraster : Grid
{
    protected override AutomationPeer OnCreateAutomationPeer() => new GroepPeer(this, nameof(Groepsraster));
}

internal sealed class GroepPeer(FrameworkElement eigenaar, string klasse) : FrameworkElementAutomationPeer(eigenaar)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    protected override string GetClassNameCore() => klasse;
}
