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

    /// <summary>Fase 7: de webapp voor de leiding (voorstellen, leiding, synchroniseren). Achteraan, zodat bewaarde waarden gelijk blijven.</summary>
    Webapp,
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
        new(ModuleSoort.Overzicht, "Overzicht", "", 1, PerAdministratie: false, KanSluiten: false, Fase: 1,
            Omschrijving: "De stand van zaken en wat er te doen is."),
        new(ModuleSoort.Leden, "Leden", "", 2, PerAdministratie: false, KanSluiten: true, Fase: 2,
            Omschrijving: "Alle leden uit SOL3, met hun groepen, voogden en notities."),
        new(ModuleSoort.Contributie, "Contributie", "", 3, PerAdministratie: true, KanSluiten: true, Fase: 3,
            AlleenVoor: AdministratieSoort.Vereniging,
            Omschrijving: "Tarieven, machtigingen en de maandelijkse incasso."),
        new(ModuleSoort.Boekhouding, "Boekhouding", "", 4, PerAdministratie: true, KanSluiten: true, Fase: 4,
            Omschrijving: "Boekingen, bank, bonnen, facturen en de overzichten van het jaar."),
        new(ModuleSoort.Begroting, "Begroting", "", 5, PerAdministratie: true, KanSluiten: true, Fase: 5,
            Omschrijving: "Wat u verwacht uit te geven en te ontvangen, naast wat er werkelijk gebeurt."),
        new(ModuleSoort.Email, "E-mail", "", 6, PerAdministratie: false, KanSluiten: true, Fase: 6,
            Omschrijving: "Berichten aan leden en voogden, via Gmail."),
        new(ModuleSoort.Instellingen, "Instellingen", "", null, PerAdministratie: false, KanSluiten: true, Fase: 1,
            Omschrijving: "Administraties, incasso, facturen, e-mail en groepen."),
        new(ModuleSoort.Webapp, "Webapp", "\uE774", null, PerAdministratie: false, KanSluiten: true, Fase: 7,
            Omschrijving: "Voorstellen van de leiding, accounts en synchroniseren."),
    ];

    public static ModuleDefinitie Geef(ModuleSoort soort) => Alle.Single(m => m.Soort == soort);
}
