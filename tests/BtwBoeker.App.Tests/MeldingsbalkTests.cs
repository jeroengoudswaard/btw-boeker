using BtwBoeker.App.ViewModels;

namespace BtwBoeker.App.Tests;

/// <summary>
/// UX-review stap 1 (besluit 32, overgenomen uit het moederproduct): de meldingsbalk vervangt de statusbalk; ✓ verdwijnt
/// vanzelf, ⚠ en ✕ blijven tot sluiten of een andere pagina.
/// </summary>
public sealed class MeldingsbalkTests
{
    private readonly HandmatigeTijd _tijd = new(new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Gelukt_verdwijnt_na_zes_seconden()
    {
        var balk = new Meldingsbalk(_tijd);

        balk.Toon(Statussoort.Gelukt, "Opgeslagen.");
        Assert.True(balk.IsZichtbaar);
        Assert.Equal("✓", balk.Teken);
        Assert.Equal("Opgeslagen.", balk.Tekst);

        _tijd.Verder(TimeSpan.FromSeconds(5));
        Assert.True(balk.IsZichtbaar);
        _tijd.Verder(TimeSpan.FromSeconds(1));
        Assert.False(balk.IsZichtbaar);
    }

    [Theory]
    [InlineData(Statussoort.Waarschuwing, "⚠")]
    [InlineData(Statussoort.Fout, "✕")]
    public void Waarschuwing_en_fout_blijven_tot_sluiten(Statussoort soort, string teken)
    {
        var balk = new Meldingsbalk(_tijd);

        balk.Toon(soort, "Let op");
        _tijd.Verder(TimeSpan.FromMinutes(5));
        Assert.True(balk.IsZichtbaar);
        Assert.Equal(teken, balk.Teken);

        balk.SluitCommand.Execute(null);
        Assert.False(balk.IsZichtbaar);
    }

    [Fact]
    public void Een_nieuwe_melding_krijgt_weer_de_volle_wachttijd()
    {
        var balk = new Meldingsbalk(_tijd);
        balk.Toon(Statussoort.Gelukt, "Eerste");
        _tijd.Verder(TimeSpan.FromSeconds(4));

        balk.Toon(Statussoort.Gelukt, "Tweede");
        _tijd.Verder(TimeSpan.FromSeconds(3));
        Assert.True(balk.IsZichtbaar);
        Assert.Equal("Tweede", balk.Tekst);

        _tijd.Verder(TimeSpan.FromSeconds(3));
        Assert.False(balk.IsZichtbaar);
    }

    [Fact]
    public void Een_waarschuwing_na_een_gelukt_wordt_niet_door_de_oude_timer_weggehaald()
    {
        var balk = new Meldingsbalk(_tijd);
        balk.Toon(Statussoort.Gelukt, "Opgeslagen.");
        balk.Toon(Statussoort.Waarschuwing, "Eén regel nog niet");

        _tijd.Verder(TimeSpan.FromSeconds(10));

        Assert.True(balk.IsZichtbaar);
        Assert.Equal(Statussoort.Waarschuwing, balk.Soort);
    }

    [Fact]
    public void Een_gewone_melding_toont_niets()
    {
        var balk = new Meldingsbalk(_tijd);
        balk.Toon(Statussoort.Fout, "Mislukt");

        balk.Toon(Statussoort.Info, "Gereed");

        Assert.False(balk.IsZichtbaar);
    }

    [Fact]
    public void Een_andere_pagina_haalt_waarschuwing_en_fout_weg_maar_niet_gelukt()
    {
        var balk = new Meldingsbalk(_tijd);
        balk.Toon(Statussoort.Waarschuwing, "Let op");
        balk.AnderePagina();
        Assert.False(balk.IsZichtbaar);

        balk.Toon(Statussoort.Gelukt, "Back-up opgeslagen.");
        balk.AnderePagina();
        Assert.True(balk.IsZichtbaar);
    }
}
