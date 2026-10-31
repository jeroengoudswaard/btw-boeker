using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Verenigd.Application.Administraties;
using Verenigd.Application.Boekhouding;
using Verenigd.Application.Database;
using Verenigd.Application.Import;
using Verenigd.Application.Incasso;
using Verenigd.Application.Inrichting;
using Verenigd.Application.Leden;
using Verenigd.Infrastructure.Database.Leden;

namespace Verenigd.Infrastructure.Database;

internal sealed class DatabaseSessie : IDatabaseSessie
{
    private readonly byte[] _databasesleutel;

    public DatabaseSessie(string pad, byte[] databasesleutel)
    {
        Pad = pad;
        _databasesleutel = databasesleutel;
        Administraties = new AdministratieRepository(MaakContext);
        Leden = new LedenRepository(MaakContext);
        LedenImport = new LedenImportOpslag(MaakContext);
        Contributie = new ContributieRepository(MaakContext);
        Boekhouding = new BoekhoudingRepository(MaakContext);
        Startwizard = new StartwizardOpslag(MaakContext);
        Email = new EmailOpslag(MaakContext);
        Webapp = new WebappOpslag(MaakContext);
    }

    public string Pad { get; }

    public IAdministratieRepository Administraties { get; }

    public ILedenRepository Leden { get; }

    public ILedenImportOpslag LedenImport { get; }

    public IContributieRepository Contributie { get; }

    public IBoekhoudingRepository Boekhouding { get; }

    public IStartwizardOpslag Startwizard { get; }

    public Application.Email.IEmailOpslag Email { get; }

    public Application.Webapp.IWebappOpslag Webapp { get; }

    public async Task MaakBackupAsync(string doelPad, CancellationToken annulering = default)
    {
        var werkmap = Directory.CreateTempSubdirectory("verenigd-backup-");
        try
        {
            // SQLite online-backup: consistent, ook terwijl de database open is. De kopie houdt dezelfde sleutel.
            var kopie = Path.Combine(werkmap.FullName, DatabaseBeheer.BackupDatabaseNaam);
            using (var bron = new SqliteConnection(DatabaseBeheer.Verbindingstekst(Pad, _databasesleutel)))
            using (var doel = new SqliteConnection(DatabaseBeheer.Verbindingstekst(kopie, _databasesleutel)))
            {
                await bron.OpenAsync(annulering).ConfigureAwait(false);
                await doel.OpenAsync(annulering).ConfigureAwait(false);
                bron.BackupDatabase(doel);
            }

            File.Copy(DatabaseBeheer.SleutelPad(Pad), Path.Combine(werkmap.FullName, DatabaseBeheer.BackupSleutelNaam));

            var tijdelijk = doelPad + ".tmp";
            File.Delete(tijdelijk);
            await ZipFile.CreateFromDirectoryAsync(
                werkmap.FullName, tijdelijk, CompressionLevel.Optimal, includeBaseDirectory: false, annulering).ConfigureAwait(false);
            File.Move(tijdelijk, doelPad, overwrite: true);
        }
        finally
        {
            werkmap.Delete(recursive: true);
        }
    }

    public ValueTask DisposeAsync()
    {
        CryptographicOperations.ZeroMemory(_databasesleutel);
        return ValueTask.CompletedTask;
    }

    private VerenigdDbContext MaakContext() => DatabaseBeheer.MaakContext(Pad, _databasesleutel);
}
