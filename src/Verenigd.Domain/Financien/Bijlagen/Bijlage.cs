using System.Security.Cryptography;
using Verenigd.Domain.Administraties;

namespace Verenigd.Domain.Financien.Bijlagen;

/// <summary>De toegestane bestandstypen (besluit 137), herkend aan de eerste bytes van de inhoud.</summary>
public enum BijlageSoort
{
    Pdf = 1,
    Jpg = 2,
    Png = 3,
}

/// <summary>
/// Een bon of factuur bij een journaalpost (besluiten 83 en 84). Zonder post staat de bijlage in het postvak (besluit 85).
/// Koppelen en ontkoppelen mag ook bij een geboekte post; de boeking zelf verandert niet (besluit 136). De inhoud staat
/// apart in <see cref="BijlageInhoud"/>, zodat lijsten licht blijven.
/// </summary>
public sealed class Bijlage
{
    /// <summary>Besluit 97: maximaal 10 MB per bijlage.</summary>
    public const int MaximaleGrootte = 10 * 1024 * 1024;

    public const int MaximaleNaamLengte = 200;

    private Bijlage()
    {
        Bestandsnaam = null!;
        Controlecode = null!;
    }

    /// <exception cref="ArgumentException">Leeg, groter dan 10 MB, of geen PDF, JPG of PNG.</exception>
    public Bijlage(AdministratieSoort administratie, string bestandsnaam, ReadOnlySpan<byte> inhoud, DateTime nuUtc)
    {
        if (inhoud.Length > MaximaleGrootte)
        {
            throw new ArgumentException("Een bijlage is maximaal 10 MB.", nameof(inhoud));
        }

        Soort = Herken(inhoud) ?? throw new ArgumentException("Alleen PDF, JPG of PNG kan als bijlage worden toegevoegd.", nameof(inhoud));
        var naam = Path.GetFileName((bestandsnaam ?? string.Empty).Replace('\\', '/')).Trim();
        if (naam.Length == 0)
        {
            naam = "bijlage." + Soort.ToString().ToLowerInvariant();
        }

        Id = Guid.NewGuid();
        Administratie = administratie;
        Bestandsnaam = naam.Length > MaximaleNaamLengte ? naam[..MaximaleNaamLengte] : naam;
        Grootte = inhoud.Length;
        Controlecode = Convert.ToHexString(SHA256.HashData(inhoud));
        ToegevoegdUtc = nuUtc;
    }

    public Guid Id { get; private set; }
    public AdministratieSoort Administratie { get; private set; }

    /// <summary>De journaalpost; <c>null</c> = in het postvak.</summary>
    public Guid? PostId { get; private set; }

    public string Bestandsnaam { get; private set; }
    public BijlageSoort Soort { get; private set; }
    public int Grootte { get; private set; }

    /// <summary>SHA-256 van de inhoud, om hetzelfde bestand te herkennen.</summary>
    public string Controlecode { get; private set; }

    /// <summary>De datum op de bon: herkend uit de PDF-tekst of ingevuld (besluit 96).</summary>
    public DateOnly? Datum { get; private set; }

    /// <summary>Het totaalbedrag op de bon: herkend uit de PDF-tekst of ingevuld (besluit 96); negatief bij een creditnota (besluit 142).</summary>
    public Bedrag? Bedrag { get; private set; }

    public DateTime ToegevoegdUtc { get; private set; }

    public bool IsInPostvak => PostId is null;

    /// <summary>Het soort aan de eerste bytes (PDF <c>%PDF-</c>, JPG <c>FF D8 FF</c>, PNG-handtekening), of <c>null</c>.</summary>
    public static BijlageSoort? Herken(ReadOnlySpan<byte> inhoud) =>
        inhoud.StartsWith("%PDF-"u8) ? BijlageSoort.Pdf
        : inhoud.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]) ? BijlageSoort.Jpg
        : inhoud.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]) ? BijlageSoort.Png
        : null;

    /// <exception cref="ArgumentException">Geen post.</exception>
    public void KoppelAan(Guid postId)
    {
        if (postId == Guid.Empty)
        {
            throw new ArgumentException("Kies een post.", nameof(postId));
        }

        PostId = postId;
    }

    /// <summary>Terug naar het postvak.</summary>
    public void Ontkoppel() => PostId = null;

    public void ZetGegevens(DateOnly? datum, Bedrag? bedrag)
    {
        Datum = datum;
        Bedrag = bedrag;
    }
}

/// <summary>De bestandsinhoud van een bijlage; wordt pas geladen bij bekijken of exporteren.</summary>
public sealed class BijlageInhoud
{
    private BijlageInhoud()
    {
        Gegevens = [];
    }

    public BijlageInhoud(Guid bijlageId, byte[] gegevens)
    {
        ArgumentNullException.ThrowIfNull(gegevens);
        BijlageId = bijlageId;
        Gegevens = gegevens;
    }

    public Guid BijlageId { get; private set; }

    public byte[] Gegevens { get; private set; }
}
