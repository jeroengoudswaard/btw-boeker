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
