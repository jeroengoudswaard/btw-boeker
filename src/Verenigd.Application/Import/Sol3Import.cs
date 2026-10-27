using Verenigd.Domain.Financien;
using Verenigd.Domain.Gedeeld;
using Verenigd.Domain.Leden;

namespace Verenigd.Application.Import;

/// <summary>Instellingen van de import.</summary>
public sealed record Sol3ImportOpties
{
    /// <summary>
    /// Reguliere expressies (hoofdletterongevoelig) voor voogdnamen die betekenen dat de naam onbekend is,
    /// zoals "Voogd 1", "Voogd 2" of "Voogd Jansen" (besluit 31).
    /// </summary>
    public IReadOnlyList<string> VoogdPlaatsvervangers { get; init; } = [@"^voogd(\s+\d+)?$", @"^voogd\s+\S.*$"];
}

/// <summary>Alles wat SOL3 over één lid levert, samengevoegd uit de vier exports.</summary>
public sealed record ImportLid(
    Lidnummer Lidnummer,
    LidGegevens Gegevens,
    IReadOnlyList<VoogdGegevens> Voogden,
    IReadOnlyList<FunctieGegevens> Functies,
    Iban? Iban,
    string? Rekeninghouder);

/// <summary>
/// Een waarschuwing voor de gebruiker. Mag het lidnummer noemen (dat is nodig om het te controleren),
/// maar nooit IBAN's, e-mailadressen of andere veldwaarden; wordt niet gelogd.
/// </summary>
public sealed record Importmelding(Sol3Export? Export, int? Regel, string? Lidnummer, string Tekst);

/// <summary>Het ingelezen en gecontroleerde resultaat van de vier exports.</summary>
public sealed record Sol3Import(IReadOnlyList<ImportLid> Leden, IReadOnlyList<Importmelding> Meldingen);
