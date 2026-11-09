using BtwBoeker.Application.Beveiliging;
using BtwBoeker.Infrastructure.Beveiliging;

namespace BtwBoeker.Infrastructure.Tests;

public class SleutelKluisTests
{
    private const string Wachtwoord = "correct paard batterij";

    [Fact]
    public void Maak_geeft_willekeurige_databasesleutel_van_256_bits()
    {
        var (sleutel1, _) = SleutelKluis.Maak(Wachtwoord, Herstelsleutel.Genereer());
        var (sleutel2, _) = SleutelKluis.Maak(Wachtwoord, Herstelsleutel.Genereer());

        Assert.Equal(32, sleutel1.Length);
        Assert.NotEqual(sleutel1, sleutel2);
    }

    [Fact]
    public void Ontsluiten_met_juist_wachtwoord_geeft_dezelfde_sleutel()
    {
        var (sleutel, envelop) = SleutelKluis.Maak(Wachtwoord, Herstelsleutel.Genereer());

        Assert.Equal(sleutel, SleutelKluis.OntsluitMetWachtwoord(envelop, Wachtwoord));
    }

    [Fact]
    public void Ontsluiten_met_onjuist_wachtwoord_faalt()
    {
        var (_, envelop) = SleutelKluis.Maak(Wachtwoord, Herstelsleutel.Genereer());

        Assert.Throws<OngeldigeSleutelException>(() => SleutelKluis.OntsluitMetWachtwoord(envelop, "verkeerd wachtwoord"));
    }

    [Fact]
    public void Ontsluiten_met_herstelsleutel_geeft_dezelfde_sleutel()
    {
        var herstel = Herstelsleutel.Genereer();
        var (sleutel, envelop) = SleutelKluis.Maak(Wachtwoord, herstel);

        Assert.Equal(sleutel, SleutelKluis.OntsluitMetHerstelsleutel(envelop, herstel));
    }

    [Fact]
    public void Ontsluiten_met_andere_herstelsleutel_faalt()
    {
        var (_, envelop) = SleutelKluis.Maak(Wachtwoord, Herstelsleutel.Genereer());

        Assert.Throws<OngeldigeSleutelException>(
            () => SleutelKluis.OntsluitMetHerstelsleutel(envelop, Herstelsleutel.Genereer()));
    }

    [Fact]
    public void VervangWachtwoord_maakt_oud_wachtwoord_ongeldig_en_behoudt_herstelsleutel()
    {
        var herstel = Herstelsleutel.Genereer();
        var (sleutel, envelop) = SleutelKluis.Maak(Wachtwoord, herstel);

        var nieuw = SleutelKluis.VervangWachtwoord(envelop, sleutel, "een heel nieuw wachtwoord");

        Assert.Throws<OngeldigeSleutelException>(() => SleutelKluis.OntsluitMetWachtwoord(nieuw, Wachtwoord));
        Assert.Equal(sleutel, SleutelKluis.OntsluitMetWachtwoord(nieuw, "een heel nieuw wachtwoord"));
        Assert.Equal(sleutel, SleutelKluis.OntsluitMetHerstelsleutel(nieuw, herstel));
    }

    [Fact]
    public void Sleutelbestand_overleeft_opslaan_en_inlezen()
    {
        using var map = new TijdelijkeMap();
        var (sleutel, envelop) = SleutelKluis.Maak(Wachtwoord, Herstelsleutel.Genereer());
        var pad = map.Bestand("test.btwdb.sleutel");

        Sleutelbestand.Schrijf(pad, envelop);
        var gelezen = Sleutelbestand.Lees(pad);

        Assert.Equal(sleutel, SleutelKluis.OntsluitMetWachtwoord(gelezen, Wachtwoord));
    }

    [Fact]
    public void Sleutelbestand_bevat_geen_wachtwoord_of_sleutel_in_leesbare_vorm()
    {
        using var map = new TijdelijkeMap();
        var (sleutel, envelop) = SleutelKluis.Maak(Wachtwoord, Herstelsleutel.Genereer());
        var pad = map.Bestand("test.btwdb.sleutel");

        Sleutelbestand.Schrijf(pad, envelop);
        var inhoud = File.ReadAllText(pad);

        Assert.DoesNotContain(Wachtwoord, inhoud, StringComparison.Ordinal);
        Assert.DoesNotContain(Convert.ToBase64String(sleutel), inhoud, StringComparison.Ordinal);
        Assert.DoesNotContain(Convert.ToHexString(sleutel), inhoud, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Beschadigd_sleutelbestand_geeft_duidelijke_fout()
    {
        using var map = new TijdelijkeMap();
        var pad = map.Bestand("kapot.btwdb.sleutel");
        File.WriteAllText(pad, "{ dit is geen sleutelbestand");

        Assert.Throws<Application.Database.DatabaseBestandException>(() => Sleutelbestand.Lees(pad));
    }
}
