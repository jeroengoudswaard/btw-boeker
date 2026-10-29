using Verenigd.Domain.Financien;
using Verenigd.Domain.Gedeeld;
using Verenigd.Domain.Leden;

namespace Verenigd.Application.Import;

/// <summary>De huidige inhoud van de database, voor zover de import die nodig heeft.</summary>
public sealed record ImportStand(
    IReadOnlyList<Persoon> Personen,
    IReadOnlyList<Groep> Groepen,
    IReadOnlyList<Betaler> Betalers,
    IReadOnlyList<BetalerLid> Koppelingen)
{
    public static ImportStand Leeg { get; } = new([], [], [], []);
}

/// <summary>Eén lid in het voorbeeldoverzicht, met de verschillen per veld.</summary>
public sealed record ImportRegel(Lidnummer Lidnummer, string Naam, IReadOnlyList<Veldwijziging> Wijzigingen);

/// <summary>Een groep die nog niet bestaat, met het voorgestelde soort (besluit 43).</summary>
public sealed record NieuweGroep(string Naam, GroepSoort VoorgesteldeSoort);

/// <summary>Wat de import zou doen; er is dan nog niets gewijzigd.</summary>
public sealed record ImportVoorstel(
    IReadOnlyList<ImportRegel> Nieuw,
    IReadOnlyList<ImportRegel> Gewijzigd,
    IReadOnlyList<ImportRegel> Uitgeschreven,
    IReadOnlyList<NieuweGroep> NieuweGroepen,
    IReadOnlyList<Importmelding> Meldingen)
{
    public bool IsLeeg => Nieuw.Count == 0 && Gewijzigd.Count == 0 && Uitgeschreven.Count == 0 && NieuweGroepen.Count == 0;

    /// <summary>Leden waarvan SOL3 een ander IBAN levert dan hun betaler in Verenigd (besluit 141).</summary>
    public IReadOnlyList<IbanVerschil> IbanVerschillen { get; init; } = [];

    /// <summary>Alleen aantallen; geschikt voor het logboek.</summary>
    public string Samenvatting =>
        $"{Nieuw.Count} nieuw, {Gewijzigd.Count} gewijzigd, {Uitgeschreven.Count} uitgeschreven, {Meldingen.Count} waarschuwingen";
}

/// <summary>De keuzes van de gebruiker in de importwizard.</summary>
/// <summary>Een lid met een betaler in Verenigd waarvoor SOL3 een ander IBAN levert (besluiten 22 en 141).</summary>
public sealed record IbanVerschil(Lidnummer Lidnummer, string Naam, Iban Huidig, Iban Sol3);

public sealed record ImportKeuzes(IReadOnlyDictionary<string, GroepSoort> GroepSoorten, IReadOnlySet<Lidnummer> Uitschrijven)
{
    /// <summary>Besluit 141: alleen bij deze leden het afwijkende IBAN uit SOL3 overnemen; standaard geen.</summary>
    public IReadOnlySet<Lidnummer> IbanOvernemen { get; init; } = new HashSet<Lidnummer>();

    /// <summary>Voorgestelde groepsoorten en alle verdwenen leden uitschrijven.</summary>
    public static ImportKeuzes Standaard(ImportVoorstel voorstel)
    {
        ArgumentNullException.ThrowIfNull(voorstel);
        return new ImportKeuzes(
            voorstel.NieuweGroepen.ToDictionary(g => g.Naam, g => g.VoorgesteldeSoort, StringComparer.Ordinal),
            voorstel.Uitgeschreven.Select(r => r.Lidnummer).ToHashSet());
    }
}

/// <summary>Nieuwe objecten die aan de database moeten worden toegevoegd; bestaande zijn al bijgewerkt.</summary>
public sealed record ImportWijzigingen(
    IReadOnlyList<Persoon> NieuwePersonen,
    IReadOnlyList<Groep> NieuweGroepen,
    IReadOnlyList<Betaler> NieuweBetalers,
    IReadOnlyList<BetalerLid> NieuweKoppelingen,
    int Gewijzigd,
    int Uitgeschreven);

/// <summary>Lezen en in één transactie bijwerken van de gegevens die de import raakt.</summary>
public interface ILedenImportOpslag
{
    Task<ImportStand> LaadAsync(CancellationToken annulering = default);

    /// <summary>
    /// Laadt de stand, laat <paramref name="wijziging"/> die bijwerken en slaat alles in één transactie op.
    /// Mislukt er iets, dan blijft de database ongewijzigd.
    /// </summary>
    Task PasToeAsync(Func<ImportStand, ImportWijzigingen> wijziging, CancellationToken annulering = default);
}
