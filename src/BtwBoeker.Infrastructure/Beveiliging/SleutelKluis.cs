using System.Security.Cryptography;
using System.Text;
using BtwBoeker.Application.Beveiliging;

namespace BtwBoeker.Infrastructure.Beveiliging;

/// <summary>
/// Envelope encryption: de database wordt versleuteld met een willekeurige databasesleutel (256 bits).
/// Die sleutel wordt twee keer ingepakt (AES-256-GCM): met een sleutel afgeleid van het wachtwoord
/// (PBKDF2-SHA256) en met een sleutel afgeleid van de herstelsleutel (HKDF-SHA256).
/// Zo kan het wachtwoord worden vervangen zonder de database opnieuw te versleutelen.
/// </summary>
public static class SleutelKluis
{
    public const int HuidigeVersie = 1;
    internal const int WachtwoordIteraties = 600_000; // OWASP-advies 2023+ voor PBKDF2-SHA256
    private const int SleutelLengte = 32;
    private const int ZoutLengte = 16;
    private const string KdfWachtwoord = "PBKDF2-SHA256";
    private const string KdfHerstel = "HKDF-SHA256";

    public static (byte[] Databasesleutel, SleutelEnvelop Envelop) Maak(string wachtwoord, Herstelsleutel herstelsleutel)
    {
        ArgumentNullException.ThrowIfNull(herstelsleutel);
        var databasesleutel = RandomNumberGenerator.GetBytes(SleutelLengte);
        var envelop = new SleutelEnvelop(
            HuidigeVersie,
            PakInMetWachtwoord(databasesleutel, wachtwoord),
            PakIn(databasesleutel, KdfHerstel, 0, RandomNumberGenerator.GetBytes(ZoutLengte), herstelsleutel.Bytes, "herstel"));
        return (databasesleutel, envelop);
    }

    /// <exception cref="OngeldigeSleutelException">Onjuist wachtwoord.</exception>
    public static byte[] OntsluitMetWachtwoord(SleutelEnvelop envelop, string wachtwoord)
    {
        ArgumentNullException.ThrowIfNull(envelop);
        return PakUit(envelop.Wachtwoord, Encoding.UTF8.GetBytes(wachtwoord), "wachtwoord");
    }

    /// <exception cref="OngeldigeSleutelException">Onjuiste herstelsleutel.</exception>
    public static byte[] OntsluitMetHerstelsleutel(SleutelEnvelop envelop, Herstelsleutel herstelsleutel)
    {
        ArgumentNullException.ThrowIfNull(envelop);
        ArgumentNullException.ThrowIfNull(herstelsleutel);
        return PakUit(envelop.Herstel, herstelsleutel.Bytes, "herstel");
    }

    /// <summary>Pakt de databasesleutel opnieuw in met een nieuw wachtwoord; de herstelsleutel blijft geldig.</summary>
    public static SleutelEnvelop VervangWachtwoord(SleutelEnvelop envelop, byte[] databasesleutel, string nieuwWachtwoord)
    {
        ArgumentNullException.ThrowIfNull(envelop);
        return envelop with { Wachtwoord = PakInMetWachtwoord(databasesleutel, nieuwWachtwoord) };
    }

    private static SleutelSlot PakInMetWachtwoord(byte[] databasesleutel, string wachtwoord) =>
        PakIn(
            databasesleutel,
            KdfWachtwoord,
            WachtwoordIteraties,
            RandomNumberGenerator.GetBytes(ZoutLengte),
            Encoding.UTF8.GetBytes(wachtwoord),
            "wachtwoord");

    private static SleutelSlot PakIn(
        byte[] databasesleutel, string kdf, int iteraties, byte[] zout, ReadOnlySpan<byte> geheim, string slotnaam)
    {
        var kek = LeidSleutelAf(kdf, iteraties, zout, geheim);
        try
        {
            var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
            var versleuteld = new byte[databasesleutel.Length];
            var tag = new byte[AesGcm.TagByteSizes.MaxSize];
            using var aes = new AesGcm(kek, tag.Length);
            aes.Encrypt(nonce, databasesleutel, versleuteld, tag, Context(slotnaam));
            return new SleutelSlot(kdf, iteraties, zout, nonce, versleuteld, tag);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    private static byte[] PakUit(SleutelSlot slot, ReadOnlySpan<byte> geheim, string slotnaam)
    {
        var kek = LeidSleutelAf(slot.Kdf, slot.Iteraties, slot.Zout, geheim);
        try
        {
            var databasesleutel = new byte[slot.Versleuteld.Length];
            using var aes = new AesGcm(kek, slot.Tag.Length);
            aes.Decrypt(slot.Nonce, slot.Versleuteld, slot.Tag, databasesleutel, Context(slotnaam));
            return databasesleutel;
        }
        catch (AuthenticationTagMismatchException ex)
        {
            throw new OngeldigeSleutelException(
                slotnaam == "herstel" ? "De herstelsleutel is onjuist." : "Het wachtwoord is onjuist.", ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    private static byte[] LeidSleutelAf(string kdf, int iteraties, byte[] zout, ReadOnlySpan<byte> geheim) => kdf switch
    {
        KdfWachtwoord => Rfc2898DeriveBytes.Pbkdf2(geheim, zout, iteraties, HashAlgorithmName.SHA256, SleutelLengte),
        KdfHerstel => HKDF.DeriveKey(HashAlgorithmName.SHA256, geheim.ToArray(), SleutelLengte, zout, "btw-boeker-herstel"u8.ToArray()),
        _ => throw new CryptographicException($"Onbekende sleutelafleiding '{kdf}'."),
    };

    // Bindt elk ingepakt blok aan zijn doel, zodat slots niet onderling verwisseld kunnen worden.
    private static byte[] Context(string slotnaam) => Encoding.UTF8.GetBytes($"btw-boeker-sleutel-v{HuidigeVersie}:{slotnaam}");
}

/// <summary>Inhoud van het sleutelbestand naast de database. Bevat alleen ingepakte (versleutelde) sleutels.</summary>
public sealed record SleutelEnvelop(int Versie, SleutelSlot Wachtwoord, SleutelSlot Herstel);

#pragma warning disable CA1819 // Byte-arrays zijn hier bewust: dit is een serialisatiecontract.
public sealed record SleutelSlot(string Kdf, int Iteraties, byte[] Zout, byte[] Nonce, byte[] Versleuteld, byte[] Tag);
#pragma warning restore CA1819
