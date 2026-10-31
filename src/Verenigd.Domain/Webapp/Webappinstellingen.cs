namespace Verenigd.Domain.Webapp;

/// <summary>
/// De koppeling met het Supabase-project van de webapp (besluiten 37 en 226–233). Eén rij, in de versleutelde database.
/// De publieke sleutel is geen geheim; de vernieuwingssleutel van de beheerderssessie wel (alleen in deze database).
/// De <c>service_role</c>-sleutel komt hier nooit (besluit 37).
/// </summary>
public sealed class Webappinstellingen
{
    public const int MaximaleLengte = 2000;

    public int Id { get; private set; } = 1;

    /// <summary>Bijvoorbeeld <c>https://abcd.supabase.co</c>.</summary>
    public string? Projectadres { get; private set; }

    public string? PubliekeSleutel { get; private set; }

    public string? BeheerderEmail { get; private set; }

    public string? Vernieuwingssleutel { get; private set; }

    public DateTime? LaatsteSynchronisatieUtc { get; private set; }

    public bool LaatsteGelukt { get; private set; }

    public string? LaatsteMelding { get; private set; }

    public bool IsIngesteld => Projectadres is not null && PubliekeSleutel is not null && BeheerderEmail is not null;

    public bool IsGekoppeld => IsIngesteld && Vernieuwingssleutel is not null;

    public void WijzigProject(string projectadres, string publiekeSleutel, string beheerderEmail)
    {
        var adres = projectadres?.Trim().TrimEnd('/') ?? "";
        if (!Uri.TryCreate(adres, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.PathAndQuery != "/" || adres.Length > MaximaleLengte)
        {
            throw new ArgumentException("Het projectadres begint met https:// en heeft geen pad, bijvoorbeeld https://abcd.supabase.co.", nameof(projectadres));
        }

        var sleutel = publiekeSleutel?.Trim() ?? "";
        if (sleutel.Length == 0 || sleutel.Length > MaximaleLengte || sleutel.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("De publieke sleutel ontbreekt of bevat spaties.", nameof(publiekeSleutel));
        }

        var email = beheerderEmail?.Trim() ?? "";
        if (email.Length is 0 or > 320 || !email.Contains('@', StringComparison.Ordinal))
        {
            throw new ArgumentException("Vul het e-mailadres van de beheerder in.", nameof(beheerderEmail));
        }

        if (!string.Equals(adres, Projectadres, StringComparison.Ordinal) || !string.Equals(email, BeheerderEmail, StringComparison.OrdinalIgnoreCase))
        {
            Vernieuwingssleutel = null;
        }

        Projectadres = adres;
        PubliekeSleutel = sleutel;
        BeheerderEmail = email;
    }

    public void Koppel(string vernieuwingssleutel)
    {
        if (string.IsNullOrWhiteSpace(vernieuwingssleutel))
        {
            throw new ArgumentException("Lege vernieuwingssleutel.", nameof(vernieuwingssleutel));
        }

        Vernieuwingssleutel = vernieuwingssleutel;
    }

    public void Ontkoppel() => Vernieuwingssleutel = null;

    public void RegistreerSynchronisatie(DateTime nuUtc, bool gelukt, string melding)
    {
        LaatsteSynchronisatieUtc = nuUtc;
        LaatsteGelukt = gelukt;
        LaatsteMelding = melding.Length > 500 ? melding[..500] : melding;
    }
}
