using Verenigd.Application.Boekhouding;
using Verenigd.Application.Debiteuren;
using Verenigd.Application.Facturen;
using Verenigd.Domain.Financien.Bank;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.Application.Bank;

/// <summary>De rekening voor een banktransactie, met de omschrijving van de post en waar het voorstel vandaan komt.</summary>
public sealed record Herkenning(Grootboekrekening Rekening, string? Omschrijving, string Bron)
{
    /// <summary>Bij contributie: de mutaties op vorderingen die met de post meegaan (stap 4c-1).</summary>
    public IReadOnlyList<Afletterregel> Aflettering { get; init; } = [];
}

public static class Bankherkenning
{
    /// <summary>
    /// Eerst contributie (eigen incassobatch, stornering, betaling van een open post) op 1300 Debiteuren contributie; dan
    /// een overboeking tussen eigen rekeningen via de rekening met de rol Kruisposten (besluit 70); anders wint de eerste
    /// actieve boekingsregel die past (besluit 78). <c>null</c>: de transactie blijft in de werkvoorraad.
    /// </summary>
    public static Herkenning? Bepaal(BoekhoudingStand stand, Bankmutatie mutatie)
    {
        ArgumentNullException.ThrowIfNull(stand);
        ArgumentNullException.ThrowIfNull(mutatie);
        if (stand.Rekeningen.FirstOrDefault(r => r.Nummer == Debiteurenrekeningen.Contributie && r.IsActief) is { } debiteuren
            && Contributievorderingen.Herken(stand, mutatie) is { } contributie)
        {
            return new Herkenning(debiteuren, contributie.Omschrijving, "Contributie") { Aflettering = contributie.Regels };
        }

        if (stand.Rekeningen.FirstOrDefault(r => r.Nummer == Debiteurenrekeningen.Overig && r.IsActief) is { } overig
            && Factuuroverzicht.Herken(stand, mutatie) is { } factuur)
        {
            return new Herkenning(overig, factuur.Omschrijving, "Factuur") { Aflettering = factuur.Regels };
        }

        if (mutatie.TegenIban is { } tegen
            && stand.Dagboeken.FirstOrDefault(d => d.Soort == DagboekSoort.Bank && d.Iban?.Waarde == tegen && !ReferenceEquals(d, mutatie.Dagboek)) is { } eigen
            && stand.Rekeningen.FirstOrDefault(r => r.Rol == RekeningRol.Kruisposten && r.IsActief) is { } kruisposten)
        {
            return new Herkenning(kruisposten, $"Overboeking {(mutatie.IsBij ? "van" : "naar")} {eigen.Code}", "Eigen rekening");
        }

        var regel = stand.Boekingsregels
            .OrderBy(r => r.Volgorde).ThenBy(r => r.Id)
            .FirstOrDefault(r => r.Rekening.IsActief && r.Past(mutatie));
        return regel is null ? null : new Herkenning(regel.Rekening, regel.Omschrijving, regel.Naam);
    }
}
