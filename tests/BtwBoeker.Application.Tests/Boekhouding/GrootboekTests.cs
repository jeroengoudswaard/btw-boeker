using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Application.Tests.Boekhouding;

public class GrootboekTests
{
    private readonly BoekhoudingTestdata _data = new();

    private void Basis()
    {
        _data.Geboekt(new DateOnly(2026, 1, 1), "Openingsbalans", PostSoort.Openingsbalans,
            (_data.Kas, 61000, 0), (_data.Bank, 1342345, 0), (_data.Reserve, 0, 1403345));
        _data.Geboekt(new DateOnly(2026, 3, 3), "Contributie maart", (_data.Bank, 109900, 0), (_data.Welpen, 0, 109900));
        _data.Geboekt(new DateOnly(2026, 4, 1), "Energie", (_data.Energie, 30000, 0), (_data.Bank, 0, 30000));
    }

    [Fact]
    public void Saldo_per_rekening_alleen_geboekte_posten()
    {
        Basis();
        var concept = new Journaalpost(BoekhoudingTestdata.Ver, _data.Memoriaal, new DateOnly(2026, 5, 1), "concept");
        concept.Wijzig(_data.Memoriaal, concept.Datum, "concept",
            [new RegelInvoer(_data.Energie, null, new Bedrag(999), Bedrag.Nul), new RegelInvoer(_data.Bank, null, Bedrag.Nul, new Bedrag(999))]);
        _data.Posten.Add(concept);

        Assert.Equal(1342345 + 109900 - 30000, Grootboek.Saldo(_data.Stand, _data.Bank, 2026));
        Assert.Equal(1342345 + 109900, Grootboek.Saldo(_data.Stand, _data.Bank, 2026, totEnMet: new DateOnly(2026, 3, 31)));
    }

    [Fact]
    public void Saldibalans_is_in_evenwicht()
    {
        Basis();

        var balans = Grootboek.Saldibalans(_data.Stand, 2026);

        Assert.Equal(5, balans.Regels.Count);
        Assert.Equal(balans.TotaalDebet, balans.TotaalCredit);
        var bank = balans.Regels.Single(r => r.Rekening == _data.Bank);
        Assert.Equal(new Bedrag(1342345 + 109900), bank.Debet);
        Assert.Equal(new Bedrag(30000), bank.Credit);
    }

    [Fact]
    public void Wenv_per_rubriek_met_resultaat()
    {
        Basis();

        var wenv = Grootboek.WinstEnVerlies(_data.Stand, 2026);

        Assert.Equal(new Bedrag(109900), wenv.TotaalBaten);
        Assert.Equal(new Bedrag(30000), wenv.TotaalLasten);
        Assert.Equal(79900, wenv.Resultaat);
        Assert.Equal("Contributie", Assert.Single(wenv.Baten).Rubriek);
        Assert.Equal("Terreinkosten", Assert.Single(wenv.Lasten).Rubriek);
    }

    [Fact]
    public void Balans_bevat_resultaat_lopend_boekjaar_en_is_in_evenwicht()
    {
        Basis();

        var balans = Grootboek.Balans(_data.Stand, 2026);

        Assert.Equal(balans.TotaalActiva, balans.TotaalPassiva);
        Assert.Equal(new Bedrag(61000 + 1342345 + 109900 - 30000), balans.TotaalActiva);
        Assert.Equal(79900, balans.ResultaatLopendJaar);
    }

    [Fact]
    public void Afsluitpost_telt_niet_mee_in_wenv()
    {
        Basis();
        _data.Geboekt(new DateOnly(2026, 12, 31), "Afsluiting", PostSoort.Afsluiting,
            (_data.Welpen, 109900, 0), (_data.Energie, 0, 30000), (_data.Reserve, 0, 79900));

        var wenv = Grootboek.WinstEnVerlies(_data.Stand, 2026);
        var balans = Grootboek.Balans(_data.Stand, 2026);

        Assert.Equal(79900, wenv.Resultaat);
        Assert.Equal(0, balans.ResultaatLopendJaar);
        Assert.Equal(balans.TotaalActiva, balans.TotaalPassiva);
    }

    [Fact]
    public void Grootboekkaart_met_beginsaldo_en_lopend_saldo()
    {
        Basis();

        var kaart = Grootboek.Kaart(_data.Stand, _data.Bank, 2026, new DateOnly(2026, 3, 1), new DateOnly(2026, 12, 31));

        Assert.Equal(1342345, kaart.Beginsaldo);
        Assert.Equal(2, kaart.Mutaties.Count);
        Assert.Equal(1342345 + 109900, kaart.Mutaties[0].LopendSaldo);
        Assert.Equal("MEM-2026-0002", kaart.Mutaties[0].Nummer);
        Assert.Equal(1342345 + 109900 - 30000, kaart.Eindsaldo);
    }

    [Fact]
    public void Rapporten_als_tabel_voor_afdrukken_en_export()
    {
        Basis();

        var tabel = Rapporten.Balans(Grootboek.Balans(_data.Stand, 2026), "Vereniging");

        Assert.Equal("Balans per 31-12-2026", tabel.Titel);
        Assert.Contains(tabel.Rijen, r => r.IsKop && Equals(r.Cellen[0], "Activa"));
        Assert.Contains(tabel.Rijen, r => r.IsTotaal);
    }
}
