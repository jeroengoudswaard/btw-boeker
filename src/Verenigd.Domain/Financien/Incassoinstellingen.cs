namespace Verenigd.Domain.Financien;

/// <summary>Gegevens van de Vereniging als incassant (besluiten 50, 56 en 57).</summary>
public sealed class Incassoinstellingen
{
    public const string StandaardSjabloon = "Contributie Scouting {maanden} - {voornamen}";

    private Incassoinstellingen()
    {
        Naam = null!;
        Omschrijvingssjabloon = null!;
    }

    public Incassoinstellingen(IncassantId incassantId, string naam, Iban iban, int incassodag, Maand eersteIncassomaand)
    {
        Id = 1;
        Omschrijvingssjabloon = StandaardSjabloon;
        Naam = null!;
        Wijzig(incassantId, naam, iban, incassodag, eersteIncassomaand, StandaardSjabloon);
    }

    public int Id { get; private set; }
    public IncassantId IncassantId { get; private set; }
    public string Naam { get; private set; }
    public Iban Iban { get; private set; }

    /// <summary>Dag van de maand (1–28) waarop wordt geïncasseerd.</summary>
    public int Incassodag { get; private set; }

    /// <summary>
    /// De eerste maand waarover Verenigd incasseert. Eerdere maanden zijn nooit verschuldigd via Verenigd, zodat de
    /// eerste run geen jaren aan naheffing bevat.
    /// </summary>
    public Maand EersteIncassomaand { get; private set; }

    /// <summary>Met <c>{maanden}</c> en <c>{voornamen}</c>.</summary>
    public string Omschrijvingssjabloon { get; private set; }

    public void Wijzig(IncassantId incassantId, string naam, Iban iban, int incassodag, Maand eersteIncassomaand, string omschrijvingssjabloon)
    {
        if (string.IsNullOrWhiteSpace(naam))
        {
            throw new ArgumentException("De naam van de incassant is verplicht.", nameof(naam));
        }

        if (!iban.ControlegetalGeldig)
        {
            throw new ArgumentException("Het IBAN van de Vereniging is ongeldig.", nameof(iban));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(incassodag, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(incassodag, 28);
        if (string.IsNullOrWhiteSpace(omschrijvingssjabloon))
        {
            throw new ArgumentException("Het omschrijvingssjabloon is verplicht.", nameof(omschrijvingssjabloon));
        }

        IncassantId = incassantId;
        Naam = SepaTekst.Normaliseer(naam, 70);
        Iban = iban;
        Incassodag = incassodag;
        EersteIncassomaand = eersteIncassomaand;
        Omschrijvingssjabloon = omschrijvingssjabloon.Trim();
    }

    /// <summary>De uitvoerdatum voor een maand: de incassodag, verschoven naar de volgende bankwerkdag (besluit 59).</summary>
    public DateOnly Uitvoerdatum(Maand maand) => Bankwerkdagen.OpOfNa(new DateOnly(maand.Jaar, maand.Nummer, Incassodag));
}
