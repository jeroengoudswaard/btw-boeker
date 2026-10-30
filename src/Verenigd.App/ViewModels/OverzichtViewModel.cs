using System.Collections.ObjectModel;
using Verenigd.Application.Inrichting;
using Verenigd.Application.Modules;

namespace Verenigd.App.ViewModels;

/// <summary>Startpagina: zonder database de keuzes om te beginnen, met database de kaarten met werk (besluit 183).</summary>
public sealed class OverzichtViewModel(ShellViewModel shell)
    : TabbladViewModel(ModuleCatalogus.Geef(ModuleSoort.Overzicht), administratie: null)
{
    public ShellViewModel Shell { get; } = shell;

    /// <summary>Alleen kaarten waar iets te doen is.</summary>
    public ObservableCollection<Startkaart> Kaarten { get; } = [];
}
