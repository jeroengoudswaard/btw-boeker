using System.Windows;
using System.Windows.Media;
using Verenigd.Domain.Administraties;

namespace Verenigd.App.Views.Thema;

/// <summary>
/// Besluit 216: de accentkleur volgt de gekozen administratie in plaats van de Windows-accentkleur. De sleutels zijn die
/// van het Fluent-thema; ze worden op het venster gezet, zodat alles in het hoofdvenster ze overneemt.
/// </summary>
public static class Accentkleuren
{
    public static readonly string[] Vulsleutels = ["AccentFillColorDefaultBrush", "AccentButtonBackground"];
    public static readonly string[] Zweefsleutels = ["AccentFillColorSecondaryBrush", "AccentButtonBackgroundPointerOver"];
    public static readonly string[] Druksleutels = ["AccentFillColorTertiaryBrush", "AccentButtonBackgroundPressed"];
    public static readonly string[] Tekstsleutels = ["TextOnAccentFillColorPrimaryBrush", "AccentButtonForeground", "AccentButtonForegroundPointerOver", "AccentButtonForegroundPressed"];

    /// <summary>Zonder administratie (geen database) gelden weer de kleuren van Windows.</summary>
    public static void PasToe(ResourceDictionary bronnen, Kleur? accent)
    {
        ArgumentNullException.ThrowIfNull(bronnen);
        if (accent is not { } kleur)
        {
            foreach (var sleutel in Vulsleutels.Concat(Zweefsleutels).Concat(Druksleutels).Concat(Tekstsleutels).Append("AccentTextFillColorPrimaryBrush"))
            {
                bronnen.Remove(sleutel);
            }

            return;
        }

        var basis = Color.FromRgb(kleur.R, kleur.G, kleur.B);
        Zet(bronnen, Vulsleutels, basis);
        Zet(bronnen, Zweefsleutels, Color.FromArgb(0xE6, kleur.R, kleur.G, kleur.B));
        Zet(bronnen, Druksleutels, Color.FromArgb(0xCC, kleur.R, kleur.G, kleur.B));
        var tekst = kleur.LeesbareTekstkleur();
        Zet(bronnen, Tekstsleutels, Color.FromRgb(tekst.R, tekst.G, tekst.B));
        Zet(bronnen, ["AccentTextFillColorPrimaryBrush"], basis);
    }

    private static void Zet(ResourceDictionary bronnen, IEnumerable<string> sleutels, Color kleur)
    {
        var penseel = new SolidColorBrush(kleur);
        penseel.Freeze();
        foreach (var sleutel in sleutels)
        {
            bronnen[sleutel] = penseel;
        }
    }
}
