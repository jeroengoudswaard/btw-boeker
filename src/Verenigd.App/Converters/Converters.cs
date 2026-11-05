using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Verenigd.Domain.Administraties;

namespace Verenigd.App.Converters;

/// <summary><see cref="Kleur"/> naar een (bevroren) <see cref="SolidColorBrush"/>; <c>null</c> wordt transparant.</summary>
public sealed class KleurNaarBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Kleur kleur)
        {
            return Brushes.Transparent;
        }

        var brush = new SolidColorBrush(Color.FromRgb(kleur.R, kleur.G, kleur.B));
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class NullNaarZichtbaarheidConverter : IValueConverter
{
    /// <summary>Indien <c>true</c>: zichtbaar als de waarde <c>null</c> is.</summary>
    public bool Omgekeerd { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value is not null) ^ Omgekeerd ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class OmgekeerdeBoolNaarZichtbaarheidConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Voor aanvinkbare menu-items: <c>true</c> als de waarde gelijk is aan de parameter.</summary>
public sealed class GelijkAanParameterConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => Equals(value, parameter);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Zichtbaar als de waarde (bijvoorbeeld een enum) als tekst gelijk is aan de parameter.</summary>
public sealed class GelijkAanNaarZichtbaarheidConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Besluit 270: zichtbaar als een aantal groter is dan nul (een knop die alleen verschijnt als er iets te doen is).</summary>
public sealed class GroterDanNulNaarZichtbaarheidConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int aantal && aantal > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary><c>true</c> wordt <c>false</c> en omgekeerd.</summary>
public sealed class NietConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}

/// <summary>Enum naar index en terug, bijvoorbeeld voor <c>TabControl.SelectedIndex</c>.</summary>
public sealed class EnumNaarIndexConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Enum waarde ? System.Convert.ToInt32(waarde, CultureInfo.InvariantCulture) : -1;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int index && index >= 0 && targetType.IsEnum ? Enum.ToObject(targetType, index) : Binding.DoNothing;
}

/// <summary>Toont een rapport als <see cref="System.Windows.Documents.FlowDocument"/>, gelijk aan de afdruk.</summary>
public sealed class RapportNaarDocumentConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Application.Boekhouding.RapportTabel tabel)
        {
            return null;
        }

        var document = Diensten.WpfAfdrukDienst.MaakDocument(tabel);
        document.PagePadding = new Thickness(0);
        document.Background = Brushes.Transparent;
        document.SetResourceReference(System.Windows.Documents.FlowDocument.ForegroundProperty, "TextFillColorPrimaryBrush");
        return document;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Besluit 168: één hoofdactie per pagina. <c>true</c> geeft de accentstijl, <c>false</c> de gewone knop; zo kan
/// <i>Opslaan</i> alleen op pagina's waar opslaan de hoofdactie is blauw zijn.
/// </summary>
public sealed class HoofdactieStijlConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        System.Windows.Application.Current?.TryFindResource(value is true ? "AccentButtonStyle" : typeof(System.Windows.Controls.Button));

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>
/// Besluit 239: een onderdeel (<see cref="Verenigd.Application.Modules.ModuleSoort"/>, of een startkaart via het onderdeel
/// waar hij naartoe leidt) naar zijn kleur. Met <see cref="Tint"/> de kleur voor 16 % dekkend, zodat de tint in licht en
/// donker vanzelf klopt.
/// </summary>
public sealed class OnderdeelkleurConverter : IValueConverter
{
    public bool Tint { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var module = value switch
        {
            Verenigd.Application.Modules.ModuleSoort m => m,
            Verenigd.Application.Inrichting.Startkaartsoort s => ViewModels.Onderdeelkleuren.OnderdeelVan(s),
            _ => Verenigd.Application.Modules.ModuleSoort.Overzicht,
        };
        var kleur = ViewModels.Onderdeelkleuren.Voor(module);
        var alfa = Tint ? (byte)Math.Round(ViewModels.Onderdeelkleuren.Tintdekking * 255) : (byte)255;
        var brush = new SolidColorBrush(Color.FromArgb(alfa, kleur.R, kleur.G, kleur.B));
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Een vaste paletkleur (<c>Koraal</c>, <c>Blauw</c>…) uit <see cref="ViewModels.Onderdeelkleuren"/> als penseel.</summary>
public sealed class PaletkleurExtension : System.Windows.Markup.MarkupExtension
{
    public string Naam { get; set; } = "Grijs";

    public bool Tint { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var kleur = (Kleur)(typeof(ViewModels.Onderdeelkleuren).GetField(Naam)?.GetValue(null)
            ?? throw new InvalidOperationException($"Onbekende paletkleur {Naam}."));
        var alfa = Tint ? (byte)Math.Round(ViewModels.Onderdeelkleuren.Tintdekking * 255) : (byte)255;
        var brush = new SolidColorBrush(Color.FromArgb(alfa, kleur.R, kleur.G, kleur.B));
        brush.Freeze();
        return brush;
    }
}
