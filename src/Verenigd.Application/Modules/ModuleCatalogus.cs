using Verenigd.Domain.Administraties;

namespace Verenigd.Application.Modules;

public enum ModuleSoort
{
    Overzicht,
    Leden,
    Contributie,
    Boekhouding,
    Begroting,
    Email,
    Instellingen,
}

/// <param name="Pictogram">Codepunt uit het lettertype Segoe Fluent Icons.</param>
/// <param name="Sneltoetsnummer">Cijfer voor Ctrl+1...6, of <c>null</c> als er geen sneltoets is.</param>
/// <param name="PerAdministratie">Hoort het tabblad bij één administratie (Vereniging of Stichting)?</param>
/// <param name="Fase">Ontwikkelfase waarin de module functioneel wordt.</param>
/// <param name="AlleenVoor">Als ingevuld: de module bestaat alleen voor deze administratie.</param>
public sealed record ModuleDefinitie(
    ModuleSoort Soort,
    string Titel,
    string Pictogram,
    int? Sneltoetsnummer,
    bool PerAdministratie,
    bool KanSluiten,
    int Fase,
    AdministratieSoort? AlleenVoor = null);

public static class ModuleCatalogus
{
    public static IReadOnlyList<ModuleDefinitie> Alle { get; } =
    [
        new(ModuleSoort.Overzicht, "Overzicht", "", 1, PerAdministratie: false, KanSluiten: false, Fase: 1),
        new(ModuleSoort.Leden, "Leden", "", 2, PerAdministratie: false, KanSluiten: true, Fase: 2),
        new(ModuleSoort.Contributie, "Contributie", "", 3, PerAdministratie: true, KanSluiten: true, Fase: 3,
            AlleenVoor: AdministratieSoort.Vereniging),
        new(ModuleSoort.Boekhouding, "Boekhouding", "", 4, PerAdministratie: true, KanSluiten: true, Fase: 4),
        new(ModuleSoort.Begroting, "Begroting", "", 5, PerAdministratie: true, KanSluiten: true, Fase: 5),
        new(ModuleSoort.Email, "E-mail", "", 6, PerAdministratie: false, KanSluiten: true, Fase: 6),
        new(ModuleSoort.Instellingen, "Instellingen", "", null, PerAdministratie: false, KanSluiten: true, Fase: 1),
    ];

    public static ModuleDefinitie Geef(ModuleSoort soort) => Alle.Single(m => m.Soort == soort);
}
