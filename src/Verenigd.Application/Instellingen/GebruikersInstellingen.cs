using Verenigd.Application.Modules;
using Verenigd.Domain.Administraties;

namespace Verenigd.Application.Instellingen;

public enum Thema
{
    Systeem,
    Licht,
    Donker,
}

public sealed record OpenTabblad(ModuleSoort Module, AdministratieSoort? Administratie);

/// <summary>Voorkeuren per Windows-gebruiker. Bevat geen persoonsgegevens van leden.</summary>
public sealed record GebruikersInstellingen
{
    public Thema Thema { get; init; } = Thema.Systeem;

    public string? LaatsteDatabase { get; init; }

    public IReadOnlyList<OpenTabblad> OpenTabbladen { get; init; } = [];
}

public interface IInstellingenOpslag
{
    GebruikersInstellingen Laad();

    void Bewaar(GebruikersInstellingen instellingen);
}
