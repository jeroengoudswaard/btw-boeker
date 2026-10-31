using Verenigd.Application.Bank;
using Verenigd.Application.Begrotingen;
using Verenigd.Application.Boekhouding;
using Verenigd.Application.Debiteuren;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.Application.Inrichting;

/// <summary>Het saldo van één bankrekening: volgens het laatste afschrift, of zonder afschrift volgens het grootboek.</summary>
public sealed record Rekeningstand(string Naam, Bedrag Saldo, DateOnly? Datum);

/// <summary>
/// De kerncijfers op Start (besluit 217): de bankrekeningen, de openstaande contributie (alleen de Vereniging) en het
/// resultaat van het lopende jaar tot vandaag, met de begroting naar rato als die er is.
/// </summary>
public sealed record Kerncijfers(
    IReadOnlyList<Rekeningstand> Bankrekeningen, Bedrag? OpenstaandeContributie, int Storneringen, Bedrag Resultaat, Bedrag? BegrootNaarRato,
    bool HeeftBoekhouding)
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

        Bedrag? openstaand = null;
        var storneringen = 0;
        if (stand.Administratie == AdministratieSoort.Vereniging)
        {
            var open = Contributievorderingen.Bereken(stand).Where(v => v.IsOpen).ToList();
            openstaand = new Bedrag(open.Sum(v => v.Saldo));
            storneringen = open.Count;
        }

        // Debet positief: baten staan credit, dus het resultaat (positief = overschot) is het omgekeerde van de som.
        var resultaat = -stand.Rekeningen.Where(r => r.Soort == RekeningSoort.Resultaat).Sum(r => Grootboek.Resultaat(stand, r, vandaag.Year, vandaag));
        var overzicht = Begrotingsrekenaar.Overzicht(stand, vandaag.Year, vandaag);
        Bedrag? naarRato = overzicht.Begroting is null
            ? null
            : new Bedrag(overzicht.Totaal(ResultaatKant.Baten, r => r.NaarRato).Centen - overzicht.Totaal(ResultaatKant.Lasten, r => r.NaarRato).Centen);

        return new Kerncijfers(bank, openstaand, storneringen, new Bedrag(resultaat), naarRato, stand.Rekeningen.Count > 0 || stand.Dagboeken.Count > 0);
    }
}
