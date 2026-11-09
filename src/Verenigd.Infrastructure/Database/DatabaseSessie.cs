using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Verenigd.Application.Administraties;
using Verenigd.Application.Boekhouding;
using Verenigd.Application.Database;
using Verenigd.Application.Inrichting;

namespace Verenigd.Infrastructure.Database;

internal sealed class DatabaseSessie : IDatabaseSessie
{
    private readonly byte[] _databasesleutel;
    private readonly Wijzigingen _wijzigingen = new();

    public DatabaseSessie(string pad, byte[] databasesleutel)
    {
        Pad = pad;
        _databasesleutel = databasesleutel;
        _wijzigingen.Gewijzigd += (_, e) => Gewijzigd?.Invoke(this, e); // met de sessie als afzender
        Administraties = new AdministratieRepository(MaakContext);
        Boekhouding = new BoekhoudingRepository(MaakContext, _wijzigingen);
        Startwizard = new StartwizardOpslag(MaakContext);
    }

    public string Pad { get; }

    public event EventHandler? Gewijzigd;

    public IAdministratieRepository Administraties { get; }

    public IBoekhoudingRepository Boekhouding { get; }

    public IStartwizardOpslag Startwizard { get; }

    public async Task MaakBackupAsync(string doelPad, CancellationToken annulering = default)
    {
        var werkmap = Directory.CreateTempSubdirectory("btw-boeker-backup-");
        try
        {
            // SQLite online-backup: consistent, ook terwijl de database open is. De kopie houdt dezelfde sleutel.
            var kopie = Path.Combine(werkmap.FullName, DatabaseBeheer.BackupDatabaseNaam);
            using (var bron = new SqliteConnection(DatabaseBeheer.Verbindingstekst(Pad, _databasesleutel)))
            using (var doel = new SqliteConnection(DatabaseBeheer.Verbindingstekst(kopie, _databasesleutel, pooling: false))) // de kopie gaat direct weer dicht
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
        DatabaseBeheer.LeegPool(Pad, _databasesleutel); // besluit 236: geen open bestandshandles na sluiten
        CryptographicOperations.ZeroMemory(_databasesleutel);
        return ValueTask.CompletedTask;
    }

    private VerenigdDbContext MaakContext() => DatabaseBeheer.MaakContext(Pad, _databasesleutel, _wijzigingen);
}
