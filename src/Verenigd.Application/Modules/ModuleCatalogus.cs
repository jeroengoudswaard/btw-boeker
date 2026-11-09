using Verenigd.Domain.Administraties;

namespace Verenigd.Application.Modules;

public enum ModuleSoort
{
    Overzicht = 0,
    Boekhouding = 3,
    Begroting = 4,
    Instellingen = 6,

}

/// <param name="Pictogram">Codepunt uit het lettertype Segoe Fluent Icons.</param>
/// <param name="Sneltoetsnummer">Cijfer voor Ctrl+1...6, of <c>null</c> als er geen sneltoets is.</param>
/// <param name="PerAdministratie">Hoort het tabblad bij één administratie (Vereniging of Stichting)?</param>
/// <param name="Fase">Ontwikkelfase waarin de module functioneel wordt.</param>
/// <param name="AlleenVoor">Als ingevuld: de module bestaat alleen voor deze administratie.</param>
/// <param name="Omschrijving">Eén zin uitleg onder de titel van de pagina (besluit 242).</param>
public sealed record ModuleDefinitie(
    ModuleSoort Soort,
    string Titel,
    string Pictogram,
    int? Sneltoetsnummer,
    bool PerAdministratie,
    bool KanSluiten,
    int Fase,
    AdministratieSoort? AlleenVoor = null,
    string Omschrijving = "");

public static class ModuleCatalogus
{
    public static IReadOnlyList<ModuleDefinitie> Alle { get; } =
    [
        new(ModuleSoort.Overzicht, "Overzicht", "", null, PerAdministratie: false, KanSluiten: false, Fase: 1,
            Omschrijving: "De stand van zaken en wat er te doen is."),
        new(ModuleSoort.Boekhouding, "Boekhouding", "", 1, PerAdministratie: true, KanSluiten: true, Fase: 4,
            Omschrijving: "Boekingen, bank, bonnen en overzichten van het jaar."),
        new(ModuleSoort.Begroting, "Begroting", "", 2, PerAdministratie: true, KanSluiten: true, Fase: 5,
            Omschrijving: "Wat u verwacht uit te geven en te ontvangen, naast wat er werkelijk gebeurt."),
        new(ModuleSoort.Instellingen, "Instellingen", "", null, PerAdministratie: false, KanSluiten: true, Fase: 1,
            Omschrijving: "Administraties en algemene instellingen."),
    ];

    public static ModuleDefinitie Geef(ModuleSoort soort) => Alle.Single(m => m.Soort == soort);
}
