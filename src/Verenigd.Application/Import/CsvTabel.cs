namespace Verenigd.Application.Import;

/// <summary>Een ingelezen CSV-bestand. Kolommen worden op naam gezocht, hoofdletterongevoelig.</summary>
public sealed class CsvTabel
{
    private readonly Dictionary<string, int> _index;

    internal CsvTabel(IReadOnlyList<string> kolommen, IEnumerable<(int Regelnummer, string[] Velden)> regels)
    {
        Kolommen = kolommen;
        _index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < kolommen.Count; i++)
        {
            if (!_index.TryAdd(kolommen[i], i))
            {
                throw new Sol3ImportException($"De kolom '{kolommen[i]}' komt meer dan één keer voor.");
            }
        }

        Regels = [.. regels.Select(r => new CsvRegel(this, r.Regelnummer, r.Velden))];
    }

    public IReadOnlyList<string> Kolommen { get; }
    public IReadOnlyList<CsvRegel> Regels { get; }

    public bool HeeftKolom(string kolom) => _index.ContainsKey(kolom);

    internal int Index(string kolom) =>
        _index.TryGetValue(kolom, out var index) ? index : throw new Sol3ImportException($"De kolom '{kolom}' ontbreekt.");
}

/// <summary>Eén gegevensregel; <see cref="Regelnummer"/> telt de kopregel als regel 1.</summary>
public sealed class CsvRegel
{
    private readonly CsvTabel _tabel;
    private readonly string[] _velden;

    internal CsvRegel(CsvTabel tabel, int regelnummer, string[] velden)
    {
        _tabel = tabel;
        Regelnummer = regelnummer;
        _velden = velden;
    }

    public int Regelnummer { get; }

    /// <summary>De waarde zonder spaties aan de randen.</summary>
    public string this[string kolom] => _velden[_tabel.Index(kolom)].Trim();

    /// <summary>De waarde, of <c>null</c> als het veld leeg is.</summary>
    public string? Waarde(string kolom) => this[kolom] is { Length: > 0 } waarde ? waarde : null;
}
