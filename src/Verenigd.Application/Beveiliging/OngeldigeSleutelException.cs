namespace Verenigd.Application.Beveiliging;

/// <summary>Het opgegeven wachtwoord of de herstelsleutel past niet bij de database.</summary>
public sealed class OngeldigeSleutelException : Exception
{
    public OngeldigeSleutelException()
        : base("Het wachtwoord of de herstelsleutel is onjuist.")
    {
    }

    public OngeldigeSleutelException(string message)
        : base(message)
    {
    }

    public OngeldigeSleutelException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
