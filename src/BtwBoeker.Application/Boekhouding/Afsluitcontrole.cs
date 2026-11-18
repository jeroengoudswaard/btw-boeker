using System.Globalization;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Application.Boekhouding;

/// <summary>Waarom een boekjaar (nog) niet kan worden afgesloten.</summary>
public enum Afsluitbeletsel
{
    Geen,
    AlAfgesloten,
    EerderJaarOpen,
    Concepten,
    GeenReserve,
}

/// <summary>
/// Besluit 40: de voorwaarden om een boekjaar af te sluiten, op één plek. De dienst gooit met <see cref="Reden"/> een fout;
/// de app toont de reden en wat eerst moet, in plaats van een knop die niet kan.
/// </summary>
public sealed record Afsluitcontrole(Afsluitbeletsel Beletsel, int? EerderJaar = null, int Concepten = 0)
{
    public bool KanAfsluiten => Beletsel == Afsluitbeletsel.Geen;

    public static Afsluitcontrole Voor(BoekhoudingStand stand, int jaar)
    {
        ArgumentNullException.ThrowIfNull(stand);
        var boekjaar = stand.Boekjaar(jaar) ?? throw new InvalidOperationException($"Boekjaar {jaar.ToString(CultureInfo.InvariantCulture)} bestaat niet.");
        if (!boekjaar.IsOpen)
        {
            return new(Afsluitbeletsel.AlAfgesloten);
        }

        if (stand.Boekjaren.Where(b => b.Jaar < jaar && b.IsOpen).Select(b => (int?)b.Jaar).Min() is { } eerder)
        {
            return new(Afsluitbeletsel.EerderJaarOpen, EerderJaar: eerder);
        }

        var concepten = stand.Posten.Count(p => !p.IsGeboekt && p.Datum.Year == jaar);
        if (concepten > 0)
        {
            return new(Afsluitbeletsel.Concepten, Concepten: concepten);
        }

        return stand.Rekeningen.Any(r => r.Rol == RekeningRol.Reserve) ? new(Afsluitbeletsel.Geen) : new(Afsluitbeletsel.GeenReserve);
    }

    /// <summary>De melding voor de gebruiker, of <c>null</c> als het jaar kan worden afgesloten.</summary>
    public string? Reden(int jaar) => Beletsel switch
    {
        Afsluitbeletsel.AlAfgesloten => $"Boekjaar {jaar.ToString(CultureInfo.InvariantCulture)} is al afgesloten.",
        Afsluitbeletsel.EerderJaarOpen => "Sluit eerst de eerdere boekjaren af.",
        Afsluitbeletsel.Concepten => string.Create(CultureInfo.CurrentCulture,
            $"Er {(Concepten == 1 ? "staat nog 1 conceptpost" : $"staan nog {Concepten} conceptposten")} in {jaar}. Boek of verwijder ze eerst."),
        Afsluitbeletsel.GeenReserve => "Wijs eerst een reserverekening aan (bijv. 600 Algemene reserve) bij Rekeningschema.",
        _ => null,
    };
}
