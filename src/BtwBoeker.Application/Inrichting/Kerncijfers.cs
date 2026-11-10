using BtwBoeker.Application.Bank;
using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Application.Inrichting;

/// <summary>Het saldo van één bankrekening: volgens het laatste afschrift, of zonder afschrift volgens het grootboek.</summary>
public sealed record Rekeningstand(string Naam, Bedrag Saldo, DateOnly? Datum);

/// <summary>
/// De kerncijfers op Start: bankrekeningen en het resultaat van het lopende jaar tot vandaag.
/// </summary>
public sealed record Kerncijfers(IReadOnlyList<Rekeningstand> Bankrekeningen, Bedrag Resultaat, bool HeeftBoekhouding)
{
    public static Kerncijfers Bepaal(BoekhoudingStand stand, DateOnly vandaag)
    {
        ArgumentNullException.ThrowIfNull(stand);
        var bank = stand.Dagboeken
            .Where(d => d.Soort == DagboekSoort.Bank && d.Rekening is not null)
            .Select(d =>
            {
                var saldi = Bankoverzicht.Saldi(stand, d);
                return new Rekeningstand(d.Naam, new Bedrag(saldi.Bank ?? saldi.Grootboek), saldi.Datum);
            })
            .ToList();

        // Debet positief: baten staan credit, dus het resultaat (positief = overschot) is het omgekeerde van de som.
        var resultaat = -stand.Rekeningen.Where(r => r.Soort == RekeningSoort.Resultaat).Sum(r => Grootboek.Resultaat(stand, r, vandaag.Year, vandaag));

        return new Kerncijfers(bank, new Bedrag(resultaat), stand.Rekeningen.Count > 0 || stand.Dagboeken.Count > 0);
    }
}
