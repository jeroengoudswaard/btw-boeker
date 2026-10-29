using Verenigd.Application.Begrotingen;
using Verenigd.Application.Boekhouding;
using Verenigd.Application.Tests.Boekhouding;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Begrotingen;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.Application.Tests.Begrotingen;

/// <summary>Begroting Vereniging (besluiten 146–155). Bedragen zijn verzonnen.</summary>
public sealed class BegrotingTests
{
    private static readonly DateTime Nu = new(2026, 10, 29, 9, 0, 0, DateTimeKind.Utc);
    private readonly BoekhoudingTestdata _data = new();

    public BegrotingTests() => _data.Boekjaren.Add(new Boekjaar(BoekhoudingTestdata.Ver, 2025));

    private BegrotingDienst Dienst(DateTimeOffset? nu = null) =>
        new(new BoekhoudingDienstTests.FakeSessie(_data), new BoekhoudingDienstTests.VasteTijd(nu ?? new DateTimeOffset(Nu)));

    /// <summary>2025: energie 1.150,00 en contributie 4.210,00; 2026 t/m oktober: energie 1.020,00 en contributie 3.980,00.</summary>
    private void Werkelijk()
    {
        _data.Geboekt(new DateOnly(2025, 6, 1), "Energie 2025", (_data.Energie, 115000, 0), (_data.Bank, 0, 115000));
        _data.Geboekt(new DateOnly(2025, 6, 1), "Contributie 2025", (_data.Bank, 421000, 0), (_data.Welpen, 0, 421000));
        _data.Geboekt(new DateOnly(2026, 5, 1), "Energie 2026", (_data.Energie, 102000, 0), (_data.Bank, 0, 102000));
        _data.Geboekt(new DateOnly(2026, 9, 1), "Contributie 2026", (_data.Bank, 398000, 0), (_data.Welpen, 0, 398000));
    }

    [Fact]
    public void Overnemen_van_de_werkelijke_cijfers_met_percentage_rondt_af_op_tientallen()
    {
        Werkelijk();

        var bedragen = Begrotingsrekenaar.Overnemen(_data.Stand, BegrotingBron.Werkelijk, 2025, 5m);

        Assert.Equal(new Bedrag(121000), bedragen[_data.Energie]); // 1.150 + 5% = 1.207,50 → 1.210
        Assert.Equal(new Bedrag(442000), bedragen[_data.Welpen]); // 4.210 + 5% = 4.420,50 → 4.420
        Assert.Equal(new Bedrag(115000), Begrotingsrekenaar.Overnemen(_data.Stand, BegrotingBron.Werkelijk, 2025, 0m)[_data.Energie]);
    }

    [Fact]
    public void Overnemen_van_een_begroting_gebruikt_de_geldende_versie()
    {
        var oud = new Begroting(BoekhoudingTestdata.Ver, 2026, Nu);
        oud.ZetBedrag(_data.Energie, new Bedrag(100000), null);
        oud.StelVast(new DateOnly(2025, 12, 1), null);
        var herziening = oud.Herzie(Nu);
        herziening.ZetBedrag(_data.Energie, new Bedrag(120000), null);
        herziening.StelVast(new DateOnly(2026, 6, 1), null);
        _data.Begrotingen.AddRange([oud, herziening]);

        Assert.Same(herziening, Begrotingsrekenaar.Geldend(_data.Stand, 2026));
        Assert.Equal(new Bedrag(126000), Begrotingsrekenaar.Overnemen(_data.Stand, BegrotingBron.Begroting, 2026, 5m)[_data.Energie]); // 1.200 + 5% = 1.260
    }

    [Fact]
    public void Een_concept_geldt_als_er_nog_geen_vastgestelde_versie_is()
    {
        var concept = new Begroting(BoekhoudingTestdata.Ver, 2027, Nu);
        _data.Begrotingen.Add(concept);

        Assert.Same(concept, Begrotingsrekenaar.Geldend(_data.Stand, 2027));
        Assert.Null(Begrotingsrekenaar.Geldend(_data.Stand, 2028));
    }

    [Fact]
    public void Overzicht_lopend_jaar_vergelijkt_naar_rato_per_dag()
    {
        Werkelijk();
        var begroting = new Begroting(BoekhoudingTestdata.Ver, 2026, Nu);
        begroting.ZetBedrag(_data.Energie, new Bedrag(120000), null);
        begroting.ZetBedrag(_data.Welpen, new Bedrag(430000), null);
        _data.Begrotingen.Add(begroting);
        var vandaag = new DateOnly(2026, 7, 2); // dag 183 van 365

        var overzicht = Begrotingsrekenaar.Overzicht(_data.Stand, 2026, vandaag);

        var energie = overzicht.Lasten.SelectMany(r => r.Regels).Single(r => r.Rekening == _data.Energie);
        Assert.Equal(new Bedrag(115000), energie.WerkelijkVorigJaar);
        Assert.Equal(new Bedrag(120000), energie.Begroot);
        Assert.Equal(new Bedrag(102000), energie.Werkelijk); // geboekt in mei
        Assert.Equal(new Bedrag(60164), energie.NaarRato); // 1.200 × 183/365
        Assert.Equal(102000 - 60164, energie.Verschil);

        var welpen = overzicht.Baten.SelectMany(r => r.Regels).Single(r => r.Rekening == _data.Welpen);
        Assert.Equal(Bedrag.Nul, welpen.Werkelijk); // contributie pas in september geboekt
        Assert.Equal(new Bedrag(430000), overzicht.TotaalBegroot(ResultaatKant.Baten));
        Assert.Equal(430000 - 120000, overzicht.BegrootResultaat);
    }

    [Fact]
    public void Overzicht_afgelopen_jaar_vergelijkt_met_het_hele_bedrag_en_zonder_afsluitpost()
    {
        Werkelijk();
        _data.Geboekt(new DateOnly(2025, 12, 31), "Afsluiting", PostSoort.Afsluiting, (_data.Welpen, 421000, 0), (_data.Energie, 0, 115000), (_data.Reserve, 0, 306000));

        var overzicht = Begrotingsrekenaar.Overzicht(_data.Stand, 2025, new DateOnly(2026, 10, 29));

        var energie = overzicht.Lasten.SelectMany(r => r.Regels).Single(r => r.Rekening == _data.Energie);
        Assert.Equal(new Bedrag(115000), energie.Werkelijk);
        Assert.Equal(Bedrag.Nul, energie.Begroot);
        Assert.Equal(1m, overzicht.Fractie);
    }

    [Fact]
    public void Historisch_jaar_telt_als_werkelijk_vorig_jaar()
    {
        _data.Boekjaren.RemoveAll(b => b.Jaar == 2025);
        _data.Boekjaren.Add(Boekjaar.Historisch(BoekhoudingTestdata.Ver, 2025, Nu));
        _data.HistorischeTotalen.Add(new HistorischTotaal(BoekhoudingTestdata.Ver, 2025, _data.Energie, new Bedrag(99900), Bedrag.Nul));

        var overzicht = Begrotingsrekenaar.Overzicht(_data.Stand, 2026, new DateOnly(2026, 1, 1));

        Assert.Equal(new Bedrag(99900), overzicht.Lasten.SelectMany(r => r.Regels).Single(r => r.Rekening == _data.Energie).WerkelijkVorigJaar);
    }

    [Fact]
    public async Task Dienst_maakt_bewaart_neemt_over_stelt_vast_en_herziet()
    {
        Werkelijk();
        var dienst = Dienst();

        var id = await dienst.MaakAsync(BoekhoudingTestdata.Ver, 2027);
        await Assert.ThrowsAsync<InvalidOperationException>(() => dienst.MaakAsync(BoekhoudingTestdata.Ver, 2027));
        await dienst.NeemOverAsync(BoekhoudingTestdata.Ver, id, BegrotingBron.Werkelijk, 2026, 10m);
        var begroting = _data.Begrotingen.Single();
        Assert.Equal(new Bedrag(112000), begroting.Bedrag(_data.Energie)); // 1.020 + 10% = 1.122 → 1.120

        await dienst.BewaarAsync(BoekhoudingTestdata.Ver, id, [new BegrotingInvoer(_data.Energie.Id, new Bedrag(125000), "Nieuw contract")]);
        Assert.Equal(new Bedrag(125000), begroting.Bedrag(_data.Energie));
        Assert.Equal(new Bedrag(438000), begroting.Bedrag(_data.Welpen)); // 3.980 + 10% = 4.378 → 4.380

        await dienst.StelVastAsync(BoekhoudingTestdata.Ver, id, new DateOnly(2026, 12, 12), "ALV 12-12-2026");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dienst.BewaarAsync(BoekhoudingTestdata.Ver, id, [new BegrotingInvoer(_data.Energie.Id, new Bedrag(1), null)]));

        var tweede = await dienst.HerzieAsync(BoekhoudingTestdata.Ver, id);
        Assert.Equal(2, _data.Begrotingen.Single(b => b.Id == tweede).Versie);
        await Assert.ThrowsAsync<InvalidOperationException>(() => dienst.HerzieAsync(BoekhoudingTestdata.Ver, id)); // er is al een nieuwere versie
    }

    [Fact]
    public void Alv_afdruk_heeft_per_rubriek_werkelijk_vorig_jaar_begroot_dit_jaar_en_volgend_jaar()
    {
        Werkelijk();
        var dit = new Begroting(BoekhoudingTestdata.Ver, 2026, Nu);
        dit.ZetBedrag(_data.Energie, new Bedrag(120000), null);
        var volgend = new Begroting(BoekhoudingTestdata.Ver, 2027, Nu);
        volgend.ZetBedrag(_data.Energie, new Bedrag(130000), null);
        volgend.ZetBedrag(_data.Welpen, new Bedrag(440000), null);
        _data.Begrotingen.AddRange([dit, volgend]);

        var tabel = Rapporten.BegrotingAlv(_data.Stand, 2027, "Vereniging");

        Assert.Equal("Begroting 2027", tabel.Titel);
        Assert.Equal(["Omschrijving", "Werkelijk 2025", "Begroot 2026", "Begroot 2027"], tabel.Kolommen.Select(k => k.Naam));
        var energie = tabel.Rijen.Single(r => (r.Cellen[0] as string) == "4115 Energie");
        Assert.Equal([new Bedrag(115000), new Bedrag(120000), new Bedrag(130000)], energie.Cellen.Skip(1));
        Assert.Contains(tabel.Rijen, r => r.IsKop && (string?)r.Cellen[0] == "Baten");
        Assert.Contains(tabel.Rijen, r => r.IsTotaal && (string?)r.Cellen[0] == "Totaal Terreinkosten");
        var resultaat = tabel.Rijen[^1];
        Assert.True(resultaat.IsTotaal);
        Assert.Equal(new Bedrag(440000 - 130000), resultaat.Cellen[3]);
    }
}
