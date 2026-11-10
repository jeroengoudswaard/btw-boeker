using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace BtwBoeker.Application.Beveiliging;

/// <summary>
/// Een eenmalig getoonde sleutel (200 bits) waarmee de database geopend kan worden als het wachtwoord kwijt is.
/// Weergave: acht groepen van vijf Base32-tekens (RFC 4648), bijvoorbeeld <c>ABCDE-FGH23-...</c>.
/// </summary>
public sealed class Herstelsleutel
{
    public const int AantalBytes = 25;
    private const string Alfabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const int AantalTekens = AantalBytes * 8 / 5; // 40
    private const int Groepsgrootte = 5;

    private readonly byte[] _bytes;

    private Herstelsleutel(byte[] bytes) => _bytes = bytes;

    public ReadOnlySpan<byte> Bytes => _bytes;

    public static Herstelsleutel Genereer() => new(RandomNumberGenerator.GetBytes(AantalBytes));

    public static bool TryParse([NotNullWhen(true)] string? invoer, [NotNullWhen(true)] out Herstelsleutel? sleutel)
    {
        sleutel = null;
        if (invoer is null)
        {
            return false;
        }

        var tekens = new StringBuilder(AantalTekens);
        foreach (var c in invoer)
        {
            if (c is ' ' or '-')
            {
                continue;
            }

            tekens.Append(char.ToUpperInvariant(c));
        }

        if (tekens.Length != AantalTekens)
        {
            return false;
        }

        var bytes = new byte[AantalBytes];
        int buffer = 0, bits = 0, index = 0;
        for (var i = 0; i < tekens.Length; i++)
        {
            var waarde = Alfabet.IndexOf(tekens[i], StringComparison.Ordinal);
            if (waarde < 0)
            {
                return false;
            }

            buffer = (buffer << 5) | waarde;
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                bytes[index++] = (byte)(buffer >> bits);
            }
        }

        sleutel = new Herstelsleutel(bytes);
        return true;
    }

    public override string ToString()
    {
        var resultaat = new StringBuilder(AantalTekens + (AantalTekens / Groepsgrootte));
        int buffer = 0, bits = 0, geschreven = 0;
        foreach (var b in _bytes)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                if (geschreven > 0 && geschreven % Groepsgrootte == 0)
                {
                    resultaat.Append('-');
                }

                resultaat.Append(Alfabet[(buffer >> bits) & 0x1F]);
                geschreven++;
            }
        }

        return resultaat.ToString();
    }
}
