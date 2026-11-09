using BtwBoeker.Application.Bank;
using BtwBoeker.Application.Tests.Boekhouding;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Bank;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.Application.Tests.Bank;

public class BankherkenningTests
{
    private readonly BoekhoudingTestdata _data = new();
    private readonly Dagboek _rabo;

    public BankherkenningTests()
    {
        _rabo = _data.Bankdagboek("RABO", CamtVoorbeeld.Betaal);
    }

    private Bankmutatie Mutatie(string? tegenIban = CamtVoorbeeld.Tegen, string? naam = "Energie BV", bool bij = false) =>
        new(BoekhoudingTestdata.Ver, _rabo, Guid.NewGuid().ToString(), new DateOnly(2026, 2, 1), new Bedrag(4500), bij,
            new BankTransactiecode("PMNT", "ICDT", "ESCT", "520", "RABOBANK"), tegenIban, naam, "Factuur", null, null, null);

    [Fact]
    public void Overboeking_naar_eigen_rekening_gaat_via_kruisposten()
    {
        var kruisposten = _data.Rekening(1298, "Kruisposten", RekeningSoort.Balans, null, RekeningRol.Kruisposten);
        var spaar = _data.Rekening(1110, "Spaarrekening", RekeningSoort.Balans, null);
        _data.Bankdagboek("RSPA", CamtVoorbeeld.Spaar, spaar);

        var herkenning = Bankherkenning.Bepaal(_data.Stand, Mutatie(CamtVoorbeeld.Spaar));

        Assert.Same(kruisposten, herkenning?.Rekening);
        Assert.Contains("RSPA", herkenning?.Omschrijving, StringComparison.Ordinal);
    }

    [Fact]
    public void Eerste_passende_actieve_regel_wint()
    {
        var uit = new Boekingsregel(BoekhoudingTestdata.Ver, "Uit", _data.Welpen, new BoekingsregelVoorwaarden(NaamBevat: "Energie"), volgorde: 1);
        uit.Deactiveer();
        _data.Boekingsregels.Add(uit);
        _data.Boekingsregels.Add(new Boekingsregel(BoekhoudingTestdata.Ver, "Energie", _data.Energie, new BoekingsregelVoorwaarden(NaamBevat: "energie"), "Energie", 2));
        _data.Boekingsregels.Add(new Boekingsregel(BoekhoudingTestdata.Ver, "Alles af", _data.Welpen, new BoekingsregelVoorwaarden(Richting: Richting.Af), volgorde: 3));

        var herkenning = Bankherkenning.Bepaal(_data.Stand, Mutatie());

        Assert.Same(_data.Energie, herkenning?.Rekening);
        Assert.Equal("Energie", herkenning?.Omschrijving);
        Assert.Equal("Energie", herkenning?.Bron);
    }

    [Fact]
    public void Zonder_passende_regel_geen_herkenning()
    {
        Assert.Null(Bankherkenning.Bepaal(_data.Stand, Mutatie()));
    }
}
