using System.Windows;

namespace Verenigd.App.Views;

/// <summary>
/// Geeft de DataContext door aan elementen buiten de visuele boom, zoals de kop van een DataGrid-kolom
/// (<c>{Binding Data.Eigenschap, Source={StaticResource Proxy}}</c>).
/// </summary>
public sealed class Bindingsproxy : Freezable
{
    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(object), typeof(Bindingsproxy));

    public object? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    protected override Freezable CreateInstanceCore() => new Bindingsproxy();
}
