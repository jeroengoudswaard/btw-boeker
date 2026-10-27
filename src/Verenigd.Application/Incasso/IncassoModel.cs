using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Debiteuren;
using Verenigd.Domain.Gedeeld;
using Verenigd.Domain.Leden;

namespace Verenigd.Application.Incasso;

/// <summary>
/// Alles wat contributie en incasso nodig hebben. Dit is (naast de SOL3-import) de enige plek die ledengegevens en
/// financiële gegevens samen leest; de koppeling is het lidnummer (besluit 36).
/// </summary>
public sealed record IncassoStand(
    IReadOnlyList<Persoon> Personen,
    IReadOnlyList<Contributiecategorie> Categorieen,
    IReadOnlyList<Tarief> Tarieven,
    IReadOnlyList<LidCategorie> LidCategorieen,
    IReadOnlyList<Betaler> Betalers,
    IReadOnlyList<BetalerLid> Koppelingen,
    IReadOnlyList<Incassorun> Runs,
    Incassoinstellingen? Instellingen)
{
    /// <summary>Gestorneerde posten die in de volgende run opnieuw worden meegenomen (besluit 92).</summary>
    public IReadOnlyList<Herincasso> Herincassos { get; init; } = [];
}

/// <summary>Eén lid × één maand in een voorstel.</summary>
public sealed record VoorstelRegel(
    Lidnummer Lidnummer, string Naam, string Voornaam, Maand Maand, string Categorie, Bedrag Bedrag, bool IsNaheffing);

/// <summary>Eén incasso bij één betaler (besluit 46).</summary>
public sealed record VoorstelPost(Betaler Betaler, string Rekeninghouder, string Omschrijving, IReadOnlyList<VoorstelRegel> Regels)
{
    /// <summary>Bij een herincasso (besluit 100): het EndToEndId van de gestorneerde post.</summary>
    public string? HerincassoVan { get; init; }

    public Bedrag Bedrag => Regels.Select(r => r.Bedrag).Som();
}

/// <summary>Een lid dat niet via de incasso kan worden geïnd (besluit 49). <see cref="Bedrag"/> is onbekend zonder categorie of tarief.</summary>
public sealed record ZelfTeInnenRegel(Lidnummer Lidnummer, string Naam, string Maanden, Bedrag? Bedrag, string Reden);

/// <summary>Het resultaat van een berekening; er is dan nog niets opgeslagen.</summary>
public sealed record IncassoVoorstel(
    Maand Maand,
    DateOnly Uitvoerdatum,
    IReadOnlyList<VoorstelPost> Posten,
    IReadOnlyList<ZelfTeInnenRegel> ZelfTeInnen,
    IReadOnlyList<string> Meldingen)
{
    public Bedrag Totaal => Posten.Select(p => p.Bedrag).Som();

    public int AantalPosten => Posten.Count;

    /// <summary>Er gaat niets naar de bank.</summary>
    public bool IsLeeg => Posten.Count == 0;
}

public sealed record IncassoBestand(string Bestandsnaam, byte[] Inhoud);

public sealed record LidCategorieInvoer(Lidnummer Lidnummer, int CategorieId, DateOnly Ingangsdatum);

public sealed record BetalerInvoer(
    Guid? Id,
    string Iban,
    string? Rekeninghouder,
    string? Mandaatkenmerk,
    DateOnly? OndertekendOp,
    DateOnly? IngetrokkenOp,
    IReadOnlyList<Lidnummer> Leden)
{
    /// <summary>Invoerfouten in begrijpelijke taal; leeg als alles klopt.</summary>
    public IReadOnlyList<string> Fouten(IEnumerable<Betaler> bestaand)
    {
        ArgumentNullException.ThrowIfNull(bestaand);
        var fouten = new List<string>();
        if (!Domain.Financien.Iban.TryMaak(Iban, out var iban))
        {
            fouten.Add("Vul een IBAN in.");
        }
        else
        {
            if (!iban.ControlegetalGeldig)
            {
                fouten.Add("Het IBAN is ongeldig (controlegetal klopt niet).");
            }

            if (bestaand.Any(b => b.Iban == iban && b.Id != Id))
            {
                fouten.Add("Er bestaat al een betaler met dit IBAN.");
            }
        }

        var heeftKenmerk = !string.IsNullOrWhiteSpace(Mandaatkenmerk);
        if (heeftKenmerk && !SepaTekst.IsGeldigKenmerk(Mandaatkenmerk!.Trim()))
        {
            fouten.Add("Het mandaatkenmerk heeft 1–35 tekens (letters, cijfers en / - ? : ( ) . , ' +), zonder spaties.");
        }

        if (heeftKenmerk != OndertekendOp.HasValue)
        {
            fouten.Add("Vul bij een machtiging zowel het kenmerk als de ondertekeningsdatum in.");
        }

        if (IngetrokkenOp is not null && (!heeftKenmerk || IngetrokkenOp < OndertekendOp))
        {
            fouten.Add("Een intrekking kan alleen na de ondertekening van een machtiging.");
        }

        return fouten;
    }
}

public sealed record IncassoinstellingenInvoer(
    string IncassantId, string Naam, string Iban, int Incassodag, Maand EersteIncassomaand, string Omschrijvingssjabloon);

/// <summary>Opslag van contributie en incasso (financiële gegevens, besluit 35).</summary>
public interface IContributieRepository
{
    Task<IncassoStand> LaadAsync(CancellationToken annulering = default);

    Task VoegCategorieenToeAsync(IReadOnlyList<Contributiecategorie> categorieen, CancellationToken annulering = default);

    /// <summary>Maandbedragen per categorie-ID voor één boekjaar; ontbrekende tarieven worden aangemaakt.</summary>
    Task BewaarTarievenAsync(int boekjaar, IReadOnlyDictionary<int, Bedrag> bedragen, CancellationToken annulering = default);

    /// <summary>Voegt categorieën met ingangsdatum toe; een bestaande rij met dezelfde datum wordt vervangen.</summary>
    Task BewaarLidCategorieenAsync(IReadOnlyList<LidCategorieInvoer> invoer, CancellationToken annulering = default);

    /// <exception cref="ArgumentException">De invoer bevat fouten (zie <see cref="BetalerInvoer.Fouten"/>).</exception>
    Task<Guid> BewaarBetalerAsync(BetalerInvoer invoer, CancellationToken annulering = default);

    /// <exception cref="ArgumentException">Ongeldig incassant-ID, IBAN of andere waarde.</exception>
    Task BewaarInstellingenAsync(IncassoinstellingenInvoer invoer, CancellationToken annulering = default);

    /// <summary>Laadt de stand, laat <paramref name="maakRun"/> een run maken en slaat die in één transactie op.</summary>
    Task BewaarRunAsync(Func<IncassoStand, Incassorun> maakRun, CancellationToken annulering = default);

    Task LegBestandVastAsync(Guid runId, string hash, CancellationToken annulering = default);

    /// <summary>Annuleert de run; herincasso's die erin zaten wachten weer op de volgende run.</summary>
    Task AnnuleerRunAsync(Guid runId, DateTime nuUtc, CancellationToken annulering = default);

    Task VraagHerincassoAanAsync(Herincasso herincasso, CancellationToken annulering = default);

    /// <summary>Per categorie-ID de opbrengstrekening (grootboekrekening-ID), of <c>null</c> om de koppeling te wissen.</summary>
    Task BewaarOpbrengstrekeningenAsync(IReadOnlyDictionary<int, int?> perCategorie, CancellationToken annulering = default);
}
