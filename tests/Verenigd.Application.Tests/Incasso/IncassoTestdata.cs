using Verenigd.Application.Incasso;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Gedeeld;
using Verenigd.Domain.Leden;

namespace Verenigd.Application.Tests.Incasso;

/// <summary>Bouwt een verzonnen <see cref="IncassoStand"/> op. Alle IBAN's zijn openbare voorbeeldnummers.</summary>
internal sealed class IncassoTestdata
{
    public const string IbanA = "NL91ABNA0417164300";
    public const string IbanB = "BE68539007547034";
    private static readonly DateTime Nu = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly Dictionary<string, Groep> _groepen = new(StringComparer.Ordinal)
    {
        ["Welpen"] = new Groep("Welpen", GroepSoort.Speltak),
        ["Junioren"] = new Groep("Junioren", GroepSoort.Speltak),
    };

    public List<Persoon> Personen { get; } = [];
    public List<Contributiecategorie> Categorieen { get; } = [];
    public List<Tarief> Tarieven { get; } = [];
    public List<LidCategorie> LidCategorieen { get; } = [];
    public List<Betaler> Betalers { get; } = [];
    public List<BetalerLid> Koppelingen { get; } = [];
    public List<Incassorun> Runs { get; } = [];

    public Incassoinstellingen? Instellingen { get; set; } = new(
        IncassantId.Maak("DE98ZZZ09999999999"), "Scouting Testgroep", Iban.Maak("NL91ABNA0417164300"), 1, new Maand(2026, 1));

    public Contributiecategorie Welpen { get; }
    public Contributiecategorie Junioren { get; }
    public Contributiecategorie Leiding { get; }

    public IncassoTestdata()
    {
        Welpen = Categorie("Jeugdlid Welpen", CategorieSoort.Jeugdlid, "Welpen");
        Junioren = Categorie("Jeugdlid Junioren", CategorieSoort.Jeugdlid, "Junioren");
        Leiding = Categorie("Leiding", CategorieSoort.Leiding, null);
        foreach (var jaar in new[] { 2026, 2027 })
        {
            Tarieven.Add(new Tarief(Welpen, jaar, new Bedrag(jaar == 2026 ? 1000 : 1250)));
            Tarieven.Add(new Tarief(Junioren, jaar, new Bedrag(1500)));
            Tarieven.Add(new Tarief(Leiding, jaar, Bedrag.Nul));
        }
    }

    public IncassoStand Stand => new(Personen, Categorieen, Tarieven, LidCategorieen, Betalers, Koppelingen, Runs, Instellingen);

    public Contributiecategorie Categorie(string naam, CategorieSoort soort, string? speltak)
    {
        var categorie = new Contributiecategorie(naam, soort, speltak);
        Categorieen.Add(categorie);
        return categorie;
    }

    /// <summary>Een jeugdlid (of leider) met functie vanaf <paramref name="start"/>.</summary>
    public Persoon Lid(
        string lidnummer, string voornaam, DateOnly start, string groep = "Welpen", Lidtype lidtype = Lidtype.Jeugdlid,
        Contributiecategorie? categorie = null, DateOnly? categorieVanaf = null, string? iban = IbanA, bool machtiging = true)
    {
        var persoon = new Persoon(
            Lidnummer.Parse(lidnummer),
            new LidGegevens(voornaam, null, "Test", new DateOnly(2015, 1, 1), null, null, null, null, "Actief", null, null, null, null, null, null),
            Nu);
        persoon.WerkFunctiesBij(
            [new FunctieGegevens(groep, lidtype == Lidtype.Kaderlid ? "Leider" : "Lid", lidtype, start, null, "Actief")],
            _groepen, new DateOnly(2026, 10, 1), Nu);
        Personen.Add(persoon);

        if (categorie is not null)
        {
            LidCategorieen.Add(new LidCategorie(persoon.Lidnummer, categorie, categorieVanaf ?? start));
        }

        if (iban is not null)
        {
            var betaler = Betalers.Find(b => b.Iban == Iban.Maak(iban));
            if (betaler is null)
            {
                betaler = new Betaler(Iban.Maak(iban), "P. Test");
                if (machtiging)
                {
                    betaler.RegistreerMachtiging("VER-" + Betalers.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), new DateOnly(2019, 7, 1));
                }

                Betalers.Add(betaler);
            }

            Koppelingen.Add(new BetalerLid(persoon.Lidnummer, betaler.Id));
        }

        return persoon;
    }

    /// <summary>Legt een eerdere definitieve run vast met de gegeven lid × maand-combinaties.</summary>
    public Incassorun EerdereRun(Maand maand, params (Persoon Lid, Maand Maand)[] regels)
    {
        var run = new Incassorun(maand, maand.EersteDag, Nu);
        var post = run.VoegPostToe(Betalers[0].Id, Betalers[0].Iban, "P. Test", "VER-0", new DateOnly(2019, 7, 1), "x");
        foreach (var (lid, m) in regels)
        {
            post.VoegRegelToe(lid.Lidnummer, m, "Jeugdlid Welpen", new Bedrag(1000));
        }

        Runs.Add(run);
        return run;
    }
}
