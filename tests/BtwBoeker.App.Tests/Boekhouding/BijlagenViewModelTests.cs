using System.Text;
using BtwBoeker.App.ViewModels;
using BtwBoeker.App.ViewModels.Boekhouding;
using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien;

namespace BtwBoeker.App.Tests.Boekhouding;

/// <summary>Bijlagen bij een post, het postvak bonnen en het filter "Zonder bon".</summary>
public sealed class BijlagenViewModelTests : IDisposable
{
    private const AdministratieSoort Adm = AdministratieSoort.Standaard;
    private readonly BoekhoudingTestomgeving _omgeving = new();
    private readonly List<string> _bestanden = [];

    public void Dispose()
    {
        foreach (var pad in _bestanden)
        {
            File.Delete(pad);
        }

        _omgeving.Dispose();
    }

    private string Bestand(string naam, byte[] inhoud)
    {
        var map = Path.Combine(Path.GetTempPath(), $"bijlagen-{Guid.NewGuid():N}");
        Directory.CreateDirectory(map);
        var pad = Path.Combine(map, naam);
        File.WriteAllBytes(pad, inhoud);
        _bestanden.Add(pad);
        return pad;
    }

    private static byte[] Pdf(string tekst) => Encoding.ASCII.GetBytes("%PDF-1.7\n" + tekst);

    private bool _ingericht;

    private async Task<BoekhoudingDienst> InrichtenAsync()
    {
        var dienst = new BoekhoudingDienst(_omgeving.Sessie, _omgeving.Tijd);
        if (!_ingericht)
        {
            await dienst.ImporteerAsync(Adm, Application.Tests.Boekhouding.Testvoorstel.Standaard());
            _ingericht = true;
        }

        return dienst;
    }

    /// <summary>Een kasuitgave (Energie aan Kas) van <paramref name="centen"/>.</summary>
    private async Task<Guid> KasuitgaveAsync(long centen, DateOnly datum, bool boeken = true)
    {
        var dienst = await InrichtenAsync();
        var stand = await dienst.LaadAsync(Adm);
        var invoer = new PostInvoer(null, stand.Dagboeken.Single(d => d.Code == "KAS").Id, datum, "Boodschappen",
        [
            new RegelInvoerId(stand.Rekeningen.Single(r => r.Nummer == 4115).Id, null, new Bedrag(centen), Bedrag.Nul),
            new RegelInvoerId(stand.Rekeningen.Single(r => r.Nummer == 1000).Id, null, Bedrag.Nul, new Bedrag(centen)),
        ]);
        var id = await dienst.BewaarConceptAsync(Adm, invoer);
        if (boeken)
        {
            await dienst.BoekAsync(Adm, invoer with { Id = id });
        }

        return id;
    }

    private async Task<BoekhoudingViewModel> OpenAsync()
    {
        await InrichtenAsync();
        var vm = _omgeving.Maak();
        await vm.VernieuwCommand.ExecuteAsync(null);
        return vm;
    }

    [Fact]
    public async Task Bijlage_toevoegen_aan_een_geboekte_post_toont_hem_in_het_blok_en_de_paperclip()
    {
        var post = await KasuitgaveAsync(2345, new DateOnly(2026, 6, 10));
        var vm = await OpenAsync();
        await vm.VernieuwCommand.ExecuteAsync(null);
        vm.GeselecteerdePost = vm.Posten.Single(p => p.Id == post);
        Assert.False(vm.GeselecteerdePost.HeeftBijlage);
        _omgeving.Dialogen.Bijlagen = [Bestand("bon.pdf", Pdf("Totaal 23,45"))];

        await vm.BijlageToevoegenCommand.ExecuteAsync(null);

        var bijlage = Assert.Single(vm.Postbijlagen);
        Assert.Equal("bon.pdf", bijlage.Bestandsnaam);
        Assert.True(vm.Posten.Single(p => p.Id == post).HeeftBijlage);
        Assert.Equal(post, vm.GeselecteerdePost?.Id);
    }

    [Fact]
    public async Task Slepen_voegt_toe_en_meldt_overgeslagen_bestanden()
    {
        var post = await KasuitgaveAsync(2345, new DateOnly(2026, 6, 10));
        var vm = await OpenAsync();
        vm.GeselecteerdePost = vm.Posten.Single(p => p.Id == post);

        await vm.BijlagenSlepenCommand.ExecuteAsync([Bestand("bon.png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1]), Bestand("notitie.txt", "tekst"u8.ToArray())]);

        Assert.Single(vm.Postbijlagen);
        Assert.Contains("notitie.txt", vm.Melding, StringComparison.Ordinal);
        Assert.Contains("PDF, JPG of PNG", vm.Melding, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Een_nieuwe_post_krijgt_pas_bijlagen_na_opslaan()
    {
        var vm = await OpenAsync();
        vm.NieuwePostCommand.Execute(null);

        Assert.False(vm.BijlageToevoegenCommand.CanExecute(null));
        Assert.False(vm.KanBijlagenToevoegen);
    }

    [Fact]
    public async Task Verwijderen_vraagt_bevestiging_en_bekijken_en_opslaan_gebruiken_de_inhoud()
    {
        var post = await KasuitgaveAsync(2345, new DateOnly(2026, 6, 10));
        var vm = await OpenAsync();
        vm.GeselecteerdePost = vm.Posten.Single(p => p.Id == post);
        var inhoud = Pdf("bewaren");
        _omgeving.Dialogen.Bijlagen = [Bestand("bon.pdf", inhoud)];
        await vm.BijlageToevoegenCommand.ExecuteAsync(null);
        var regel = Assert.Single(vm.Postbijlagen);

        await vm.BijlageBekijkenCommand.ExecuteAsync(regel);
        var voorbeeld = Assert.IsType<BijlageVoorbeeldViewModel>(_omgeving.Dialogen.GetoondeBijlage);
        Assert.Equal(inhoud, voorbeeld.Inhoud);
        Assert.True(voorbeeld.IsPdf);

        var doel = Path.Combine(Path.GetTempPath(), $"export-{Guid.NewGuid():N}.pdf");
        _bestanden.Add(doel);
        _omgeving.Dialogen.BijlageDoel = doel;
        await voorbeeld.OpslaanAlsCommand.ExecuteAsync(null);
        Assert.Equal(inhoud, await File.ReadAllBytesAsync(doel));

        _omgeving.Dialogen.BevestigAntwoord = false;
        await vm.BijlageVerwijderenCommand.ExecuteAsync(regel);
        Assert.Single(vm.Postbijlagen);

        _omgeving.Dialogen.BevestigAntwoord = true;
        await vm.BijlageVerwijderenCommand.ExecuteAsync(regel);
        Assert.Empty(vm.Postbijlagen);
        Assert.False(vm.Posten.Single(p => p.Id == post).HeeftBijlage);
    }

    [Fact]
    public async Task Filter_zonder_bon_toont_kasuitgaven_boven_10_euro_zonder_bijlage()
    {
        var groot = await KasuitgaveAsync(2500, new DateOnly(2026, 6, 1));
        await KasuitgaveAsync(900, new DateOnly(2026, 6, 2));
        var vm = await OpenAsync();
        var aantal = vm.Posten.Count;

        vm.AlleenZonderBon = true;

        Assert.Equal([groot], vm.Posten.Select(p => p.Id));
        vm.AlleenZonderBon = false;
        Assert.Equal(aantal, vm.Posten.Count);
    }

    [Fact]
    public async Task Filter_zonder_bon_volgt_de_drempel_van_de_administratie()
    {
        await KasuitgaveAsync(2500, new DateOnly(2026, 6, 1));
        await KasuitgaveAsync(900, new DateOnly(2026, 6, 2));
        await InrichtenAsync();
        var model = new Administratie(AdministratieSoort.Standaard, "Mijn administratie");
        model.WijzigZonderBonDrempel(500);
        var vm = _omgeving.Maak(new AdministratieViewModel(model));
        await vm.VernieuwCommand.ExecuteAsync(null);

        vm.AlleenZonderBon = true;

        Assert.Equal(2, vm.Posten.Count); // ook € 9,00 ligt boven de drempel van € 5
    }

    [Fact]
    public async Task Postvak_stelt_een_post_voor_en_koppelt()
    {
        var post = await KasuitgaveAsync(2345, new DateOnly(2026, 6, 10));
        var vm = await OpenAsync();
        _omgeving.Dialogen.Bijlagen = [Bestand("foto.jpg", [0xFF, 0xD8, 0xFF, 1, 2])];

        await vm.BonnenToevoegenCommand.ExecuteAsync(null);

        var bon = Assert.Single(vm.Postvak);
        Assert.Null(bon.GekozenPost);
        bon.Datum = new DateTime(2026, 6, 12);
        bon.Bedragtekst = "23,45";
        Assert.Equal(post, bon.GekozenPost?.Post.Id);
        Assert.True(bon.GekozenPost!.IsVoorstel);
        vm.GeselecteerdeBon = bon;

        await vm.BonKoppelenCommand.ExecuteAsync(null);

        Assert.Empty(vm.Postvak);
        Assert.True(vm.Posten.Single(p => p.Id == post).HeeftBijlage);
        var opgeslagen = Assert.Single((await _omgeving.Opslag.LaadAsync(Adm)).Bijlagen);
        Assert.Equal(new Bedrag(2345), opgeslagen.Bedrag);
        Assert.Equal(new DateOnly(2026, 6, 12), opgeslagen.Datum);
    }

    [Fact]
    public async Task Postvak_accepteert_een_negatief_bedrag_en_stelt_een_ontvangst_voor()
    {
        var dienst = await InrichtenAsync();
        var stand = await dienst.LaadAsync(Adm);
        var invoer = new PostInvoer(null, stand.Dagboeken.Single(d => d.Code == "KAS").Id, new DateOnly(2026, 6, 10), "Retour",
        [
            new RegelInvoerId(stand.Rekeningen.Single(r => r.Nummer == 1000).Id, null, new Bedrag(1500), Bedrag.Nul),
            new RegelInvoerId(stand.Rekeningen.Single(r => r.Nummer == 4115).Id, null, Bedrag.Nul, new Bedrag(1500)),
        ]);
        var retour = await dienst.BewaarConceptAsync(Adm, invoer);
        var vm = await OpenAsync();
        await vm.BonnenSlepenCommand.ExecuteAsync([Bestand("creditnota.jpg", [0xFF, 0xD8, 0xFF, 9])]);
        var bon = Assert.Single(vm.Postvak);

        bon.Datum = new DateTime(2026, 6, 11);
        bon.Bedragtekst = "-15,00";

        Assert.Null(bon.Fout);
        Assert.Equal(new Bedrag(-1500), bon.Bedrag);
        Assert.True(bon.IsNegatief);
        Assert.Equal(retour, bon.GekozenPost?.Post.Id);
    }

    [Fact]
    public async Task Postvak_bon_verwijderen_na_bevestiging()
    {
        var vm = await OpenAsync();
        await vm.BonnenSlepenCommand.ExecuteAsync([Bestand("bon.pdf", Pdf("x"))]);
        vm.GeselecteerdeBon = Assert.Single(vm.Postvak);

        await vm.BonVerwijderenCommand.ExecuteAsync(null);

        Assert.Empty(vm.Postvak);
        Assert.Contains(_omgeving.Dialogen.Bevestigingsvragen, v => v.Contains("bon.pdf", StringComparison.Ordinal));
    }

    [Fact]
    public async Task In_eenvoudig_heet_een_bijlage_een_bon()
    {
        var vm = await OpenAsync();

        vm.Woorden.Weergave = Application.Instellingen.Weergavemodus.Eenvoudig;

        Assert.Equal("Bonnen", vm.Woorden["Bijlagen"]);
        Assert.Equal("Bijlagen", new Woordenboek()["Bijlagen"]);
    }
}
