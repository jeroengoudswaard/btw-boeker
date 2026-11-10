using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Application.Boekhouding;

/// <summary>
/// Eén rekening om een lege administratie mee in te richten, met het beginsaldo voor de openingsbalans. Komt uit een
/// sjabloon of (stap 2b) een XAF-bestand.
/// </summary>
public sealed record Inrichtrekening(
    int Nummer, string Naam, string? Rubriek, RekeningSoort Soort, RekeningRol Rol, bool Overslaan, Bedrag Debet, Bedrag Credit)
{
    /// <summary>Baten of lasten volgens de bron; <c>null</c> = de standaard (vanaf 8000 baten).</summary>
    public ResultaatKant? Kant { get; init; }

    /// <summary>De kant uit de bron, alleen bij een resultaatrekening.</summary>
    public ResultaatKant? VoorgesteldeKant => Soort == RekeningSoort.Resultaat ? Kant : null;
}

/// <summary>
/// Een volledig voorstel voor het inrichten van een lege administratie: rekeningen met beginsaldi per 1 januari van
/// <see cref="Beginjaar"/>. Zie <see cref="BoekhoudingDienst.ImporteerAsync"/>.
/// </summary>
/// <param name="Jaar">Het jaar vóór het beginjaar: de beginsaldi zijn de eindstand van dat jaar.</param>
public sealed record Inrichtvoorstel(int Jaar, IReadOnlyList<Inrichtrekening> Rekeningen, IReadOnlyList<string> Meldingen)
{
    public Bedrag TotaalDebet => Rekeningen.Where(r => !r.Overslaan).Select(r => r.Debet).Som();

    public Bedrag TotaalCredit => Rekeningen.Where(r => !r.Overslaan).Select(r => r.Credit).Som();

    public bool IsInEvenwicht => TotaalDebet == TotaalCredit;

    /// <summary>Het eerste boekjaar van de administratie; de beginsaldi gelden op 1 januari van dit jaar.</summary>
    public int Beginjaar => Jaar + 1;

    /// <summary>Komt in de omschrijving van de openingsbalans, bijvoorbeeld "Openingsbalans 2026 (standaardinrichting)".</summary>
    public string Herkomst { get; init; } = "ingericht";

    /// <summary>Ook het verkoopdagboek VRK aanmaken.</summary>
    public bool MetVerkoopboek { get; init; }
}
