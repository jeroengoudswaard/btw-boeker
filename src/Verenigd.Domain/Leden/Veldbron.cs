namespace Verenigd.Domain.Leden;

/// <summary>Waar de huidige waarde van een veld vandaan komt (besluit 38).</summary>
public enum Veldbron
{
    Sol3 = 1,
    Verenigd = 2,
    Leiding = 3,
}

/// <summary>Herkomst van één veld van een persoon.</summary>
public sealed class Veldherkomst
{
    internal Veldherkomst(string veld, Veldbron bron, DateTime gewijzigdUtc)
    {
        Veld = veld;
        Bron = bron;
        GewijzigdUtc = gewijzigdUtc;
    }

    public int Id { get; private set; }
    public string Veld { get; private set; }
    public Veldbron Bron { get; private set; }
    public DateTime GewijzigdUtc { get; private set; }

    internal void Wijzig(Veldbron bron, DateTime gewijzigdUtc)
    {
        Bron = bron;
        GewijzigdUtc = gewijzigdUtc;
    }
}

/// <summary>Eén verschil tussen de huidige en de nieuwe waarde, in leesbare vorm.</summary>
public sealed record Veldwijziging(string Veld, string? Oud, string? Nieuw);
