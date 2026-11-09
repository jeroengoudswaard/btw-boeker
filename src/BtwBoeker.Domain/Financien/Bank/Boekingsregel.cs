using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Domain.Financien.Bank;

public enum Richting
{
    Beide = 0,
    Bij = 1,
    Af = 2,
}

/// <summary>Voorwaarden van een boekingsregel (besluit 78); een lege voorwaarde doet niet mee.</summary>
public sealed record BoekingsregelVoorwaarden(
    string? TegenIban = null, string? NaamBevat = null, string? OmschrijvingBevat = null, Richting Richting = Richting.Beide,
    Bedrag? BedragVan = null, Bedrag? BedragTot = null, string? Transactiecode = null)
{
    public bool IsLeeg =>
        string.IsNullOrWhiteSpace(TegenIban) && string.IsNullOrWhiteSpace(NaamBevat) && string.IsNullOrWhiteSpace(OmschrijvingBevat)
        && Richting == Richting.Beide && BedragVan is null && BedragTot is null && string.IsNullOrWhiteSpace(Transactiecode);

    /// <summary>"Regel maken van deze boeking": tegen-IBAN en richting, of de transactiecode als er geen tegenrekening is.</summary>
    public static BoekingsregelVoorwaarden Van(Bankmutatie mutatie)
    {
        ArgumentNullException.ThrowIfNull(mutatie);
        var richting = mutatie.IsBij ? Richting.Bij : Richting.Af;
        return mutatie.TegenIban is { } iban
            ? new BoekingsregelVoorwaarden(TegenIban: iban, Richting: richting)
            : new BoekingsregelVoorwaarden(Richting: richting, Transactiecode: mutatie.Code.EigenSleutel ?? mutatie.Code.Iso);
    }
}

/// <summary>Herkent banktransacties en geeft de grootboekrekening (besluit 64). De eerste passende regel wint.</summary>
public sealed class Boekingsregel
{
    private Boekingsregel()
    {
        Naam = null!;
        Rekening = null!;
    }

    public Boekingsregel(AdministratieSoort administratie, string naam, Grootboekrekening rekening, BoekingsregelVoorwaarden voorwaarden,
        string? omschrijving = null, int volgorde = 0)
    {
        Naam = null!;
        Rekening = null!;
        Administratie = administratie;
        Volgorde = volgorde;
        Wijzig(naam, rekening, voorwaarden, omschrijving);
        IsActief = true;
    }

    public int Id { get; private set; }
    public AdministratieSoort Administratie { get; private set; }
    public int Volgorde { get; private set; }
    public string Naam { get; private set; }
    public string? TegenIban { get; private set; }
    public string? NaamBevat { get; private set; }
    public string? OmschrijvingBevat { get; private set; }
    public Richting Richting { get; private set; }
    public Bedrag? BedragVan { get; private set; }
    public Bedrag? BedragTot { get; private set; }
    public string? Transactiecode { get; private set; }
    public Grootboekrekening Rekening { get; private set; }

    /// <summary>Omschrijving van de post; leeg betekent de omschrijving van de bank.</summary>
    public string? Omschrijving { get; private set; }

    public bool IsActief { get; private set; }

    public BoekingsregelVoorwaarden Voorwaarden =>
        new(TegenIban, NaamBevat, OmschrijvingBevat, Richting, BedragVan, BedragTot, Transactiecode);

    /// <exception cref="ArgumentException">Geen voorwaarde, of bedrag van is groter dan tot.</exception>
    public void Wijzig(string naam, Grootboekrekening rekening, BoekingsregelVoorwaarden voorwaarden, string? omschrijving)
    {
        ArgumentNullException.ThrowIfNull(rekening);
        ArgumentNullException.ThrowIfNull(voorwaarden);
        if (voorwaarden.IsLeeg)
        {
            throw new ArgumentException("Geef minstens één voorwaarde, anders past de regel op elke transactie.", nameof(voorwaarden));
        }

        if (voorwaarden.BedragVan is { } van && voorwaarden.BedragTot is { } tot && van > tot)
        {
            throw new ArgumentException("Het bedrag 'van' is groter dan 'tot'.", nameof(voorwaarden));
        }

        Naam = string.IsNullOrWhiteSpace(naam) ? rekening.ToString() : naam.Trim();
        Rekening = rekening;
        TegenIban = Iban.TryMaak(voorwaarden.TegenIban, out var iban) ? iban.Waarde : null;
        NaamBevat = Leeg(voorwaarden.NaamBevat);
        OmschrijvingBevat = Leeg(voorwaarden.OmschrijvingBevat);
        Richting = voorwaarden.Richting;
        BedragVan = voorwaarden.BedragVan;
        BedragTot = voorwaarden.BedragTot;
        Transactiecode = Leeg(voorwaarden.Transactiecode)?.ToUpperInvariant();
        Omschrijving = Leeg(omschrijving);
    }

    public void ZetVolgorde(int volgorde) => Volgorde = volgorde;

    public void Activeer() => IsActief = true;

    public void Deactiveer() => IsActief = false;

    public bool Past(Bankmutatie mutatie)
    {
        ArgumentNullException.ThrowIfNull(mutatie);
        return IsActief
            && (TegenIban is null || string.Equals(TegenIban, mutatie.TegenIban, StringComparison.Ordinal))
            && Bevat(mutatie.Tegennaam, NaamBevat)
            && Bevat(mutatie.Omschrijving, OmschrijvingBevat)
            && (Richting == Richting.Beide || (Richting == Richting.Bij) == mutatie.IsBij)
            && (BedragVan is null || mutatie.Bedrag >= BedragVan.Value)
            && (BedragTot is null || mutatie.Bedrag <= BedragTot.Value)
            && (Transactiecode is null || mutatie.Code.Past(Transactiecode));
    }

    private static bool Bevat(string? tekst, string? deel) =>
        deel is null || (tekst is not null && tekst.Contains(deel, StringComparison.OrdinalIgnoreCase));

    private static string? Leeg(string? tekst) => string.IsNullOrWhiteSpace(tekst) ? null : tekst.Trim();
}
