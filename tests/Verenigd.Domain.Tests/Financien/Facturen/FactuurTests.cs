using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Boekhouding;
using Verenigd.Domain.Financien.Facturen;
using Verenigd.Domain.Financien.Relaties;

namespace Verenigd.Domain.Tests.Financien.Facturen;

public class FactuurTests
{
    private const AdministratieSoort Ver = AdministratieSoort.Vereniging;
    private static readonly Grootboekrekening Verhuur = new(Ver, 8400, "Verhuur", RekeningSoort.Resultaat, null);
    private static readonly DateOnly Datum = new(2026, 11, 1);

    private static Relatie Kerk() => new(new RelatieGegevens("Stichting De Kerk", "J. Jansen", "Kerkstraat 1", "1234 AB", "Dorp",
        "penningmeester@example.org", null, "NL91ABNA0417164300", IsDebiteur: true, IsCrediteur: false, null));

    private static Verkoopfactuur Concept(params (string Omschrijving, decimal Aantal, long Prijs)[] regels)
    {
        var factuur = new Verkoopfactuur(Ver, Kerk(), Datum, 14);
        factuur.Wijzig(Kerk(), Datum, 14, regels.Select(r => new FactuurregelInvoer(r.Omschrijving, r.Aantal, new Bedrag(r.Prijs), Verhuur)));
        return factuur;
    }

    [Fact]
    public void Relatie_valideert_naam_iban_email_en_soort()
    {
        var kerk = Kerk();
        Assert.Equal(["J. Jansen", "Kerkstraat 1", "1234 AB  Dorp"], kerk.Adresregels);
        Assert.Throws<ArgumentException>(() => new Relatie(new RelatieGegevens(" ", null, null, null, null, null, null, null, true, false, null)));
        Assert.Throws<ArgumentException>(() => new Relatie(new RelatieGegevens("X", null, null, null, null, null, null, "NL00ABNA0417164300", true, false, null)));
        Assert.Throws<ArgumentException>(() => new Relatie(new RelatieGegevens("X", null, null, null, null, "geen-mail", null, null, true, false, null)));
        Assert.Throws<ArgumentException>(() => new Relatie(new RelatieGegevens("X", null, null, null, null, null, null, null, false, false, null)));
        Assert.Equal("NL91ABNA0417164300", new Relatie(new RelatieGegevens("X", null, null, null, null, null, null, "nl91 abna 0417 1643 00", true, false, null)).Iban);
    }

    [Fact]
    public void Concept_rekent_regels_en_totaal_in_hele_centen()
    {
        var factuur = Concept(("Zaalhuur", 1, 25000), ("Schoonmaak", 2.5m, 1999));

        Assert.Equal(FactuurStatus.Concept, factuur.Status);
        Assert.Null(factuur.Nummer);
        Assert.Equal(new Bedrag(4998), factuur.Regels[1].Bedrag); // 2,5 × 19,99 = 49,975 → 49,98
        Assert.Equal(new Bedrag(29998), factuur.Totaal);
        Assert.Equal(new DateOnly(2026, 11, 15), factuur.Vervaldatum);
        Assert.Empty(factuur.Controleer());
    }

    [Fact]
    public void Controle_weigert_lege_en_nulfacturen()
    {
        Assert.NotEmpty(Concept().Controleer());
        Assert.NotEmpty(Concept(("Niets", 1, 0)).Controleer());
        Assert.NotEmpty(Concept((" ", 1, 1000)).Controleer());
    }

    [Fact]
    public void Definitief_geeft_nummer_met_voorvoegsel_en_bevriest_de_ontvanger()
    {
        var factuur = Concept(("Zaalhuur", 1, 25000));
        var post = Guid.NewGuid();

        factuur.MaakDefinitief(3, Kerk(), post);

        Assert.Equal(FactuurStatus.Definitief, factuur.Status);
        Assert.Equal("V2026-003", factuur.Nummer);
        Assert.Equal(2026, factuur.Jaar);
        Assert.Equal(post, factuur.PostId);
        Assert.StartsWith("Stichting De Kerk", factuur.Ontvanger, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => factuur.Wijzig(Kerk(), Datum, 14, []));
        Assert.Equal("S", Verkoopfactuur.Voorvoegsel(AdministratieSoort.Stichting));
    }

    [Fact]
    public void Creditfactuur_spiegelt_de_regels_van_een_definitieve_factuur()
    {
        var factuur = Concept(("Zaalhuur", 1, 25000));
        Assert.Throws<InvalidOperationException>(() => factuur.MaakCreditfactuur(Datum));
        factuur.MaakDefinitief(1, Kerk(), Guid.NewGuid());

        var credit = factuur.MaakCreditfactuur(new DateOnly(2026, 11, 20));

        Assert.True(credit.IsCreditfactuur);
        Assert.Equal(factuur.Id, credit.CreditVan);
        Assert.Equal(new Bedrag(-25000), credit.Totaal);
        Assert.Equal(FactuurStatus.Concept, credit.Status);
        Assert.Empty(credit.Controleer());
        Assert.Contains("V2026-001", credit.Regels[0].Omschrijving, StringComparison.Ordinal);
    }

    [Fact]
    public void Factuurinstellingen_accepteren_alleen_png_of_jpg_logo()
    {
        var instellingen = new Factuurinstellingen(Ver);
        Assert.Equal(14, instellingen.Termijn);

        instellingen.ZetLogo([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0]);
        Assert.Equal("image/png", instellingen.LogoType);
        instellingen.ZetLogo([0xFF, 0xD8, 0xFF, 0xE0, 0, 0]);
        Assert.Equal("image/jpeg", instellingen.LogoType);
        Assert.Throws<ArgumentException>(() => instellingen.ZetLogo([0x47, 0x49, 0x46, 0x38]));
        Assert.Throws<ArgumentException>(() => instellingen.ZetLogo(new byte[Factuurinstellingen.MaximaleLogoGrootte + 1]));
        instellingen.ZetLogo(null);
        Assert.Null(instellingen.Logo);
    }

    [Fact]
    public void Epc_qr_volgt_het_formaat_van_de_european_payments_council()
    {
        var inhoud = EpcQr.Inhoud("Scouting Testgroep", Iban.Maak("NL91ABNA0417164300"), new Bedrag(25000), "V2026-001");

        Assert.Equal("BCD\n002\n1\nSCT\n\nScouting Testgroep\nNL91ABNA0417164300\nEUR250.00\n\n\nV2026-001", inhoud);
        Assert.Throws<ArgumentOutOfRangeException>(() => EpcQr.Inhoud("X", Iban.Maak("NL91ABNA0417164300"), Bedrag.Nul, "V"));
    }
}
