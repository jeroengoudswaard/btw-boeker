using Verenigd.App.ViewModels.Boekhouding;
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

    string? KiesCsvDoel(string voorgesteldeNaam);

    /// <summary>Waar de auditfile (XAF) heen moet (besluit 197).</summary>
    string? KiesXafDoel(string voorgesteldeNaam);

    string? KiesExcelBestand();

    /// <summary>Een of meer CAMT.053-bankafschriften (.xml).</summary>
    IReadOnlyList<string>? KiesCamtBestanden();

    /// <returns><c>true</c> als de afschriften zijn ingelezen.</returns>
    bool ToonBankImport(BankImportViewModel wizard);

    string? KiesXlsxDoel(string voorgesteldeNaam);

    /// <summary>Een rekeningschema-sjabloon (.vsjabloon) om mee in te richten.</summary>
    string? KiesSjabloonbestand();

    string? KiesSjabloonDoel(string voorgesteldeNaam);

    /// <returns><c>true</c> als de administratie is ingericht.</returns>
    bool ToonStandaardinrichting(StandaardinrichtingViewModel wizard);

    /// <returns><c>true</c> als de jaarrekeningindeling is opgeslagen (besluiten 263–268).</returns>
    bool ToonJaarrekeningIndeling(JaarrekeningIndelingViewModel editor);

    /// <returns><c>true</c> als de boekhouding is overgenomen.</returns>
    bool ToonBoekhoudingImport(BoekhoudingImportViewModel wizard);

    /// <returns><c>true</c> als de historie is overgenomen.</returns>
    bool ToonHistorieImport(HistorieImportViewModel wizard);

    /// <summary>Eén of meer bonnen of facturen (PDF, JPG, PNG); leeg bij annuleren.</summary>
    IReadOnlyList<string> KiesBijlagen();

    /// <summary><i>Opslaan als…</i> voor een bijlage; <c>null</c> bij annuleren.</summary>
    string? KiesBijlageDoel(string voorgesteldeNaam);

    /// <summary>Toont het voorbeeldvenster van een bijlage (besluit 86).</summary>
    void ToonBijlage(BijlageVoorbeeldViewModel voorbeeld);

    /// <summary>De startwizard (besluit 169); sluit via <i>Later verder</i>, <i>Voltooien</i> of ✕.</summary>
    void ToonStartwizard(ViewModels.Inrichting.StartwizardViewModel wizard);
}
