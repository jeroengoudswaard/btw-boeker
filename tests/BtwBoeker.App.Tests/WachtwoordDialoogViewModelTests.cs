using BtwBoeker.App.ViewModels.Dialogen;

namespace BtwBoeker.App.Tests;

public sealed class WachtwoordDialoogViewModelTests
{
    [Fact]
    public void Nieuw_wachtwoord_moet_aan_beleid_voldoen()
    {
        var vm = new NieuwWachtwoordViewModel { Wachtwoord = "kort", Bevestiging = "kort" };

        Assert.False(vm.KanBevestigen);
        Assert.NotNull(vm.Fout);
    }

    [Fact]
    public void Nieuw_wachtwoord_en_bevestiging_moeten_gelijk_zijn()
    {
        var vm = new NieuwWachtwoordViewModel { Wachtwoord = "lang genoeg wachtwoord", Bevestiging = "lang genoeg wachtwoorD" };

        Assert.False(vm.KanBevestigen);
        Assert.Equal("De wachtwoorden komen niet overeen.", vm.Fout);
    }

    [Fact]
    public void Geldig_nieuw_wachtwoord_kan_bevestigd_worden()
    {
        var vm = new NieuwWachtwoordViewModel { Wachtwoord = "lang genoeg wachtwoord", Bevestiging = "lang genoeg wachtwoord" };

        Assert.True(vm.KanBevestigen);
        Assert.Null(vm.Fout);
    }

    [Fact]
    public void Herstel_vereist_geldige_herstelsleutel()
    {
        var vm = new HerstelViewModel
        {
            HerstelsleutelTekst = "ABCDE-FGHIJ",
            Wachtwoord = { Wachtwoord = "lang genoeg wachtwoord", Bevestiging = "lang genoeg wachtwoord" },
        };

        Assert.False(vm.KanBevestigen);
        Assert.NotNull(vm.Fout);
    }

    [Fact]
    public void Herstel_met_geldige_sleutel_en_wachtwoord_kan_bevestigd_worden()
    {
        var sleutel = Application.Beveiliging.Herstelsleutel.Genereer();
        var vm = new HerstelViewModel
        {
            HerstelsleutelTekst = sleutel.ToString().ToLowerInvariant(),
            Wachtwoord = { Wachtwoord = "lang genoeg wachtwoord", Bevestiging = "lang genoeg wachtwoord" },
        };

        Assert.True(vm.KanBevestigen);
        Assert.Equal(sleutel.ToString(), vm.MaakInvoer()!.Herstelsleutel.ToString());
    }
}
