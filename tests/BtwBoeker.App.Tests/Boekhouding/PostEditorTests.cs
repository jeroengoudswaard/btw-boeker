using BtwBoeker.App.ViewModels.Boekhouding;
using BtwBoeker.Domain.Administraties;
using BtwBoeker.Domain.Financien;
using BtwBoeker.Domain.Financien.Boekhouding;

namespace BtwBoeker.App.Tests.Boekhouding;

/// <summary>
/// Elke regel van de posteditor draagt de keuzelijst van zijn eigen editor. Zo wisselen lijst en keuze in de weergave
/// tegelijk; anders kon een hergebruikte rij de rekening leegmaken met de lijst van de vorige post, en vroeg de app bij
/// het sluiten om iets op te slaan dat alleen bekeken was.
/// </summary>
public sealed class PostEditorTests
{
    private static readonly Dagboek Memoriaal = new(AdministratieSoort.Standaard, DagboekSoort.Memoriaal, "MEM", "Memoriaal", null);

    private static List<Grootboekrekening> Rekeningen() =>
    [
        new(AdministratieSoort.Standaard, 1100, "Bank", RekeningSoort.Balans, null),
        new(AdministratieSoort.Standaard, 4200, "Energie", RekeningSoort.Resultaat, null),
    ];

    private static Journaalpost Concept(List<Grootboekrekening> rekeningen)
    {
        var post = new Journaalpost(AdministratieSoort.Standaard, Memoriaal, new DateOnly(2026, 10, 28), "Energie");
        post.Wijzig(Memoriaal, post.Datum, post.Omschrijving,
            [new RegelInvoer(rekeningen[1], null, new Bedrag(14500), Bedrag.Nul), new RegelInvoer(rekeningen[0], null, Bedrag.Nul, new Bedrag(14500))]);
        return post;
    }

    [Fact]
    public void Elke_regel_kiest_uit_de_rekeningen_van_zijn_eigen_editor()
    {
        var rekeningen = Rekeningen();
        var editor = new PostEditor(Concept(rekeningen), [Memoriaal], rekeningen, new DateOnly(2026, 11, 11));

        Assert.All(editor.Regels, r =>
        {
            Assert.Same(editor.Rekeningen, r.Rekeningen);
            Assert.Contains(r.GekozenRekening!, r.Rekeningen);
        });
        Assert.False(editor.IsGewijzigd);

        editor.NieuweRegelCommand.Execute(null);
        Assert.Same(editor.Rekeningen, editor.Regels[^1].Rekeningen);
    }

    [Fact]
    public void Na_herladen_kiest_een_regel_uit_de_nieuwe_lijst_en_niet_uit_de_oude()
    {
        var oud = new PostEditor(Concept(Rekeningen()), [Memoriaal], Rekeningen(), new DateOnly(2026, 11, 11));
        var herladen = Rekeningen(); // na opslaan elders: nieuwe objecten voor dezelfde rekeningen
        var nieuw = new PostEditor(Concept(herladen), [Memoriaal], herladen, new DateOnly(2026, 11, 11));

        Assert.NotSame(oud.Regels[0].Rekeningen, nieuw.Regels[0].Rekeningen);
        Assert.All(nieuw.Regels, r => Assert.Contains(r.GekozenRekening!, r.Rekeningen));
    }
}
