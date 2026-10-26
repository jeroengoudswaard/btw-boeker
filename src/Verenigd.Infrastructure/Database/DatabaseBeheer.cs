using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Verenigd.Application.Beveiliging;
using Verenigd.Application.Database;
using Verenigd.Infrastructure.Beveiliging;

namespace Verenigd.Infrastructure.Database;

public sealed class DatabaseBeheer : IDatabaseBeheer
{
    internal const string BackupDatabaseNaam = "verenigd.vdb";
    internal const string BackupSleutelNaam = "verenigd.vdb.sleutel";
    private const int SqliteNotADatabase = 26;

    public static string SleutelPad(string databasePad) => databasePad + ".sleutel";

    public bool Bestaat(string pad) => File.Exists(pad) && File.Exists(SleutelPad(pad));

    public async Task<Herstelsleutel> MaakAanAsync(string pad, string wachtwoord, CancellationToken annulering = default)
    {
        ValideerWachtwoord(wachtwoord);
        if (File.Exists(pad) || File.Exists(SleutelPad(pad)))
        {
            throw new DatabaseBestandException($"'{Path.GetFileName(pad)}' bestaat al en wordt niet overschreven.");
        }

        var herstelsleutel = Herstelsleutel.Genereer();
        var (databasesleutel, envelop) = SleutelKluis.Maak(wachtwoord, herstelsleutel);
        try
        {
            Sleutelbestand.Schrijf(SleutelPad(pad), envelop);
            await using var context = MaakContext(pad, databasesleutel);
            await context.Database.MigrateAsync(annulering).ConfigureAwait(false);
        }
        catch
        {
            SqliteConnection.ClearAllPools();
            File.Delete(pad);
            File.Delete(SleutelPad(pad));
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(databasesleutel);
        }

        return herstelsleutel;
    }

    public Task<IDatabaseSessie> OpenAsync(string pad, string wachtwoord, CancellationToken annulering = default)
    {
        var envelop = LeesEnvelop(pad);
        return OpenSessieAsync(pad, SleutelKluis.OntsluitMetWachtwoord(envelop, wachtwoord), annulering);
    }

    public Task<IDatabaseSessie> OpenMetHerstelsleutelAsync(
        string pad, Herstelsleutel herstelsleutel, string nieuwWachtwoord, CancellationToken annulering = default)
    {
        ValideerWachtwoord(nieuwWachtwoord);
        var envelop = LeesEnvelop(pad);
        var databasesleutel = SleutelKluis.OntsluitMetHerstelsleutel(envelop, herstelsleutel);
        Sleutelbestand.Schrijf(SleutelPad(pad), SleutelKluis.VervangWachtwoord(envelop, databasesleutel, nieuwWachtwoord));
        return OpenSessieAsync(pad, databasesleutel, annulering);
    }

    public async Task HerstelBackupAsync(string backupPad, string doelPad, CancellationToken annulering = default)
    {
        if (File.Exists(doelPad) || File.Exists(SleutelPad(doelPad)))
        {
            throw new DatabaseBestandException($"'{Path.GetFileName(doelPad)}' bestaat al en wordt niet overschreven.");
        }

        try
        {
            using var zip = await ZipFile.OpenReadAsync(backupPad, annulering).ConfigureAwait(false);
            var database = zip.GetEntry(BackupDatabaseNaam);
            var sleutel = zip.GetEntry(BackupSleutelNaam);
            if (database is null || sleutel is null)
            {
                throw new DatabaseBestandException("Dit bestand is geen geldige Verenigd-back-up.");
            }

            await database.ExtractToFileAsync(doelPad, overwrite: false, annulering).ConfigureAwait(false);
            await sleutel.ExtractToFileAsync(SleutelPad(doelPad), overwrite: false, annulering).ConfigureAwait(false);
        }
        catch (InvalidDataException ex)
        {
            throw new DatabaseBestandException("Dit bestand is geen geldige Verenigd-back-up.", ex);
        }
    }

    internal static VerenigdDbContext MaakContext(string pad, ReadOnlySpan<byte> databasesleutel) =>
        new(new DbContextOptionsBuilder<VerenigdDbContext>().UseSqlite(Verbindingstekst(pad, databasesleutel)).Options);

    internal static string Verbindingstekst(string pad, ReadOnlySpan<byte> databasesleutel) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = pad,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Password = Convert.ToHexString(databasesleutel),
            Pooling = false, // Geen open bestandshandles na sluiten (nodig voor back-up en bestandsbeheer).
        }.ToString();

    private static async Task<IDatabaseSessie> OpenSessieAsync(string pad, byte[] databasesleutel, CancellationToken annulering)
    {
        try
        {
            await using var context = MaakContext(pad, databasesleutel);
            await context.Database.MigrateAsync(annulering).ConfigureAwait(false);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteNotADatabase)
        {
            CryptographicOperations.ZeroMemory(databasesleutel);
            throw new DatabaseBestandException(
                "Het sleutelbestand hoort niet bij deze database, of de database is beschadigd.", ex);
        }

        return new DatabaseSessie(pad, databasesleutel);
    }

    private static SleutelEnvelop LeesEnvelop(string pad)
    {
        if (!File.Exists(pad))
        {
            throw new DatabaseBestandException($"De database '{Path.GetFileName(pad)}' bestaat niet.");
        }

        return Sleutelbestand.Lees(SleutelPad(pad));
    }

    private static void ValideerWachtwoord(string wachtwoord)
    {
        var bezwaren = WachtwoordBeleid.Controleer(wachtwoord);
        if (bezwaren.Count > 0)
        {
            throw new ArgumentException(bezwaren[0], nameof(wachtwoord));
        }
    }
}
