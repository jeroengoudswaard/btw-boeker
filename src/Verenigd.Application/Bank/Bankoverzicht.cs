using Verenigd.Application.Boekhouding;
using Verenigd.Domain.Financien.Bank;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.Application.Bank;

public enum MutatieStatus
{
    TeVerwerken,
    Concept,
    Geboekt,
}

/// <summary>Saldo volgens het laatste afschrift tegenover het grootboek op die datum (geboekt, en met concepten).</summary>
public sealed record BankSaldi(DateOnly? Datum, long? Bank, long Grootboek, long MetConcepten)
{
    public bool IsAangesloten => Bank is null || Bank == MetConcepten;
}

public static class Bankoverzicht
{
    public static MutatieStatus Status(BoekhoudingStand stand, Bankmutatie mutatie)
    {
        ArgumentNullException.ThrowIfNull(stand);
        ArgumentNullException.ThrowIfNull(mutatie);
        return mutatie.PostId is not { } id ? MutatieStatus.TeVerwerken
            : stand.Posten.FirstOrDefault(p => p.Id == id) is { IsGeboekt: true } ? MutatieStatus.Geboekt
            : MutatieStatus.Concept;
    }

    public static BankSaldi Saldi(BoekhoudingStand stand, Dagboek dagboek)
    {
        ArgumentNullException.ThrowIfNull(stand);
        ArgumentNullException.ThrowIfNull(dagboek);
        var rekening = dagboek.Rekening ?? throw new ArgumentException("Een bankdagboek hoort bij een grootboekrekening.", nameof(dagboek));
        var laatste = stand.Bankafschriften.Where(a => a.Dagboek.Id == dagboek.Id || ReferenceEquals(a.Dagboek, dagboek)).MaxBy(a => a.Datum);
        var datum = laatste?.Datum;
        var jaar = datum?.Year ?? (stand.Boekjaren.Count > 0 ? stand.Boekjaren[^1].Jaar : 0);
        var geboekt = Grootboek.Saldo(stand, rekening, jaar, datum);
        var concepten = stand.Posten
            .Where(p => !p.IsGeboekt && p.Datum.Year == jaar && (datum is null || p.Datum <= datum))
            .SelectMany(p => p.Regels)
            .Where(r => Grootboek.ZelfdeRekening(r.Rekening, rekening))
            .Sum(r => r.Saldo);
        return new BankSaldi(datum, laatste?.Eindsaldo, geboekt, geboekt + concepten);
    }
}
