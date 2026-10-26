using System.Windows;
using Verenigd.Application.Instellingen;

namespace Verenigd.App.Diensten;

public interface IThemaService
{
    void PasToe(Thema thema);
}

/// <summary>Schakelt het WPF Fluent-thema (Systeem volgt de Windows-instelling Licht/Donker).</summary>
internal sealed class WpfThemaService : IThemaService
{
    public void PasToe(Thema thema) =>
        System.Windows.Application.Current.ThemeMode = thema switch
        {
            Thema.Licht => ThemeMode.Light,
            Thema.Donker => ThemeMode.Dark,
            _ => ThemeMode.System,
        };
}
