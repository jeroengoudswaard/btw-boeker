using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Verenigd.App.Diensten;
using Verenigd.App.ViewModels;
using Verenigd.App.Views;
using Verenigd.Application.Database;
using Verenigd.Application.Instellingen;
using Verenigd.Infrastructure.Database;
using Verenigd.Infrastructure.Instellingen;

namespace Verenigd.App;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Logging bevat bewust geen persoonsgegevens, IBAN's of volledige paden.
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(Paden.Logmap, "verenigd-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                formatProvider: System.Globalization.CultureInfo.InvariantCulture)
            .CreateLogger();

        DispatcherUnhandledException += OnverwachteFout;

        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IDatabaseBeheer, DatabaseBeheer>();
        builder.Services.AddSingleton<IInstellingenOpslag>(_ => new JsonInstellingenOpslag(Paden.Instellingenbestand));
        builder.Services.AddSingleton<IDialoogService, WpfDialoogService>();
        builder.Services.AddSingleton<IThemaService, WpfThemaService>();
        builder.Services.AddSingleton<ShellViewModel>();
        builder.Services.AddSingleton<ShellWindow>();
        _host = builder.Build();

        Log.Information("Verenigd {Versie} gestart", typeof(App).Assembly.GetName().Version);
        var venster = _host.Services.GetRequiredService<ShellWindow>();
        MainWindow = venster;
        venster.Show();

        // Eerst het hoofdvenster volledig tekenen; anders verliest het na een opstartdialoog de voorgrond.
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
        await _host.Services.GetRequiredService<ShellViewModel>().InitialiseerAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Verenigd afgesloten");
        _host?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private void OnverwachteFout(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Onverwachte fout");
        MessageBox.Show(
            MainWindow,
            "Er is een onverwachte fout opgetreden. De details zijn vastgelegd in het logbestand (Help > Logbestanden openen).\n\n"
                + e.Exception.Message,
            "Verenigd",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
