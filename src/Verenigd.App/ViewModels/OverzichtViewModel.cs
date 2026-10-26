using Verenigd.Application.Modules;

namespace Verenigd.App.ViewModels;

/// <summary>Startpagina (zonder database) en overzicht (met database). Kan niet gesloten worden.</summary>
public sealed class OverzichtViewModel(ShellViewModel shell)
    : TabbladViewModel(ModuleCatalogus.Geef(ModuleSoort.Overzicht), administratie: null)
{
    public ShellViewModel Shell { get; } = shell;
}
