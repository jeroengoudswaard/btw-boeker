using CommunityToolkit.Mvvm.ComponentModel;
using Verenigd.Application.Modules;

namespace Verenigd.App.ViewModels;

/// <summary>
/// Eén regel in de navigatiebalk (besluiten 168, 178 en 181): een module, of een onderdeel (subtabblad) van Boekhouding of
/// Contributie. Onderdelen staan alleen in de lijst als hun module is uitgeklapt.
/// </summary>
public sealed partial class NavigatieItem(string sleutel, string titel, string pictogram, ModuleSoort module, int? onderdeel) : ObservableObject
{
    /// <summary>Vast en taalonafhankelijk, bijvoorbeeld <c>Boekhouding.Bank</c>.</summary>
    public string Sleutel { get; } = sleutel;

    public string Titel { get; } = titel;

    /// <summary>Codepunt uit Segoe Fluent Icons; leeg bij een onderdeel.</summary>
    public string Pictogram { get; } = pictogram;

    public ModuleSoort Module { get; } = module;

    /// <summary>Het onderdeel binnen de module, of <c>null</c> voor de module zelf.</summary>
    public int? Onderdeel { get; } = onderdeel;

    public bool IsSubitem => Onderdeel is not null;

    /// <summary>Voor een module met onderdelen: uitgeklapt of niet.</summary>
    [ObservableProperty]
    public partial bool IsUitgeklapt { get; set; }

    public bool HeeftOnderdelen { get; init; }

    /// <summary>Besluit 220: het aantal dat er te doen is, of <c>null</c>.</summary>
    [ObservableProperty]
    public partial int? Teller { get; set; }

    public override string ToString() => Titel;
}
