using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using BtwBoeker.Application.Instellingen;
using BtwBoeker.Application.Weergaven;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.App.ViewModels;

/// <summary>
/// De woorden van de gekozen weergave (besluit 127): één exemplaar voor de hele app. Een wissel meldt <c>Item[]</c>,
/// zodat bindingen op de indexer en de app-resources (<c>T.</c>/<c>U.</c>) direct meegaan. Alleen visueel.
/// </summary>
public sealed partial class Woordenboek : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEenvoudig))]
    public partial Weergavemodus Weergave { get; set; }

    partial void OnWeergaveChanged(Weergavemodus value) => OnPropertyChanged("Item[]");

    public bool IsEenvoudig => Weergave == Weergavemodus.Eenvoudig;

    /// <exception cref="KeyNotFoundException">De sleutel staat niet in de woordenlijst.</exception>
    public string this[string sleutel] => Tekst(Woordenlijst.Zoek(sleutel));

    /// <summary>De uitleg als tooltip; alleen in Eenvoudig.</summary>
    public string? Uitleg(string sleutel) => IsEenvoudig ? Woordenlijst.Zoek(sleutel).Uitleg : null;

    /// <summary>Bijvoorbeeld "1 boeking" of "3 posten".</summary>
    public string Aantal(int aantal, string enkelvoud, string meervoud) =>
        string.Create(CultureInfo.CurrentCulture, $"{aantal} {this[aantal == 1 ? enkelvoud : meervoud]}");

    /// <summary>
    /// De kolomkoppen voor debet en credit van één potje (besluit 133): inkomsten- en uitgavenpotjes Uitgave/Inkomst,
    /// geldpotjes Erbij/Eraf, overige balanspotjes Debet/Credit.
    /// </summary>
    public (string Debet, string Credit) Kolomkoppen(Grootboekrekening rekening, bool isGeldpotje)
    {
        ArgumentNullException.ThrowIfNull(rekening);
        return rekening.Soort == RekeningSoort.Resultaat ? (this["Uitgave"], this["Inkomst"])
            : isGeldpotje ? (this["Erbij"], this["Eraf"])
            : (this["Debet"], this["Credit"]);
    }

    private string Tekst(Term term) => IsEenvoudig ? term.Eenvoudig : term.Expert;
}
