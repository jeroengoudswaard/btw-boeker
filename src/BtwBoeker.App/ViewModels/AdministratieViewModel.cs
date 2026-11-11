using CommunityToolkit.Mvvm.ComponentModel;
using BtwBoeker.Domain.Administraties;

namespace BtwBoeker.App.ViewModels;

/// <summary>
/// Gedeelde, levende weergave van de administratie. Alle pagina's verwijzen naar dezelfde instantie,
/// zodat een gewijzigde naam direct overal zichtbaar is.
/// </summary>
public sealed class AdministratieViewModel(Administratie model) : ObservableObject
{
    internal Administratie Model { get; } = model;

    public AdministratieSoort Soort => Model.Soort;

    public string Naam => Model.Naam;

    /// <summary>Meldt dat het onderliggende model is gewijzigd.</summary>
    internal void Ververs()
    {
        OnPropertyChanged(nameof(Naam));
    }
}
