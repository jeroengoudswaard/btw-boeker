namespace BtwBoeker.Application.Modules;

/// <summary>De modules van de app; in de instellingen bewaard als naam, niet als getal.</summary>
public enum ModuleSoort
{
    Overzicht,
    Boekhouding,
    Instellingen,
}

/// <param name="Pictogram">Codepunt uit het lettertype Segoe Fluent Icons.</param>
/// <param name="Sneltoetsnummer">Cijfer voor Ctrl+cijfer, of <c>null</c> als er geen sneltoets is.</param>
/// <param name="KanSluiten">Of de pagina dicht kan; Start blijft altijd open.</param>
/// <param name="Omschrijving">Eén zin uitleg onder de titel van de pagina.</param>
public sealed record ModuleDefinitie(
    ModuleSoort Soort,
    string Titel,
    string Pictogram,
    int? Sneltoetsnummer,
    bool KanSluiten,
    string Omschrijving = "");

public static class ModuleCatalogus
{
    public static IReadOnlyList<ModuleDefinitie> Alle { get; } =
    [
        new(ModuleSoort.Overzicht, "Overzicht", "", null, KanSluiten: false,
            Omschrijving: "De stand van zaken en wat er te doen is."),
        new(ModuleSoort.Boekhouding, "Boekhouding", "", 1, KanSluiten: true,
            Omschrijving: "Boekingen, bank, bonnen en overzichten van het jaar."),
        new(ModuleSoort.Instellingen, "Instellingen", "", null, KanSluiten: true,
            Omschrijving: "De naam van de administratie en algemene instellingen."),
    ];

    public static ModuleDefinitie Geef(ModuleSoort soort) => Alle.Single(m => m.Soort == soort);
}
