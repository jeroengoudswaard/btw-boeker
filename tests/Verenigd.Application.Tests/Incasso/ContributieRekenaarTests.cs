using Verenigd.Application.Incasso;
using Verenigd.Domain.Financien;

namespace Verenigd.Application.Tests.Incasso;

public class ContributieRekenaarTests
{
    private static readonly Maand Maart = new(2027, 3);
    private static readonly DateOnly Vandaag = new(2027, 2, 20);
    private readonly IncassoTestdata _data = new();

    private IncassoVoorstel Bereken(Maand? maand = null, DateOnly? vandaag = null) =>
        ContributieRekenaar.Bereken(_data.Stand, maand ?? Maart, vandaag ?? Vandaag);

    private static IReadOnlyList<VoorstelRegel> Regels(IncassoVoorstel voorstel) => [.. voorstel.Posten.SelectMany(p => p.Regels)];

    [Fact]
    public void Lid_dat_op_de_eerste_ingeschreven_is_betaalt_het_maandbedrag()
    {
        _data.Lid("1", "Anna", new DateOnly(2026, 9, 1), categorie: _data.Welpen);
        _data.EerdereRun(new Maand(2027, 2), [.. new Maand(2027, 1).Tot(new Maand(2027, 2)).Select(m => (_data.Personen[0], m))]);

        var voorstel = Bereken();

        var regel = Assert.Single(Regels(voorstel));
        Assert.Equal(Maart, regel.Maand);
        Assert.Equal(new Bedrag(1250), regel.Bedrag);
        Assert.False(regel.IsNaheffing);
        Assert.Equal(new DateOnly(2027, 3, 1), voorstel.Uitvoerdatum);
    }

    [Theory]
    [InlineData(1, true)]   // start op 1 maart: maart telt
    [InlineData(2, false)]  // start op 2 maart: pas vanaf april (besluit 47)
    public void Nieuw_lid_betaalt_vanaf_de_maand_na_de_start(int dag, bool maartTelt)
    {
        _data.Lid("1", "Anna", new DateOnly(2027, 3, dag), categorie: _data.Welpen);

        Assert.Equal(maartTelt, Regels(Bereken()).Any(r => r.Maand == Maart));
    }

    [Fact]
    public void Uitgeschreven_lid_betaalt_tot_en_met_de_lopende_maand()
    {
        var anna = _data.Lid("1", "Anna", new DateOnly(2026, 9, 1), categorie: _data.Welpen);
        anna.Uitschrijven(new DateOnly(2027, 3, 15), DateTime.UtcNow);
        _data.EerdereRun(new Maand(2027, 2), (anna, new Maand(2027, 1)), (anna, new Maand(2027, 2)));

        Assert.Single(Regels(Bereken()), r => r.Maand == Maart);
        Assert.DoesNotContain(Regels(Bereken(new Maand(2027, 4))), r => r.Maand == new Maand(2027, 4));
    }

    [Fact]
    public void Categoriewissel_geldt_vanaf_de_eerste_volgende_maand()
    {
        var anna = _data.Lid("1", "Anna", new DateOnly(2026, 9, 1), categorie: _data.Welpen);
        _data.LidCategorieen.Add(new(anna.Lidnummer, _data.Junioren, new DateOnly(2027, 3, 15)));
        _data.EerdereRun(new Maand(2027, 2), (anna, new Maand(2027, 1)), (anna, new Maand(2027, 2)));

        Assert.Equal("Jeugdlid Welpen", Assert.Single(Regels(Bereken())).Categorie);
        Assert.Equal("Jeugdlid Junioren", Regels(Bereken(new Maand(2027, 4))).Single(r => r.Maand == new Maand(2027, 4)).Categorie);
    }

    [Fact]
    public void Leiding_betaalt_niet()
    {
        _data.Lid("1", "Bo", new DateOnly(2020, 1, 1), lidtype: Domain.Leden.Lidtype.Kaderlid, categorie: _data.Leiding);

        var voorstel = Bereken();

        Assert.Empty(voorstel.Posten);
        Assert.Empty(voorstel.ZelfTeInnen);
    }

    [Fact]
    public void Gemiste_maanden_binnen_het_boekjaar_worden_nageheven_als_aparte_regels()
    {
        _data.Lid("1", "Anna", new DateOnly(2026, 6, 1), categorie: _data.Welpen);

        var regels = Regels(Bereken());

        Assert.Equal([new Maand(2027, 1), new Maand(2027, 2), Maart], regels.Select(r => r.Maand));
        Assert.True(regels[0].IsNaheffing);
        Assert.False(regels[2].IsNaheffing);
        Assert.Equal(new Bedrag(3750), Assert.Single(Bereken().Posten).Bedrag);
    }

    [Fact]
    public void Gemiste_maanden_uit_vorig_boekjaar_komen_op_lijst_zelf_te_innen()
    {
        _data.Lid("1", "Anna", new DateOnly(2026, 10, 15), categorie: _data.Welpen);

        var voorstel = Bereken();

        var zelf = Assert.Single(voorstel.ZelfTeInnen);
        Assert.Equal("nov en dec 2026", zelf.Maanden);
        Assert.Equal(new Bedrag(2000), zelf.Bedrag);
        Assert.Contains("vorig boekjaar", zelf.Reden, StringComparison.Ordinal);
    }

    [Fact]
    public void Maanden_voor_de_eerste_incassomaand_zijn_nooit_verschuldigd()
    {
        _data.Instellingen = new(IncassantId.Maak("DE98ZZZ09999999999"), "Scouting", Domain.Financien.Iban.Maak(IncassoTestdata.IbanA), 1, Maart);
        _data.Lid("1", "Anna", new DateOnly(2015, 1, 1), categorie: _data.Welpen);

        var voorstel = Bereken();

        Assert.Equal([Maart], Regels(voorstel).Select(r => r.Maand));
        Assert.Empty(voorstel.ZelfTeInnen);
    }

    [Fact]
    public void Een_maand_wordt_nooit_twee_keer_geincasseerd_maar_na_annuleren_wel_weer()
    {
        var anna = _data.Lid("1", "Anna", new DateOnly(2026, 9, 1), categorie: _data.Welpen);
        var eerder = _data.EerdereRun(Maart, (anna, new Maand(2027, 1)), (anna, new Maand(2027, 2)), (anna, Maart));

        Assert.True(Bereken().IsLeeg);

        eerder.Annuleer(DateTime.UtcNow);
        Assert.Equal(3, Regels(Bereken()).Count);
    }

    [Fact]
    public void Een_post_per_betaler_met_alle_leden_en_voornamen_in_de_omschrijving()
    {
        _data.Lid("1", "Anna", new DateOnly(2026, 9, 1), categorie: _data.Welpen);
        _data.Lid("2", "Zoë", new DateOnly(2026, 9, 1), groep: "Junioren", categorie: _data.Junioren);
        _data.Lid("3", "Cas", new DateOnly(2026, 9, 1), categorie: _data.Welpen, iban: IncassoTestdata.IbanB);
        foreach (var lid in _data.Personen)
        {
            _data.EerdereRun(new Maand(2027, 2), (lid, new Maand(2027, 1)), (lid, new Maand(2027, 2)));
        }

        var voorstel = Bereken();

        Assert.Equal(2, voorstel.Posten.Count);
        var gezin = voorstel.Posten.Single(p => p.Betaler.Iban.Waarde == IncassoTestdata.IbanA);
        Assert.Equal(new Bedrag(2750), gezin.Bedrag); // geen gezinskorting (besluit 46)
        Assert.Equal("Contributie Scouting maart 2027 - Anna, Zoe", gezin.Omschrijving);
        Assert.Equal(new Bedrag(4000), voorstel.Totaal);
    }

    [Fact]
    public void Omschrijving_noemt_alle_maanden_bij_naheffing()
    {
        _data.Lid("1", "Anna", new DateOnly(2026, 6, 1), categorie: _data.Welpen);

        Assert.Equal("Contributie Scouting jan, feb en mrt 2027 - Anna", Assert.Single(Bereken().Posten).Omschrijving);
    }

    [Theory]
    [InlineData(null, true, "geen betaler")]
    [InlineData(IncassoTestdata.IbanA, false, "geen geldige machtiging")]
    [InlineData("NL92ABNA0417164300", true, "ongeldig IBAN")]
    public void Zonder_geldige_betaler_of_machtiging_naar_zelf_te_innen(string? iban, bool machtiging, string reden)
    {
        _data.Lid("1", "Anna", new DateOnly(2027, 1, 1), categorie: _data.Welpen, iban: iban, machtiging: machtiging);

        var voorstel = Bereken();

        Assert.Empty(voorstel.Posten);
        var zelf = Assert.Single(voorstel.ZelfTeInnen);
        Assert.Contains(reden, zelf.Reden, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new Bedrag(3750), zelf.Bedrag);
    }

    [Fact]
    public void Ingetrokken_machtiging_voor_uitvoerdatum_telt_niet()
    {
        _data.Lid("1", "Anna", new DateOnly(2027, 1, 1), categorie: _data.Welpen);
        _data.Betalers[0].TrekMachtigingIn(new DateOnly(2027, 2, 15));

        Assert.Contains("machtiging", Assert.Single(Bereken().ZelfTeInnen).Reden, StringComparison.Ordinal);
    }

    [Fact]
    public void Jeugdlid_zonder_categorie_naar_zelf_te_innen()
    {
        _data.Lid("1", "Anna", new DateOnly(2027, 1, 1));

        var zelf = Assert.Single(Bereken().ZelfTeInnen);
        Assert.Contains("geen categorie", zelf.Reden, StringComparison.Ordinal);
        Assert.Null(zelf.Bedrag);
    }

    [Fact]
    public void Categorie_zonder_tarief_voor_het_boekjaar_naar_zelf_te_innen()
    {
        var loodsen = _data.Categorie("Jeugdlid Loodsen", CategorieSoort.Jeugdlid, "Loodsen");
        _data.Lid("1", "Anna", new DateOnly(2027, 1, 1), categorie: loodsen);

        Assert.Contains("geen tarief 2027", Assert.Single(Bereken().ZelfTeInnen).Reden, StringComparison.Ordinal);
    }

    [Fact]
    public void Uitvoerdatum_schuift_naar_werkdag_en_te_krap_geeft_melding()
    {
        _data.Instellingen = new(IncassantId.Maak("DE98ZZZ09999999999"), "Scouting", Domain.Financien.Iban.Maak(IncassoTestdata.IbanA), 27, new Maand(2026, 1));

        var voorstel = Bereken(new Maand(2027, 2), vandaag: new DateOnly(2027, 2, 26));

        Assert.Equal(new DateOnly(2027, 3, 1), voorstel.Uitvoerdatum); // za 27 feb -> ma 1 mrt
        Assert.Empty(voorstel.Meldingen);

        var krap = Bereken(new Maand(2027, 2), vandaag: new DateOnly(2027, 3, 1));
        Assert.Equal(new DateOnly(2027, 3, 2), krap.Uitvoerdatum);
        Assert.NotEmpty(krap.Meldingen);
    }

    [Fact]
    public void Zonder_incassogegevens_kan_niet_worden_gerekend()
    {
        _data.Instellingen = null;

        Assert.Throws<InvalidOperationException>(() => Bereken());
    }

    [Fact]
    public void Voorstel_voor_categorie_volgt_actieve_jeugdlidfunctie()
    {
        var anna = _data.Lid("1", "Anna", new DateOnly(2026, 9, 1), groep: "Junioren");
        var bo = _data.Lid("2", "Bo", new DateOnly(2020, 1, 1), lidtype: Domain.Leden.Lidtype.Kaderlid);

        Assert.Same(_data.Junioren, CategorieVoorstel.Bepaal(anna, _data.Categorieen, Vandaag));
        Assert.Same(_data.Leiding, CategorieVoorstel.Bepaal(bo, _data.Categorieen, Vandaag));
    }
}
