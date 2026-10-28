using Verenigd.Application.Modules;
using Verenigd.Domain.Administraties;

namespace Verenigd.Application.Instellingen;

public enum Thema
{
    Systeem,
    Licht,
    Donker,
}

/// <summary>Welke woorden het tabblad Financiën gebruikt (besluit 127); de gegevens blijven altijd formeel.</summary>
public enum Weergavemodus
{
    Expert,
    Eenvoudig,
}

public sealed record OpenTabblad(ModuleSoort Module, AdministratieSoort? Administratie);

/// <summary>Voorkeuren per Windows-gebruiker. Bevat geen persoonsgegevens van leden.</summary>
public sealed record GebruikersInstellingen
{
    public Thema Thema { get; init; } = Thema.Systeem;

    /// <summary>Standaard Expert (besluit 132).</summary>
    public Weergavemodus Weergave { get; init; } = Weergavemodus.Expert;

    public string? LaatsteDatabase { get; init; }

    public IReadOnlyList<OpenTabblad> OpenTabbladen { get; init; } = [];
}

public interface IInstellingenOpslag
{
    GebruikersInstellingen Laad();

    void Bewaar(GebruikersInstellingen instellingen);
}
