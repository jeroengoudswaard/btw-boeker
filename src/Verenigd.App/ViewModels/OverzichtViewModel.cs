using System.Collections.ObjectModel;
using System.Globalization;
using Verenigd.Application.Inrichting;
using Verenigd.Application.Modules;

namespace Verenigd.App.ViewModels;

/// <summary>Een kerncijfer op Start (besluit 217): titel, bedrag en een korte toelichting.</summary>
public sealed record Kerncijferregel(string Titel, string Waarde, string Toelichting);

/// <summary>
/// Startpagina: zonder database de keuzes om te beginnen, met database de kerncijfers van de gekozen administratie
/// (besluit 217) en de kaarten met werk (besluit 183).
/// </summary>
public sealed class OverzichtViewModel(ShellViewModel shell)
    : TabbladViewModel(ModuleCatalogus.Geef(ModuleSoort.Overzicht), administratie: null)
{
    public ShellViewModel Shell { get; } = shell;

    /// <summary>Alleen kaarten waar iets te doen is.</summary>
    public ObservableCollection<Startkaart> Kaarten { get; } = [];

    /// <summary>Leeg zolang de boekhouding van de gekozen administratie niet is ingericht.</summary>
    public ObservableCollection<Kerncijferregel> Kerncijfers { get; } = [];

    public static IReadOnlyList<Kerncijferregel> Kerncijferregels(Kerncijfers cijfers, int jaar)
    {
        ArgumentNullException.ThrowIfNull(cijfers);
        if (!cijfers.HeeftBoekhouding)
        {
            return [];
        }

        var regels = cijfers.Bankrekeningen
            .Select(b => new Kerncijferregel(b.Naam, b.Saldo.ToString(),
                b.Datum is { } datum ? $"afschrift van {datum.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture)}" : "volgens het grootboek"))
            .ToList();
        if (cijfers.OpenstaandeContributie is { } open)
        {
            regels.Add(new Kerncijferregel("Openstaande contributie", open.ToString(),
                cijfers.Storneringen == 1 ? "1 stornering" : $"{cijfers.Storneringen} storneringen"));
        }

        regels.Add(new Kerncijferregel(string.Create(CultureInfo.InvariantCulture, $"Resultaat {jaar} t/m vandaag"), cijfers.Resultaat.ToString(),
            cijfers.BegrootNaarRato is { } rato ? $"begroot naar rato {rato}" : "geen vastgestelde begroting"));
        return regels;
    }
}
