using Verenigd.App.ViewModels.Boekhouding;
using Verenigd.App.ViewModels.Contributie;
using Verenigd.App.ViewModels.Leden;
using Verenigd.Application.Beveiliging;

namespace Verenigd.App.Diensten;

/// <param name="Wachtwoord">Het ingevoerde wachtwoord, of <c>null</c> als de gebruiker herstel koos.</param>
/// <param name="HerstelGevraagd">De gebruiker klikte op "Wachtwoord vergeten?".</param>
public sealed record WachtwoordInvoer(string? Wachtwoord, bool HerstelGevraagd);

public sealed record HerstelInvoer(Herstelsleutel Herstelsleutel, string NieuwWachtwoord);

public enum OpslaanKeuze
{
    Opslaan,
    NietOpslaan,
    Annuleren,
}

/// <summary>Alle interactie via vensters. ViewModels kennen alleen deze abstractie (testbaar zonder UI).</summary>
public interface IDialoogService
{
    string? KiesNieuwDatabasebestand(string titel);

    string? KiesBestaandDatabasebestand();

    string? KiesBackupDoel(string voorgesteldeNaam);

    string? KiesBackupBron();

    /// <returns><c>null</c> bij annuleren.</returns>
    WachtwoordInvoer? VraagWachtwoord(string databaseNaam, string? foutmelding);

    /// <returns>Een wachtwoord dat aan het beleid voldoet, of <c>null</c> bij annuleren.</returns>
    string? VraagNieuwWachtwoord();

    HerstelInvoer? VraagHerstel(string databaseNaam);

    /// <summary>Toont de herstelsleutel; keert pas terug als de gebruiker bevestigt dat deze bewaard is.</summary>
    void ToonHerstelsleutel(Herstelsleutel herstelsleutel);

    OpslaanKeuze VraagOmOpslaan(string documentnaam);

    bool Bevestig(string titel, string vraag);

    void ToonFout(string titel, string melding);

    void ToonInformatie(string titel, string melding);

    /// <summary>Eén of meer SOL3-exports (CSV) kiezen; leeg bij annuleren.</summary>
    IReadOnlyList<string> KiesImportbestanden();

    string? KiesCsvDoel(string voorgesteldeNaam);

    /// <returns><c>true</c> als de import is uitgevoerd.</returns>
    bool ToonImportWizard(ImportWizardViewModel wizard);

    string? KiesXmlDoel(string voorgesteldeNaam);

    /// <returns><c>true</c> als er een run definitief is gemaakt.</returns>
    bool ToonIncassoWizard(IncassoWizardViewModel wizard);

    string? KiesExcelBestand();

    /// <summary>Een of meer CAMT.053-bankafschriften (.xml).</summary>
    IReadOnlyList<string>? KiesCamtBestanden();

    /// <returns><c>true</c> als de afschriften zijn ingelezen.</returns>
    bool ToonBankImport(BankImportViewModel wizard);

    string? KiesXlsxDoel(string voorgesteldeNaam);

    /// <returns><c>true</c> als de boekhouding is overgenomen.</returns>
    bool ToonBoekhoudingImport(BoekhoudingImportViewModel wizard);
}
