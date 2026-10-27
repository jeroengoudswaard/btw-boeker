using Verenigd.Application.Bank;
using Verenigd.Application.Tests.Boekhouding;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.Application.Tests.Bank;

public class BankimportPlannerTests
{
    private readonly BoekhoudingTestdata _data = new();

    private static CamtBestand Bestand(string iban, params VbAfschrift[] afschriften) =>
        Camt053Lezer.Lees(CamtVoorbeeld.Stroom(CamtVoorbeeld.Xml(iban, "08", afschriften)));

    private static VbAfschrift Dag(int maand, int dag, long begin, params VbTransactie[] t) => new(new DateOnly(2026, maand, dag), begin, t);

    [Fact]
    public void Onbekende_iban_geeft_rekening_zonder_dagboek_en_alles_nieuw()
    {
        var voorstel = BankimportPlanner.Plan(_data.Stand, [Bestand(CamtVoorbeeld.Betaal, Dag(1, 2, 0, new VbTransactie(1000, Ref: "A")))]);

        var rekening = Assert.Single(voorstel.Rekeningen);
        Assert.Null(rekening.Dagboek);
        Assert.Equal(CamtVoorbeeld.Betaal, rekening.Iban.Waarde);
        Assert.Single(rekening.Nieuw);
        Assert.Equal(1, voorstel.AantalNieuw);
    }

    [Fact]
    public void Al_ingelezen_en_dubbel_in_twee_bestanden_wordt_maar_een_keer_nieuw()
    {
        var rabo = _data.Bankdagboek("RABO", CamtVoorbeeld.Betaal);
        var bekend = new VbTransactie(1000, Ref: "A");
        var plan1 = BankimportPlanner.Plan(_data.Stand, [Bestand(CamtVoorbeeld.Betaal, Dag(1, 2, 0, bekend))]);
        var sleutel = plan1.Rekeningen[0].Nieuw[0].Sleutel;
        _data.Bankmutaties.Add(new Domain.Financien.Bank.Bankmutatie(BoekhoudingTestdata.Ver, rabo, sleutel, new DateOnly(2026, 1, 2),
            new Bedrag(1000), true, default, null, null, null, null, null, null));

        var nieuw = new VbTransactie(500, Ref: "B", NtryRef: "2");
        var voorstel = BankimportPlanner.Plan(_data.Stand,
        [
            Bestand(CamtVoorbeeld.Betaal, Dag(1, 2, 0, bekend, nieuw)),
            Bestand(CamtVoorbeeld.Betaal, Dag(1, 2, 0, bekend, nieuw)),
        ]);

        var rekening = Assert.Single(voorstel.Rekeningen);
        Assert.Same(rabo, rekening.Dagboek);
        Assert.Single(rekening.Nieuw);
        Assert.Equal(1, rekening.AlBekend);
        Assert.Single(rekening.Afschriften);
    }

    [Fact]
    public void Sleutel_gebruikt_bankreferentie_en_anders_datum_bedrag_en_regel()
    {
        var bestand = Bestand(CamtVoorbeeld.Spaar, Dag(3, 1, 0, new VbTransactie(1000, Ref: "X1"), new VbTransactie(250, Ref: null, Familie: null, Eigen: "1025", NtryRef: "7")));
        var t = bestand.Afschriften[0].Transacties;

        Assert.Equal($"{CamtVoorbeeld.Spaar}|R|X1", BankimportPlanner.Sleutel(bestand.Afschriften[0].Iban, t[0]));
        Assert.Equal($"{CamtVoorbeeld.Spaar}|D|20260301|250|7|E2E-1", BankimportPlanner.Sleutel(bestand.Afschriften[0].Iban, t[1]));
    }

    [Fact]
    public void Saldo_dat_niet_aansluit_geeft_melding_over_ontbrekend_afschrift()
    {
        var voorstel = BankimportPlanner.Plan(_data.Stand, [Bestand(CamtVoorbeeld.Betaal,
            Dag(1, 2, 1000, new VbTransactie(500, Ref: "A")),
            Dag(1, 5, 2000))]);

        Assert.Contains(voorstel.Rekeningen[0].Meldingen, m => m.Contains("ontbreekt", StringComparison.Ordinal) && m.Contains("05-01-2026", StringComparison.Ordinal));
    }

    [Fact]
    public void Transacties_buiten_een_open_boekjaar_worden_overgeslagen()
    {
        var bestand = Camt053Lezer.Lees(CamtVoorbeeld.Stroom(CamtVoorbeeld.Xml(CamtVoorbeeld.Betaal, "08",
            new VbAfschrift(new DateOnly(2025, 12, 31), 0, new VbTransactie(700, Ref: "OUD")),
            new VbAfschrift(new DateOnly(2026, 1, 1), 700, new VbTransactie(300, Ref: "NIEUW")))));

        var rekening = BankimportPlanner.Plan(_data.Stand, [bestand]).Rekeningen[0];

        Assert.Equal(1, rekening.BuitenBoekjaar);
        Assert.Equal("NIEUW", Assert.Single(rekening.Nieuw).Transactie.Bankreferentie);
        Assert.Contains(rekening.Meldingen, m => m.Contains("buiten een open boekjaar", StringComparison.Ordinal));
    }

    [Fact]
    public void Beginsaldo_wordt_vergeleken_met_de_beginbalans()
    {
        _data.Geboekt(new DateOnly(2026, 1, 1), "Openingsbalans 2026", PostSoort.Openingsbalans, (_data.Bank, 100000, 0), (_data.Reserve, 0, 100000));
        var rekening = BankimportPlanner.Plan(_data.Stand, [Bestand(CamtVoorbeeld.Betaal, Dag(1, 1, 100000), Dag(1, 2, 100000))]).Rekeningen[0];
        Assert.Null(BankimportPlanner.ControleerBeginsaldo(_data.Stand, _data.Bank, rekening));

        var afwijkend = BankimportPlanner.Plan(_data.Stand, [Bestand(CamtVoorbeeld.Betaal, Dag(1, 1, 99276))]).Rekeningen[0];
        var melding = BankimportPlanner.ControleerBeginsaldo(_data.Stand, _data.Bank, afwijkend);
        Assert.Contains("€ 992,76", melding, StringComparison.Ordinal);
        Assert.Contains("€ 1.000,00", melding, StringComparison.Ordinal);
    }
}
