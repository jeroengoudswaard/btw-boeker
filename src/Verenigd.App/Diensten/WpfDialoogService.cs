using System.Windows;
using Microsoft.Win32;
using Verenigd.App.Views.Dialogen;
using Verenigd.Application.Beveiliging;

namespace Verenigd.App.Diensten;

/// <summary>Windows-standaarddialogen (bestandskiezers, MessageBox) en de eigen wachtwoordvensters.</summary>
internal sealed class WpfDialoogService : IDialoogService
{
    private const string DatabaseFilter = "Verenigd-database (*.vdb)|*.vdb";
    private const string BackupFilter = "Verenigd-back-up (*.vbak)|*.vbak";

    private static Window? Eigenaar => System.Windows.Application.Current.MainWindow;

    public string? KiesNieuwDatabasebestand(string titel)
    {
        var dialoog = new SaveFileDialog
        {
            Title = titel,
            Filter = DatabaseFilter,
            DefaultExt = ".vdb",
            FileName = "verenigd.vdb",
            OverwritePrompt = false, // Bestaande databases worden nooit overschreven; de applicatie meldt dit zelf.
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        return dialoog.ShowDialog(Eigenaar) == true ? dialoog.FileName : null;
    }

    public string? KiesBestaandDatabasebestand()
    {
        var dialoog = new OpenFileDialog { Title = "Database openen", Filter = DatabaseFilter, CheckFileExists = true };
        return dialoog.ShowDialog(Eigenaar) == true ? dialoog.FileName : null;
    }

    public string? KiesBackupDoel(string voorgesteldeNaam)
    {
        var dialoog = new SaveFileDialog
        {
            Title = "Back-up maken",
            Filter = BackupFilter,
            DefaultExt = ".vbak",
            FileName = voorgesteldeNaam,
        };
        return dialoog.ShowDialog(Eigenaar) == true ? dialoog.FileName : null;
    }

    public string? KiesBackupBron()
    {
        var dialoog = new OpenFileDialog { Title = "Back-up terugzetten", Filter = BackupFilter, CheckFileExists = true };
        return dialoog.ShowDialog(Eigenaar) == true ? dialoog.FileName : null;
    }

    public WachtwoordInvoer? VraagWachtwoord(string databaseNaam, string? foutmelding)
    {
        var venster = new WachtwoordVenster(databaseNaam, foutmelding);
        return ToonModaal(venster) ? venster.Invoer : null;
    }

    public string? VraagNieuwWachtwoord()
    {
        var venster = new NieuwWachtwoordVenster();
        return ToonModaal(venster) ? venster.ViewModel.Wachtwoord : null;
    }

    public HerstelInvoer? VraagHerstel(string databaseNaam)
    {
        var venster = new HerstelVenster(databaseNaam);
        return ToonModaal(venster) ? venster.ViewModel.MaakInvoer() : null;
    }

    public void ToonHerstelsleutel(Herstelsleutel herstelsleutel) => ToonModaal(new HerstelsleutelVenster(herstelsleutel));

    public OpslaanKeuze VraagOmOpslaan(string documentnaam) =>
        Toon($"Wilt u de wijzigingen in '{documentnaam}' opslaan?", "Verenigd", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning) switch
        {
            MessageBoxResult.Yes => OpslaanKeuze.Opslaan,
            MessageBoxResult.No => OpslaanKeuze.NietOpslaan,
            _ => OpslaanKeuze.Annuleren,
        };

    public bool Bevestig(string titel, string vraag) =>
        Toon(vraag, titel, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public void ToonFout(string titel, string melding) => Toon(melding, titel, MessageBoxButton.OK, MessageBoxImage.Error);

    public void ToonInformatie(string titel, string melding) =>
        Toon(melding, titel, MessageBoxButton.OK, MessageBoxImage.Information);

    /// <summary>Toont een eigen dialoog en geeft daarna de voorgrond expliciet terug aan het hoofdvenster.</summary>
    private static bool ToonModaal(Window venster)
    {
        var eigenaar = Eigenaar;
        venster.Owner = eigenaar is { IsLoaded: true } ? eigenaar : null;
        var resultaat = venster.ShowDialog() == true;
        eigenaar?.Activate();
        return resultaat;
    }

    private static MessageBoxResult Toon(string tekst, string titel, MessageBoxButton knoppen, MessageBoxImage pictogram) =>
        Eigenaar is { IsLoaded: true } eigenaar
            ? MessageBox.Show(eigenaar, tekst, titel, knoppen, pictogram)
            : MessageBox.Show(tekst, titel, knoppen, pictogram);
}
