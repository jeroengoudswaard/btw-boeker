using BtwBoeker.App.ViewModels.Boekhouding;
using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.App.Tests.Boekhouding;

/// <summary>Jaarrekening volgens de eigen indeling en het venster Indeling bewerken.</summary>
public sealed class JaarrekeningViewModelTests : IDisposable
{
    private readonly BoekhoudingTestomgeving _omgeving = new();

    public void Dispose() => _omgeving.Dispose();

    [Fact]
    public async Task Jaarrekening_staat_bij_de_rapporten_met_rekeningen_tonen()
    {
        var vm = await _omgeving.OpenIngerichtAsync();
        Assert.False(vm.IsJaarrekening);

        vm.Rapportsoort = Rapportsoort.JaarrekeningBalans;

        Assert.True(vm.IsJaarrekening);
        Assert.Equal("Balans per 31-12-2026", vm.Rapport?.Titel);
        Assert.Null(vm.JaarrekeningMelding);
        var met = vm.Rapport!.Rijen.Count;
        vm.RekeningenTonen = false;
        Assert.True(vm.Rapport!.Rijen.Count < met);
        Assert.Contains(vm.Rapportkeuzes, k => k.Soort == Rapportsoort.JaarrekeningBatenLasten && k.Naam == "Jaarrekening: baten en lasten");
    }

    [Fact]
    public async Task Indeling_bewerken_slaat_op_en_meldt_niet_ingedeelde_rekeningen()
    {
        var vm = await _omgeving.OpenIngerichtAsync();
        vm.Rapportsoort = Rapportsoort.JaarrekeningBalans;
        _omgeving.Dialogen.IndelingActie = editor =>
        {
            // Alleen het vermogen indelen: kas en bank vallen dan nergens onder.
            while (editor.Regels.Any(r => r.Tekst != "Vermogen"))
            {
                editor.Geselecteerd = editor.Regels.First(r => r.Tekst != "Vermogen");
                editor.VerwijderCommand.Execute(null);
            }

            editor.OpslaanCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            return editor.IsOpgeslagen;
        };

        await vm.IndelingBewerkenCommand.ExecuteAsync(null);

        var editor = Assert.Single(_omgeving.Dialogen.Indelingen);
        Assert.Equal(JaarrekeningRapport.Balans, editor.Rapport);
        Assert.Single(_omgeving.Opslag.Van(AdministratieSoort.Standaard).OfType<Jaarrekeningindeling>());
        Assert.StartsWith("✓", vm.Melding, StringComparison.Ordinal);
        Assert.Contains("1000, 1100", vm.JaarrekeningMelding, StringComparison.Ordinal);
        Assert.Contains(vm.Rapport!.Rijen, r => (string)r.Cellen[0]! == "⚠ " + Jaarrekeningrekenaar.NietIngedeeldTekst);
    }

    [Fact]
    public async Task Annuleren_slaat_niets_op()
    {
        var vm = await _omgeving.OpenIngerichtAsync();
        vm.Rapportsoort = Rapportsoort.JaarrekeningBatenLasten;

        await vm.IndelingBewerkenCommand.ExecuteAsync(null);

        Assert.Single(_omgeving.Dialogen.Indelingen);
        Assert.Empty(_omgeving.Opslag.Van(AdministratieSoort.Standaard).OfType<Jaarrekeningindeling>());
    }

    [Fact]
    public async Task Editor_begint_met_een_regel_per_rubriek_en_houdt_rubrieken_per_deel_uniek()
    {
        var vm = await _omgeving.OpenIngerichtAsync();
        var stand = await _omgeving.Sessie.Boekhouding.LaadAsync(AdministratieSoort.Standaard);
        var editor = new JaarrekeningIndelingViewModel(new JaarrekeningDienst(_omgeving.Sessie), stand, JaarrekeningRapport.BatenEnLasten);

        Assert.Equal(["Verhuur", "Parkkosten"], editor.Regels.Select(r => r.Tekst).Order(StringComparer.Ordinal).Reverse());
        Assert.Equal([JaarrekeningAfdeling.Baten, JaarrekeningAfdeling.Lasten], editor.Afdelingen.Select(a => a.Waarde));

        // Een nieuwe regel in hetzelfde deel: de rubriek van de bestaande regel is daar grijs.
        var verhuurRegel = editor.Regels.Single(r => r.Tekst == "Verhuur");
        editor.Geselecteerd = verhuurRegel;
        editor.NieuweRegelCommand.Execute(null);
        var nieuw = editor.Geselecteerd!;
        Assert.Equal(verhuurRegel.Afdeling, nieuw.Afdeling);
        Assert.False(nieuw.Rubrieken.Single(r => r.Naam == "Verhuur").KanKiezen);
        Assert.True(nieuw.Rubrieken.Single(r => r.Naam == "Parkkosten").KanKiezen);

        // Een lege regel wordt niet opgeslagen; de melding zegt wat er mis is.
        await editor.OpslaanCommand.ExecuteAsync(null);
        Assert.False(editor.IsOpgeslagen);
        Assert.StartsWith("✕", editor.Fout, StringComparison.Ordinal);
        Assert.Contains("kies minstens één rubriek", editor.Fout, StringComparison.Ordinal);

        nieuw.Rubrieken.Single(r => r.Naam == "Parkkosten").IsGekozen = true;
        Assert.True(nieuw.Rubrieken.Single(r => r.Naam == "Parkkosten").IsGekozen);
        nieuw.Soort = IndelingsregelSoort.Kop;
        nieuw.Rubrieken.Single(r => r.Naam == "Parkkosten").IsGekozen = false;
        nieuw.Tekst = "Opbrengsten";
        editor.OmhoogCommand.Execute(null);
        Assert.Equal(0, editor.Regels.IndexOf(nieuw));
        Assert.False(editor.OmhoogCommand.CanExecute(null));

        await editor.OpslaanCommand.ExecuteAsync(null);
        Assert.True(editor.IsOpgeslagen);
        Assert.Null(editor.Fout);
        stand = await _omgeving.Sessie.Boekhouding.LaadAsync(AdministratieSoort.Standaard);
        Assert.Equal("Opbrengsten", stand.Jaarrekeningindelingen.Single().Regels[0].Tekst);
        Assert.NotNull(vm);
    }

    [Fact]
    public async Task Regel_naar_een_ander_deel_sluit_daar_achteraan_aan()
    {
        await _omgeving.OpenIngerichtAsync();
        var stand = await _omgeving.Sessie.Boekhouding.LaadAsync(AdministratieSoort.Standaard);
        var editor = new JaarrekeningIndelingViewModel(new JaarrekeningDienst(_omgeving.Sessie), stand, JaarrekeningRapport.Balans);
        var eerste = editor.Regels[0];
        Assert.Equal(JaarrekeningAfdeling.Activa, eerste.Afdeling);

        eerste.Afdeling = JaarrekeningAfdeling.Passiva;

        Assert.Same(eerste, editor.Regels[^1]);
        Assert.Same(eerste, editor.Geselecteerd);
        Assert.Contains("Passiva", eerste.Omschrijving, StringComparison.Ordinal);
    }
}
