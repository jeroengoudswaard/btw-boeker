using BtwBoeker.Application.Begrotingen;
using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Application.Tests.Boekhouding;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Begrotingen;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Application.Tests.Begrotingen;

/// <summary>Meerjarenplan (besluiten 159–167). Bedragen zijn verzonnen; de testdata gebruiken de rekeningen van de Vereniging.</summary>
public sealed class MeerjarenplanTests
{
    private static readonly DateTime Nu = new(2026, 10, 29, 9, 0, 0, DateTimeKind.Utc);
    private readonly BoekhoudingTestdata _data = new();

    private MeerjarenplanDienst Dienst() =>
        new(new BoekhoudingDienstTests.FakeSessie(_data), new BoekhoudingDienstTests.VasteTijd(new DateTimeOffset(Nu)));

    [Fact]
    public void Overzicht_geeft_bedragen_en_totalen_per_jaar()
    {
        var plan = new Meerjarenplan(BoekhoudingTestdata.Ver, 2027, Nu);
        plan.ZetBedrag(_data.Welpen, 2027, new Bedrag(600000));
        plan.ZetBedrag(_data.Welpen, 2028, new Bedrag(650000));
        plan.ZetBedrag(_data.Energie, 2027, new Bedrag(250000));
        _data.Meerjarenplannen.Add(plan);

        var overzicht = Meerjarenrekenaar.Overzicht(_data.Stand, plan);

        Assert.Equal(Enumerable.Range(2027, 10), overzicht.Jaren);
        var welpen = overzicht.Baten.Single(r => r.Rekening == _data.Welpen);
        Assert.Equal(new Bedrag(650000), welpen.Bedragen[1]);
        Assert.Equal(new Bedrag(600000), overzicht.TotaalBaten(0));
        Assert.Equal(new Bedrag(250000), overzicht.TotaalLasten(0));
        Assert.Equal(600000 - 250000, overzicht.Resultaat(0));
        Assert.Equal(650000, overzicht.Resultaat(1));
        Assert.Contains(overzicht.Lasten, r => r.Rekening == _data.Energie);
        Assert.DoesNotContain(overzicht.Baten.Concat(overzicht.Lasten), r => r.Rekening == _data.Bank);
    }

    [Fact]
    public void Geldend_plan_per_startjaar_is_de_laatste_vastgestelde_versie_anders_het_concept()
    {
        var plan = new Meerjarenplan(BoekhoudingTestdata.Ver, 2027, Nu);
        _data.Meerjarenplannen.Add(plan);
        Assert.Same(plan, Meerjarenrekenaar.Geldend(_data.Stand, 2027));

        plan.StelVast(new DateOnly(2026, 11, 20), null);
        var herziening = plan.Herzie(Nu);
        _data.Meerjarenplannen.Add(herziening);

        Assert.Same(plan, Meerjarenrekenaar.Geldend(_data.Stand, 2027));
        Assert.Null(Meerjarenrekenaar.Geldend(_data.Stand, 2030));
    }

    [Fact]
    public async Task Dienst_maakt_bewaart_stelt_vast_en_herziet()
    {
        var dienst = Dienst();

        var id = await dienst.MaakAsync(BoekhoudingTestdata.Ver, 2027);
        await Assert.ThrowsAsync<InvalidOperationException>(() => dienst.MaakAsync(BoekhoudingTestdata.Ver, 2027));
        var bedragen = new Bedrag[10];
        Array.Fill(bedragen, new Bedrag(600000));
        await dienst.BewaarAsync(BoekhoudingTestdata.Ver, id, [new MeerjarenInvoer(_data.Welpen.Id, bedragen, "Huurcontract")]);

        var plan = _data.Meerjarenplannen.Single();
        Assert.Equal(new Bedrag(600000), plan.Bedrag(_data.Welpen, 2036));
        Assert.Equal("Huurcontract", plan.Toelichting(_data.Welpen));

        await dienst.StelVastAsync(BoekhoudingTestdata.Ver, id, new DateOnly(2026, 11, 20), "Bestuur 20-11-2026");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dienst.BewaarAsync(BoekhoudingTestdata.Ver, id, [new MeerjarenInvoer(_data.Welpen.Id, bedragen, null)]));
        var tweede = await dienst.HerzieAsync(BoekhoudingTestdata.Ver, id);
        Assert.Equal(2, _data.Meerjarenplannen.Single(p => p.Id == tweede).Versie);
        await Assert.ThrowsAsync<InvalidOperationException>(() => dienst.HerzieAsync(BoekhoudingTestdata.Ver, id));
    }

    [Fact]
    public async Task Bewaren_met_een_verkeerd_aantal_jaren_wordt_geweigerd()
    {
        var dienst = Dienst();
        var id = await dienst.MaakAsync(BoekhoudingTestdata.Ver, 2027);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            dienst.BewaarAsync(BoekhoudingTestdata.Ver, id, [new MeerjarenInvoer(_data.Welpen.Id, [new Bedrag(1)], null)]));
    }

    [Fact]
    public void Rapport_is_liggend_met_een_kolom_per_jaar_en_een_resultaatregel()
    {
        var plan = new Meerjarenplan(BoekhoudingTestdata.Ver, 2027, Nu);
        plan.ZetBedrag(_data.Welpen, 2027, new Bedrag(600000));
        plan.ZetBedrag(_data.Energie, 2036, new Bedrag(100000));

        var tabel = Rapporten.Meerjarenplan(Meerjarenrekenaar.Overzicht(_data.Stand, plan), "Stichting");

        Assert.True(tabel.IsLiggend);
        Assert.Equal("Meerjarenplan 2027–2036", tabel.Titel);
        Assert.Equal(11, tabel.Kolommen.Count);
        Assert.Equal("2036", tabel.Kolommen[^1].Naam);
        var resultaat = tabel.Rijen[^1];
        Assert.True(resultaat.IsTotaal);
        Assert.Equal(new Bedrag(600000), resultaat.Cellen[1]);
        Assert.Equal(new Bedrag(-100000), resultaat.Cellen[10]);
    }
}
