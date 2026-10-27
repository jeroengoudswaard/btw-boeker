using Verenigd.Application.Boekhouding;
using Verenigd.Application.Debiteuren;
using Verenigd.Domain.Financien.Bank;
using Verenigd.Domain.Financien.Debiteuren;
using Verenigd.Domain.Financien.Facturen;
using Verenigd.Domain.Financien.Relaties;

namespace Verenigd.Application.Facturen;

/// <summary>De stand van een definitieve factuur: saldo uit de effectieve debiteurmutaties op het factuurnummer.</summary>
public sealed record Factuurstand(Verkoopfactuur Factuur, Relatie? Relatie, long Saldo, bool IsVervallen, bool IsGecrediteerd)
{
    public string Statustekst =>
        !Factuur.IsDefinitief ? "Concept"
        : Factuur.IsCreditfactuur ? "Creditfactuur"
        : IsGecrediteerd && Saldo == 0 ? "Gecrediteerd"
        : Saldo <= 0 ? "Betaald"
        : IsVervallen ? "Vervallen"
        : "Open";

    public bool IsOpen => Factuur.IsDefinitief && !Factuur.IsCreditfactuur && Saldo > 0;
}

/// <summary>Openstaande verkoopfacturen en de herkenning van betalingen (stap 4c-2).</summary>
public static class Factuuroverzicht
{
    /// <summary>Alle facturen van de administratie, met saldo; concepten hebben saldo 0.</summary>
    public static IReadOnlyList<Factuurstand> Bereken(BoekhoudingStand stand, DateOnly vandaag)
    {
        ArgumentNullException.ThrowIfNull(stand);
        var mutaties = Contributievorderingen.Effectief(stand).ToLookup(m => m.Vordering, StringComparer.Ordinal);
        var relaties = stand.Relaties.ToDictionary(r => r.Id);
        return [.. stand.Verkoopfacturen
            .Where(f => f.Administratie == stand.Administratie)
            .Select(f =>
            {
                var eigen = f.Nummer is null ? [] : mutaties[f.Nummer].ToList();
                var saldo = eigen.Sum(m => m.Saldo);
                var open = f.IsDefinitief && !f.IsCreditfactuur && saldo > 0;
                return new Factuurstand(f, relaties.GetValueOrDefault(f.RelatieId), saldo, open && f.Vervaldatum < vandaag,
                    eigen.Exists(m => m.Soort == DebiteurmutatieSoort.Creditering));
            })
            .OrderByDescending(s => s.Factuur.Factuurdatum).ThenByDescending(s => s.Factuur.Nummer, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Een ontvangst met het factuurnummer in de omschrijving en precies het open bedrag, of van het IBAN van de relatie met
    /// het bedrag van precies één open factuur van die relatie.
    /// </summary>
    internal static (string Omschrijving, IReadOnlyList<Afletterregel> Regels)? Herken(BoekhoudingStand stand, Bankmutatie mutatie)
    {
        if (!mutatie.IsBij)
        {
            return null;
        }

        var open = Bereken(stand, DateOnly.MinValue).Where(f => f.IsOpen && f.Saldo == mutatie.Bedrag.Centen).ToList();
        var opNummer = mutatie.Omschrijving is { } tekst
            ? open.Where(f => tekst.Contains(f.Factuur.Nummer!, StringComparison.OrdinalIgnoreCase)).ToList()
            : [];
        var opIban = mutatie.TegenIban is { } iban ? open.Where(f => f.Relatie?.Iban == iban).ToList() : [];
        var gevonden = opNummer.Count == 1 ? opNummer[0] : opNummer.Count == 0 && opIban.Count == 1 ? opIban[0] : null;
        if (gevonden is null)
        {
            return null;
        }

        return ($"Betaling factuur {gevonden.Factuur.Nummer}: {gevonden.Relatie?.Naam}",
            [new Afletterregel(gevonden.Factuur.Nummer!, gevonden.Factuur.RelatieId, null, DebiteurmutatieSoort.Betaling, -gevonden.Saldo, null)]);
    }
}

/// <summary>Maakt de PDF van een verkoopfactuur (besluiten 95 en 110); de uitvoering zit in de infrastructuur.</summary>
public interface IFactuurPdfMaker
{
    byte[] Maak(Verkoopfactuur factuur, Factuurinstellingen instellingen);
}
