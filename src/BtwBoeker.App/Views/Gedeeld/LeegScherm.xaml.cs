using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BtwBoeker.App.Views.Gedeeld;

/// <summary>
/// Een leeg scherm (besluit 168): uitleg en één knop, boven een lijst zonder regels. <see cref="Lijst"/> noemt de
/// AutomationId van die lijst; een architectuurtest controleert dat elke hoofdlijst er een heeft.
/// </summary>
public partial class LeegScherm : UserControl
{
    public static readonly DependencyProperty TitelProperty = DependencyProperty.Register(nameof(Titel), typeof(string), typeof(LeegScherm));
    public static readonly DependencyProperty TekstProperty = DependencyProperty.Register(nameof(Tekst), typeof(string), typeof(LeegScherm));
    public static readonly DependencyProperty KnoptekstProperty = DependencyProperty.Register(nameof(Knoptekst), typeof(string), typeof(LeegScherm));
    public static readonly DependencyProperty OpdrachtProperty = DependencyProperty.Register(nameof(Opdracht), typeof(ICommand), typeof(LeegScherm));
    public static readonly DependencyProperty LijstProperty = DependencyProperty.Register(nameof(Lijst), typeof(string), typeof(LeegScherm));

    public LeegScherm() => InitializeComponent();

    public string? Titel
    {
        get => (string?)GetValue(TitelProperty);
        set => SetValue(TitelProperty, value);
    }

    public string? Tekst
    {
        get => (string?)GetValue(TekstProperty);
        set => SetValue(TekstProperty, value);
    }

    public string? Knoptekst
    {
        get => (string?)GetValue(KnoptekstProperty);
        set => SetValue(KnoptekstProperty, value);
    }

    public ICommand? Opdracht
    {
        get => (ICommand?)GetValue(OpdrachtProperty);
        set => SetValue(OpdrachtProperty, value);
    }

    public string? Lijst
    {
        get => (string?)GetValue(LijstProperty);
        set => SetValue(LijstProperty, value);
    }
}
