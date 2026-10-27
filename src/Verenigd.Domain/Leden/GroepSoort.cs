namespace Verenigd.Domain.Leden;

/// <summary>Soort van een SOL3-groep (organisatie-eenheid), in te stellen door de gebruiker (besluit 43).</summary>
public enum GroepSoort
{
    Speltak = 1,
    Bestuur = 2,

    /// <summary>Overblijfsel van de migratie naar SOL3; standaard verborgen (besluit 40).</summary>
    Technisch = 3,
    Overig = 4,
}
