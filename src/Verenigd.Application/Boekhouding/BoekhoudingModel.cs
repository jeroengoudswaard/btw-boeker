using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Bank;
using Verenigd.Domain.Financien.Debiteuren;
using Verenigd.Domain.Financien.Facturen;
using Verenigd.Domain.Financien.Relaties;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.Application.Boekhouding;

/// <summary>De boekhouding van één administratie.</summary>
public sealed record BoekhoudingStand(
    AdministratieSoort Administratie,
    IReadOnlyList<Rubriek> Rubrieken,
    IReadOnlyList<Grootboekrekening> Rekeningen,
    IReadOnlyList<Dagboek> Dagboeken,
    IReadOnlyList<Boekjaar> Boekjaren,
    IReadOnlyList<Journaalpost> Posten)
{
    public IReadOnlyList<Bankafschrift> Bankafschriften { get; init; } = [];

    public IReadOnlyList<Bankmutatie> Bankmutaties { get; init; } = [];

    /// <summary>In volgorde: de eerste passende regel wint.</summary>
    public IReadOnlyList<Boekingsregel> Boekingsregels { get; init; } = [];

    /// <summary>Mutaties op contributievorderingen (stap 4c-1).</summary>
    public IReadOnlyList<Debiteurmutatie> Debiteurmutaties { get; init; } = [];

    /// <summary>De incassoruns (alleen-lezen), om bankontvangsten en storneringen te herkennen.</summary>
    public IReadOnlyList<Incassorun> Incassoruns { get; init; } = [];

    /// <summary>Aangevraagde herincasso's (alleen-lezen).</summary>
    public IReadOnlyList<Herincasso> Herincassos { get; init; } = [];

    /// <summary>Alle relaties; gedeeld door beide administraties (besluit 105).</summary>
    public IReadOnlyList<Relatie> Relaties { get; init; } = [];

    public IReadOnlyList<Verkoopfactuur> Verkoopfacturen { get; init; } = [];

    public Factuurinstellingen? Factuurinstellingen { get; init; }

    public bool IsIngericht => Rekeningen.Count > 0;

    /// <summary>De bankmutatie waaruit een post is ontstaan, of <c>null</c>.</summary>
    public Bankmutatie? MutatieVan(Guid postId) => Bankmutaties.FirstOrDefault(m => m.PostId == postId);

    public Boekjaar? Boekjaar(int jaar) => Boekjaren.FirstOrDefault(b => b.Jaar == jaar);
}

/// <summary>Wat een wijziging toevoegt en verwijdert; bestaande objecten zijn in de stand al bijgewerkt.</summary>
public sealed class BoekhoudingWijziging<T>(T waarde)
{
    public T Waarde { get; } = waarde;
    public List<object> Toevoegen { get; } = [];
    public List<object> Verwijderen { get; } = [];

    /// <summary>Voor waarden die pas na het opslaan bekend zijn, zoals een nieuw Id.</summary>
    public Func<T>? WaardeNaOpslaan { get; init; }

    /// <summary>Wat <see cref="IBoekhoudingRepository.PasToeAsync{T}"/> na het opslaan teruggeeft.</summary>
    public T Uitkomst() => WaardeNaOpslaan is { } waarde ? waarde() : Waarde;
}

public interface IBoekhoudingRepository
{
    Task<BoekhoudingStand> LaadAsync(AdministratieSoort administratie, CancellationToken annulering = default);

    /// <summary>Laadt de (gevolgde) stand, past <paramref name="wijziging"/> toe en slaat alles in één transactie op.</summary>
    Task<T> PasToeAsync<T>(AdministratieSoort administratie, Func<BoekhoudingStand, BoekhoudingWijziging<T>> wijziging,
        CancellationToken annulering = default);
}

public sealed record RegelInvoerId(int RekeningId, string? Omschrijving, Bedrag Debet, Bedrag Credit);

public sealed record PostInvoer(Guid? Id, int DagboekId, DateOnly Datum, string Omschrijving, IReadOnlyList<RegelInvoerId> Regels);

public sealed record RekeningInvoer(int? Id, int Nummer, string Naam, int? RubriekId, RekeningSoort Soort, RekeningRol Rol, bool IsActief);

public sealed record RubriekInvoer(int? Id, string Naam, int Volgorde);

/// <summary>Besluit 73: bewuste keuze om een verschil in de beginbalans op deze (balans)rekening te boeken.</summary>
public sealed record VerschilKeuze(int Nummer, string Naam)
{
    public static VerschilKeuze Standaard { get; } = new(1299, "Te onderzoeken verschillen");
}

// ---- Rapporten -------------------------------------------------------------------------------

public sealed record SaldiRegel(Grootboekrekening Rekening, Bedrag Debet, Bedrag Credit)
{
    public long Saldo => Debet.Centen - Credit.Centen;
}

public sealed record Saldibalans(int Jaar, IReadOnlyList<SaldiRegel> Regels)
{
    public Bedrag TotaalDebet => Regels.Select(r => r.Debet).Som();

    public Bedrag TotaalCredit => Regels.Select(r => r.Credit).Som();
}

public sealed record RubriekBedragen(string Rubriek, IReadOnlyList<(Grootboekrekening Rekening, Bedrag Bedrag)> Rekeningen)
{
    public Bedrag Totaal => Rekeningen.Select(r => r.Bedrag).Som();
}

public sealed record WinstEnVerlies(int Jaar, IReadOnlyList<RubriekBedragen> Baten, IReadOnlyList<RubriekBedragen> Lasten)
{
    public Bedrag TotaalBaten => Baten.Select(b => b.Totaal).Som();

    public Bedrag TotaalLasten => Lasten.Select(l => l.Totaal).Som();

    /// <summary>Positief is een overschot.</summary>
    public long Resultaat => TotaalBaten.Centen - TotaalLasten.Centen;
}

public sealed record Balans(int Jaar, IReadOnlyList<RubriekBedragen> Activa, IReadOnlyList<RubriekBedragen> Passiva, long ResultaatLopendJaar)
{
    public Bedrag TotaalActiva => Activa.Select(a => a.Totaal).Som();

    /// <summary>Inclusief het (nog niet naar de reserve geboekte) resultaat van het lopende jaar.</summary>
    public Bedrag TotaalPassiva => new(Passiva.Select(p => p.Totaal).Som().Centen + ResultaatLopendJaar);
}

public sealed record KaartMutatie(Guid PostId, DateOnly Datum, string Nummer, string Omschrijving, Bedrag Debet, Bedrag Credit, long LopendSaldo);

public sealed record Grootboekkaart(Grootboekrekening Rekening, DateOnly Van, DateOnly Tot, long Beginsaldo, IReadOnlyList<KaartMutatie> Mutaties)
{
    public long Eindsaldo => Mutaties.Count == 0 ? Beginsaldo : Mutaties[^1].LopendSaldo;
}

/// <summary>Een rapport als tabel, voor afdrukken en export (besluit 67). Cellen zijn tekst of <see cref="Bedrag"/>.</summary>
public sealed record RapportTabel(string Titel, string? Subtitel, IReadOnlyList<RapportKolom> Kolommen, IReadOnlyList<RapportRij> Rijen);

public sealed record RapportKolom(string Naam, bool IsBedrag);

public sealed record RapportRij(IReadOnlyList<object?> Cellen, bool IsKop, bool IsTotaal);
