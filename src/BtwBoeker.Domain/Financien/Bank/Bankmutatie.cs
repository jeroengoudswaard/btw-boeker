using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Domain.Financien.Bank;

/// <summary>Een dagafschrift uit CAMT.053, bewaard voor de controle dat de saldi op elkaar aansluiten.</summary>
public sealed class Bankafschrift
{
    private Bankafschrift()
    {
        Dagboek = null!;
        Nummer = null!;
    }

    public Bankafschrift(AdministratieSoort administratie, Dagboek dagboek, string nummer, DateOnly datum, long beginsaldo, long eindsaldo)
    {
        ArgumentNullException.ThrowIfNull(dagboek);
        ArgumentException.ThrowIfNullOrWhiteSpace(nummer);
        Administratie = administratie;
        Dagboek = dagboek;
        Nummer = nummer.Trim();
        Datum = datum;
        Beginsaldo = beginsaldo;
        Eindsaldo = eindsaldo;
    }

    public int Id { get; private set; }
    public AdministratieSoort Administratie { get; private set; }
    public Dagboek Dagboek { get; private set; }
    public string Nummer { get; private set; }
    public DateOnly Datum { get; private set; }

    /// <summary>In centen; positief is een tegoed.</summary>
    public long Beginsaldo { get; private set; }

    public long Eindsaldo { get; private set; }
}

/// <summary>
/// Eén transactie van de bank. De koppeling met een journaalpost bepaalt de status: zonder post staat de
/// transactie in de werkvoorraad, anders is ze concept of geboekt.
/// </summary>
public sealed class Bankmutatie
{
    public const int MaximaleTekstLengte = 500;

    private Bankmutatie()
    {
        Dagboek = null!;
        Sleutel = null!;
    }

    public Bankmutatie(
        AdministratieSoort administratie, Dagboek dagboek, string sleutel, DateOnly boekdatum, Bedrag bedrag, bool isBij,
        BankTransactiecode code, string? tegenIban, string? tegennaam, string? omschrijving, string? endToEndId, string? mandaatId,
        int? batchAantal, string? batchKenmerk = null, string? retourreden = null)
    {
        ArgumentNullException.ThrowIfNull(dagboek);
        ArgumentException.ThrowIfNullOrWhiteSpace(sleutel);
        if (dagboek.Soort != DagboekSoort.Bank || dagboek.Rekening is null)
        {
            throw new ArgumentException("Een bankmutatie hoort bij een bankdagboek.", nameof(dagboek));
        }

        if (bedrag.Centen < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bedrag), "Het bedrag is altijd positief; bij of af staat apart.");
        }

        Id = Guid.NewGuid();
        Administratie = administratie;
        Dagboek = dagboek;
        Sleutel = sleutel;
        Boekdatum = boekdatum;
        Bedrag = bedrag;
        IsBij = isBij;
        Code = code;
        TegenIban = Iban.TryMaak(tegenIban, out var iban) ? iban.Waarde : null;
        Tegennaam = Kort(tegennaam);
        Omschrijving = Kort(omschrijving);
        EndToEndId = Kort(endToEndId);
        MandaatId = Kort(mandaatId);
        BatchAantal = batchAantal;
        BatchKenmerk = Kort(batchKenmerk);
        Retourreden = Kort(retourreden);
    }

    public Guid Id { get; private set; }
    public AdministratieSoort Administratie { get; private set; }
    public Dagboek Dagboek { get; private set; }

    /// <summary>Uniek per administratie; voorkomt dat een transactie twee keer wordt ingelezen.</summary>
    public string Sleutel { get; private set; }

    public DateOnly Boekdatum { get; private set; }
    public Bedrag Bedrag { get; private set; }
    public bool IsBij { get; private set; }
    public BankTransactiecode Code { get; private set; }
    public string? TegenIban { get; private set; }
    public string? Tegennaam { get; private set; }
    public string? Omschrijving { get; private set; }
    public string? EndToEndId { get; private set; }
    public string? MandaatId { get; private set; }

    /// <summary>Het aantal posten van een batch (bijvoorbeeld de eigen incasso), die als één regel op het afschrift staat.</summary>
    public int? BatchAantal { get; private set; }

    /// <summary>Het batchkenmerk (<c>PmtInfId</c>); bij een eigen incasso het kenmerk van de run plus <c>-1</c>.</summary>
    public string? BatchKenmerk { get; private set; }

    /// <summary>De SEPA-redencode van een stornering, zoals <c>AM04</c>.</summary>
    public string? Retourreden { get; private set; }

    public Guid? PostId { get; private set; }

    /// <summary>In centen, met teken: positief is bij.</summary>
    public long Saldo => IsBij ? Bedrag.Centen : -Bedrag.Centen;

    public string Standaardomschrijving
    {
        get
        {
            var delen = new[] { Tegennaam, Omschrijving }.Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
            var tekst = delen.Count > 0 ? string.Join(" · ", delen)
                : BatchAantal is { } n ? $"Incasso ({n} posten)"
                : Code.Omschrijving;
            return tekst.Length > Journaalpost.MaximaleOmschrijvingLengte ? tekst[..Journaalpost.MaximaleOmschrijvingLengte] : tekst;
        }
    }

    /// <summary>
    /// Een conceptpost met de vaste bankregel en één tegenregel. Met een btw-code (besluit 50) is het bankbedrag inclusief
    /// btw: de tegenregel krijgt de grondslag en direct erna komt het btw-bedrag op <paramref name="btwrekening"/>.
    /// </summary>
    public Journaalpost MaakPost(Grootboekrekening tegenrekening, string? omschrijving, Btw.BtwCode? btw = null, Grootboekrekening? btwrekening = null)
    {
        ArgumentNullException.ThrowIfNull(tegenrekening);
        var tekst = string.IsNullOrWhiteSpace(omschrijving) ? Standaardomschrijving : omschrijving.Trim();
        var post = new Journaalpost(Administratie, Dagboek, Boekdatum, tekst);
        var bank = Dagboek.Rekening!;
        var regels = new List<RegelInvoer> { Kant(bank, Bedrag, bankzijde: true, null) };
        if (btw is { HeeftBtw: true, Percentage: > 0 } code)
        {
            ArgumentNullException.ThrowIfNull(btwrekening);
            var bedragen = Btw.BtwBerekening.Bereken(Bedrag, code.Percentage, bedragInclusiefBtw: true);
            regels.Add(Kant(tegenrekening, bedragen.Grondslag, bankzijde: false, code));
            regels.Add(Kant(btwrekening, bedragen.Btw, bankzijde: false, code));
        }
        else
        {
            regels.Add(Kant(tegenrekening, Bedrag, bankzijde: false, btw is { HeeftBtw: true } nul ? nul : null));
        }

        post.Wijzig(Dagboek, Boekdatum, tekst, regels);
        return post;
    }

    private RegelInvoer Kant(Grootboekrekening rekening, Bedrag bedrag, bool bankzijde, Btw.BtwCode? code) =>
        IsBij == bankzijde
            ? new RegelInvoer(rekening, null, bedrag, Bedrag.Nul, code)
            : new RegelInvoer(rekening, null, Bedrag.Nul, bedrag, code);

    /// <summary>De bankregel staat vast: de regels op de bankrekening moeten samen het bedrag van de transactie zijn.</summary>
    /// <returns>Een foutmelding, of <c>null</c> als het klopt.</returns>
    public string? ControleerBankregel(Journaalpost post)
    {
        ArgumentNullException.ThrowIfNull(post);
        var bank = Dagboek.Rekening!;
        var saldo = post.Regels.Where(r => ReferenceEquals(r.Rekening, bank) || (bank.Id != 0 && r.Rekening.Id == bank.Id)).Sum(r => r.Saldo);
        return saldo == Saldo
            ? null
            : $"De bankregel moet {(IsBij ? "debet" : "credit")} {Bedrag} op rekening {bank} zijn, zoals op het afschrift.";
    }

    public void Koppel(Journaalpost post)
    {
        ArgumentNullException.ThrowIfNull(post);
        PostId = post.Id;
    }

    public void Ontkoppel() => PostId = null;

    /// <summary>Vult velden aan die bij een eerdere import nog niet werden bewaard; bestaande waarden blijven staan.</summary>
    public void Verrijk(string? batchKenmerk, string? retourreden)
    {
        BatchKenmerk ??= Kort(batchKenmerk);
        Retourreden ??= Kort(retourreden);
    }

    private static string? Kort(string? tekst)
    {
        var schoon = tekst?.Trim();
        return string.IsNullOrEmpty(schoon) ? null : schoon.Length > MaximaleTekstLengte ? schoon[..MaximaleTekstLengte] : schoon;
    }
}
