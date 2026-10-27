namespace Verenigd.Application.Import;

/// <summary>Een SOL3-bestand kan niet worden gelezen. De melding bevat nooit gegevens uit het bestand.</summary>
public sealed class Sol3ImportException : Exception
{
    public Sol3ImportException()
    {
    }

    public Sol3ImportException(string message)
        : base(message)
    {
    }

    public Sol3ImportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
