namespace BtwBoeker.Infrastructure.Database;

/// <summary>
/// Telt hoe vaak er in deze sessie iets in de database is opgeslagen. Een momentopname (bijvoorbeeld van de
/// boekhouding) is geldig zolang de teller niet is veranderd. Elke context van de sessie verhoogt de teller na opslaan.
/// </summary>
internal sealed class Wijzigingen
{
    private long _versie;

    public long Versie => Interlocked.Read(ref _versie);

    /// <summary>Na elke verhoging; op de thread van het opslaan.</summary>
    public event EventHandler? Gewijzigd;

    public void Verhoog()
    {
        Interlocked.Increment(ref _versie);
        Gewijzigd?.Invoke(this, EventArgs.Empty);
    }
}
