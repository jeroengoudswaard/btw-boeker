using Verenigd.Application.Administraties;
using Verenigd.Application.Beveiliging;
using Verenigd.Application.Import;
using Verenigd.Application.Incasso;
using Verenigd.Application.Leden;

namespace Verenigd.Application.Database;

/// <summary>Aanmaken, openen en herstellen van versleutelde Verenigd-databases.</summary>
public interface IDatabaseBeheer
{
    /// <summary>Staan zowel de database als het bijbehorende sleutelbestand op schijf?</summary>
    bool Bestaat(string pad);

    /// <summary>Maakt een nieuwe, versleutelde database aan.</summary>
    /// <returns>De herstelsleutel; deze wordt nergens opgeslagen en moet aan de gebruiker getoond worden.</returns>
    /// <exception cref="DatabaseBestandException">Op het pad staat al een database.</exception>
    Task<Herstelsleutel> MaakAanAsync(string pad, string wachtwoord, CancellationToken annulering = default);

    /// <exception cref="OngeldigeSleutelException">Het wachtwoord is onjuist.</exception>
    /// <exception cref="DatabaseBestandException">Het databasebestand of sleutelbestand ontbreekt.</exception>
    Task<IDatabaseSessie> OpenAsync(string pad, string wachtwoord, CancellationToken annulering = default);

    /// <summary>Opent de database met de herstelsleutel en stelt een nieuw wachtwoord in.</summary>
    /// <exception cref="OngeldigeSleutelException">De herstelsleutel is onjuist.</exception>
    Task<IDatabaseSessie> OpenMetHerstelsleutelAsync(
        string pad, Herstelsleutel herstelsleutel, string nieuwWachtwoord, CancellationToken annulering = default);

    /// <summary>Zet een back-up terug naar <paramref name="doelPad"/>. Bestaande bestanden worden nooit overschreven.</summary>
    /// <exception cref="DatabaseBestandException">Het doel bestaat al of de back-up is ongeldig.</exception>
    Task HerstelBackupAsync(string backupPad, string doelPad, CancellationToken annulering = default);
}

/// <summary>Een geopende database. Sluiten via <see cref="IAsyncDisposable.DisposeAsync"/>.</summary>
public interface IDatabaseSessie : IAsyncDisposable
{
    string Pad { get; }

    IAdministratieRepository Administraties { get; }

    ILedenRepository Leden { get; }

    ILedenImportOpslag LedenImport { get; }

    IContributieRepository Contributie { get; }

    /// <summary>Schrijft een consistente, versleutelde back-up (database + sleutelbestand) naar één bestand.</summary>
    Task MaakBackupAsync(string doelPad, CancellationToken annulering = default);
}

/// <summary>Een probleem met de bestanden van een database (ontbreekt, bestaat al, beschadigd).</summary>
public sealed class DatabaseBestandException : Exception
{
    public DatabaseBestandException()
    {
    }

    public DatabaseBestandException(string message)
        : base(message)
    {
    }

    public DatabaseBestandException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
