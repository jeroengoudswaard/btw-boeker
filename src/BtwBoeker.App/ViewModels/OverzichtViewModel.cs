using System.Collections.ObjectModel;
using System.Globalization;
using BtwBoeker.Application.Inrichting;
using BtwBoeker.Application.Modules;

namespace BtwBoeker.App.ViewModels;

/// <summary>Een kerncijfer op Start: titel, bedrag en een korte toelichting.</summary>
public sealed record Kerncijferregel(string Titel, string Waarde, string Toelichting);

/// <summary>
/// Startpagina: zonder database de keuzes om te beginnen, met database de kerncijfers en de kaarten met werk.
/// </summary>
public sealed class OverzichtViewModel(ShellViewModel shell)
    : PaginaViewModel(ModuleCatalogus.Geef(ModuleSoort.Overzicht))
{
    public ShellViewModel Shell { get; } = shell;

    /// <summary>In de navigatie en de paginakop heet het overzicht "Start".</summary>
    public override string Titel => "Start";

    /// <summary>Alleen kaarten waar iets te doen is.</summary>
    public Verzameling<Startkaart> Kaarten { get; } = [];

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
        regels.Add(new Kerncijferregel(string.Create(CultureInfo.InvariantCulture, $"Resultaat {jaar} t/m vandaag"), cijfers.Resultaat.ToString(),
            "alleen geboekte posten"));
        return regels;
    }
}
