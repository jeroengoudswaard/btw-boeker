using Verenigd.Domain.Communicatie;

namespace Verenigd.Domain.Tests.Communicatie;

/// <summary>E-mail (besluiten 191–195 en 203–208): sjablonen, logboek en instellingen.</summary>
public sealed class EmailTests
{
    private static readonly DateTime Nu = new(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Sjabloon_heeft_naam_onderwerp_en_tekst_en_is_aan_te_passen()
    {
        var sjabloon = new Emailsjabloon(Emailsoort.Vrij, "Mededeling speltak", "Kamp", "Beste {aanhef},");

        sjabloon.Wijzig(" Kamp 2027 ", "Aanmelden kamp", "Beste {aanhef}, meld je aan.");

        Assert.Equal("Kamp 2027", sjabloon.Naam);
        Assert.Equal("Aanmelden kamp", sjabloon.Onderwerp);
        Assert.False(sjabloon.IsStandaard);
        Assert.Throws<ArgumentException>(() => sjabloon.Wijzig(" ", "x", "y"));
        Assert.Throws<ArgumentException>(() => sjabloon.Wijzig("x", " ", "y"));
    }

    [Fact]
    public void Standaardsjabloon_kan_wel_worden_aangepast()
    {
        var welkom = Emailsjabloon.Standaard(Emailsoort.Welkom, "Welkomstmail", "Welkom", "Beste {aanhef},");

        welkom.Wijzig("Welkomstmail", "Welkom bij de scouting", "Hallo");

        Assert.True(welkom.IsStandaard);
        Assert.Equal("Welkom bij de scouting", welkom.Onderwerp);
    }

    [Fact]
    public void Logboek_bewaart_geen_tekst_en_een_fout_zonder_adres()
    {
        var gelukt = VerzondenBericht.Gelukt("Anna de Vries", "anna@example.org", "Kamp", "Mededeling speltak", Nu);
        var mislukt = VerzondenBericht.Mislukt("Bo", "bo@example.org", "Kamp", "Mededeling speltak", "Ongeldig adres", Nu);

        Assert.Equal(Verzendstatus.Verzonden, gelukt.Status);
        Assert.Null(gelukt.Fout);
        Assert.Equal(Verzendstatus.Mislukt, mislukt.Status);
        Assert.Equal("Ongeldig adres", mislukt.Fout);
        Assert.DoesNotContain(typeof(VerzondenBericht).GetProperties(), p => p.Name is "Tekst" or "Inhoud"); // besluit 195
    }

    [Fact]
    public void Instellingen_zijn_gekoppeld_met_client_en_vernieuwingssleutel()
    {
        var instellingen = new Emailinstellingen();
        Assert.False(instellingen.IsGekoppeld);

        instellingen.ZetAfzender("Penningmeester Scouting", "penningmeester@example.org");
        instellingen.ZetClient("client-id", "geheim");
        instellingen.Koppel("vernieuwingssleutel", Nu);

        Assert.True(instellingen.IsGekoppeld);
        Assert.Equal(Nu, instellingen.GekoppeldOpUtc);
        Assert.Throws<ArgumentException>(() => instellingen.ZetAfzender("x", "geen-adres"));
        instellingen.Ontkoppel();
        Assert.False(instellingen.IsGekoppeld);
    }

    [Theory]
    [InlineData("anna@example.org", true)]
    [InlineData(" Anna@Example.org ", true)]
    [InlineData("anna@", false)]
    [InlineData("anna example.org", false)]
    [InlineData("", false)]
    public void Emailadres_herkent_een_geldig_adres(string adres, bool geldig) => Assert.Equal(geldig, Emailadres.IsGeldig(adres));
}
