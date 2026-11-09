namespace BtwBoeker.Domain.Financien.Relaties;

public sealed record RelatieGegevens(
    string Naam, string? Contactpersoon, string? Straat, string? Postcode, string? Plaats, string? Email, string? Telefoon, string? Iban,
    bool IsDebiteur, bool IsCrediteur, string? Notitie);

/// <summary>
/// Een debiteur of crediteur zonder lidnummer, zoals een huurder of leverancier (besluiten 36 en 108). Gedeeld door beide
/// administraties (besluit 105).
/// </summary>
public sealed class Relatie
{
    public const int MaximaleNaamLengte = 100;

    private Relatie()
    {
        Naam = null!;
    }

    public Relatie(RelatieGegevens gegevens)
    {
        Naam = null!;
        Id = Guid.NewGuid();
        IsActief = true;
        Wijzig(gegevens);
    }

    public Guid Id { get; private set; }
    public string Naam { get; private set; }
    public string? Contactpersoon { get; private set; }
    public string? Straat { get; private set; }
    public string? Postcode { get; private set; }
    public string? Plaats { get; private set; }
    public string? Email { get; private set; }
    public string? Telefoon { get; private set; }

    /// <summary>Zonder spaties, in hoofdletters (besluit 29).</summary>
    public string? Iban { get; private set; }

    public bool IsDebiteur { get; private set; }
    public bool IsCrediteur { get; private set; }
    public string? Notitie { get; private set; }
    public bool IsActief { get; private set; }

    /// <summary>De regels onder de naam op een factuur: contactpersoon, straat, postcode en plaats.</summary>
    public IReadOnlyList<string> Adresregels =>
        [.. new[] { Contactpersoon, Straat, string.Join("  ", new[] { Postcode, Plaats }.Where(d => !string.IsNullOrWhiteSpace(d))) }
            .Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r!)];

    /// <exception cref="ArgumentException">Geen naam, ongeldig IBAN of e-mailadres, of geen soort.</exception>
    public void Wijzig(RelatieGegevens gegevens)
    {
        ArgumentNullException.ThrowIfNull(gegevens);
        var naam = gegevens.Naam?.Trim();
        if (string.IsNullOrEmpty(naam) || naam.Length > MaximaleNaamLengte)
        {
            throw new ArgumentException($"De naam is verplicht en maximaal {MaximaleNaamLengte} tekens.", nameof(gegevens));
        }

        string? iban = null;
        if (Financien.Iban.TryMaak(gegevens.Iban, out var i))
        {
            iban = i.ControlegetalGeldig ? i.Waarde : throw new ArgumentException("Het IBAN is ongeldig (controlegetal klopt niet).", nameof(gegevens));
        }

        var email = Leeg(gegevens.Email);
        if (email is not null && (!email.Contains('@', StringComparison.Ordinal) || email.Contains(' ', StringComparison.Ordinal)))
        {
            throw new ArgumentException("Het e-mailadres is ongeldig.", nameof(gegevens));
        }

        if (!gegevens.IsDebiteur && !gegevens.IsCrediteur)
        {
            throw new ArgumentException("Een relatie is debiteur, crediteur of allebei.", nameof(gegevens));
        }

        Naam = naam;
        Contactpersoon = Leeg(gegevens.Contactpersoon);
        Straat = Leeg(gegevens.Straat);
        Postcode = Leeg(gegevens.Postcode)?.ToUpperInvariant();
        Plaats = Leeg(gegevens.Plaats);
        Email = email;
        Telefoon = Leeg(gegevens.Telefoon);
        Iban = iban;
        IsDebiteur = gegevens.IsDebiteur;
        IsCrediteur = gegevens.IsCrediteur;
        Notitie = Leeg(gegevens.Notitie);
    }

    public void Activeer() => IsActief = true;

    public void Deactiveer() => IsActief = false;

    public override string ToString() => Naam;

    private static string? Leeg(string? tekst) => string.IsNullOrWhiteSpace(tekst) ? null : tekst.Trim();
}
