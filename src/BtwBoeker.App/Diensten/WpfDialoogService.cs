using System.Windows;
using Microsoft.Win32;
using BtwBoeker.App.ViewModels.Boekhouding;
using BtwBoeker.App.Views.Boekhouding;
using BtwBoeker.App.Views.Dialogen;
using BtwBoeker.Application.Beveiliging;

namespace BtwBoeker.App.Diensten;

/// <summary>Windows-standaarddialogen (bestandskiezers, MessageBox) en de eigen wachtwoordvensters.</summary>
internal sealed class WpfDialoogService : IDialoogService
{
    private const string DatabaseFilter = "BTW-Boeker-database (*.btwdb)|*.btwdb";
    private const string BackupFilter = "BTW-Boeker-back-up (*.btwbackup)|*.btwbackup";
    private const string CsvFilter = "CSV-bestand (*.csv)|*.csv|Alle bestanden (*.*)|*.*";

    /// <summary>Het actieve venster (bijvoorbeeld de startwizard), anders het hoofdvenster.</summary>
    private static Window? Eigenaar =>
        System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? System.Windows.Application.Current.MainWindow;

    public string? KiesNieuwDatabasebestand(string titel)
    {
        var dialoog = new SaveFileDialog
        {
            Title = titel,
            Filter = DatabaseFilter,
            DefaultExt = ".btwdb",
            FileName = "btw-boeker.btwdb",
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
            DefaultExt = ".btwbackup",
            FileName = voorgesteldeNaam,
        };
        return dialoog.ShowDialog(Eigenaar) == true ? dialoog.FileName : null;
    }

    public string? KiesBackupBron()
    {
        var dialoog = new OpenFileDialog { Title = "Back-up terugzetten", Filter = BackupFilter, CheckFileExists = true };
        return dialoog.ShowDialog(Eigenaar) == true ? dialoog.FileName : null;
    }

    public string? KiesCsvDoel(string voorgesteldeNaam)
    {
        var dialoog = new SaveFileDialog { Title = "Opslaan als", Filter = CsvFilter, DefaultExt = ".csv", FileName = voorgesteldeNaam };
        return dialoog.ShowDialog(Eigenaar) == true ? dialoog.FileName : null;
    }

    public string? KiesXafDoel(string voorgesteldeNaam)
    {
        var dialoog = new SaveFileDialog
        {
            Title = "Auditfile (XAF) opslaan",
            Filter = "Auditfile Financieel (*.xaf)|*.xaf",
            DefaultExt = ".xaf",
            FileName = voorgesteldeNaam,
        };
        return dialoog.ShowDialog(Eigenaar) == true ? dialoog.FileName : null;
    }

    public IReadOnlyList<string>? KiesCamtBestanden()
    {
        var dialoog = new OpenFileDialog
        {
            Title = "Bankafschriften (CAMT.053) kiezen", Filter = "CAMT.053-bankafschrift (*.xml)|*.xml", CheckFileExists = true, Multiselect = true,
        };
        return dialoog.ShowDialog(Eigenaar) == true ? dialoog.FileNames : null;
    }

    public bool ToonBankImport(BankImportViewModel wizard) => ToonModaal(new BankImportVenster(wizard));

    public string? KiesXlsxDoel(string voorgesteldeNaam)
    {
        var dialoog = new SaveFileDialog { Title = "Exporteren naar Excel", Filter = "Excel-werkmap (*.xlsx)|*.xlsx", DefaultExt = ".xlsx", FileName = voorgesteldeNaam };
        return dialoog.ShowDialog(Eigenaar) == true ? dialoog.FileName : null;
    }

    public string? KiesSjabloonbestand()
    {
        var dialoog = new OpenFileDialog { Title = "Rekeningschema-sjabloon kiezen", Filter = "Sjabloon BTW-Boeker (*.btwsjabloon)|*.btwsjabloon", CheckFileExists = true };
        return dialoog.ShowDialog(Eigenaar) == true ? dialoog.FileName : null;
    }

    public string? KiesSjabloonDoel(string voorgesteldeNaam)
    {
        var dialoog = new SaveFileDialog
        {
            Title = "Rekeningschema exporteren als sjabloon", Filter = "Sjabloon BTW-Boeker (*.btwsjabloon)|*.btwsjabloon", DefaultExt = ".btwsjabloon", FileName = voorgesteldeNaam,
        };
        return dialoog.ShowDialog(Eigenaar) == true ? dialoog.FileName : null;
    }

    public bool ToonStandaardinrichting(StandaardinrichtingViewModel wizard) => ToonModaal(new StandaardinrichtingVenster(wizard));

    public bool ToonJaarrekeningIndeling(JaarrekeningIndelingViewModel editor) => ToonModaal(new JaarrekeningIndelingVenster(editor));

    public void ToonStartwizard(ViewModels.Inrichting.StartwizardViewModel wizard) => ToonModaal(new Views.Inrichting.StartwizardVenster(wizard));

    public IReadOnlyList<string> KiesBijlagen()
    {
        var dialoog = new OpenFileDialog
        {
            Title = "Bonnen of facturen kiezen",
            Filter = "Bon of factuur (*.pdf;*.jpg;*.jpeg;*.png)|*.pdf;*.jpg;*.jpeg;*.png",
            CheckFileExists = true,
            Multiselect = true,
        };
        return dialoog.ShowDialog(Eigenaar) == true ? dialoog.FileNames : [];
    }

    public string? KiesBijlageDoel(string voorgesteldeNaam)
    {
        var extensie = Path.GetExtension(voorgesteldeNaam);
        var dialoog = new SaveFileDialog
        {
            Title = "Bijlage opslaan als",
            Filter = string.IsNullOrEmpty(extensie) ? "Alle bestanden (*.*)|*.*" : $"Bestand (*{extensie})|*{extensie}|Alle bestanden (*.*)|*.*",
            DefaultExt = extensie,
            FileName = voorgesteldeNaam,
        };
        return dialoog.ShowDialog(Eigenaar) == true ? dialoog.FileName : null;
    }

    public void ToonBijlage(BijlageVoorbeeldViewModel voorbeeld) => ToonModaal(new BijlageVenster(voorbeeld));

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
        Toon($"Wilt u de wijzigingen in '{documentnaam}' opslaan?", "BTW-Boeker", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning) switch
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
