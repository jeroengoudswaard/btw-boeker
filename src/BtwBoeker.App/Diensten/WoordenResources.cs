using System.Windows;
using BtwBoeker.App.ViewModels;
using BtwBoeker.Application.Weergaven;

namespace BtwBoeker.App.Diensten;

/// <summary>
/// Houdt de app-resources <c>T.Sleutel</c> (tekst) en <c>U.Sleutel</c> (uitleg, alleen in Eenvoudig) gelijk aan het
/// woordenboek, zodat <c>{DynamicResource T.Journaal}</c> in de XAML direct meewisselt.
/// </summary>
public static class WoordenResources
{
    public static void Koppel(ResourceDictionary resources, Woordenboek woorden)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(woorden);
        Werkbij(resources, woorden);
        woorden.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Woordenboek.Weergave))
            {
                Werkbij(resources, woorden);
            }
        };
    }

    public static void Werkbij(ResourceDictionary resources, Woordenboek woorden)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(woorden);
        foreach (var term in Woordenlijst.Termen)
        {
            resources["T." + term.Sleutel] = woorden[term.Sleutel];
            resources["U." + term.Sleutel] = woorden.Uitleg(term.Sleutel);
        }
    }
}
