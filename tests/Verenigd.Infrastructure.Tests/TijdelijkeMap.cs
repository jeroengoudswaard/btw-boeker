namespace Verenigd.Infrastructure.Tests;

/// <summary>Een lege map per test die na afloop wordt opgeruimd.</summary>
internal sealed class TijdelijkeMap : IDisposable
{
    public TijdelijkeMap()
    {
        Pad = Path.Combine(Path.GetTempPath(), "verenigd-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Pad);
    }

    public string Pad { get; }

    public string Bestand(string naam) => Path.Combine(Pad, naam);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Pad, recursive: true);
        }
        catch (IOException)
        {
            // Opruimen is best effort; een open bestandshandle mag een test niet laten falen.
        }
    }
}
