using System.Text;

namespace Verenigd.Application.Import;

/// <summary>
/// Leest SOL3-CSV's: <c>;</c>-gescheiden, velden (meestal) tussen aanhalingstekens, Windows-1252 of UTF-8 met BOM.
/// </summary>
public static class Sol3CsvLezer
{
    private const char Scheidingsteken = ';';
    private static readonly Encoding Windows1252 = MaakWindows1252();

    public static CsvTabel Lees(Stream inhoud)
    {
        ArgumentNullException.ThrowIfNull(inhoud);
        using var buffer = new MemoryStream();
        inhoud.CopyTo(buffer);
        var bytes = buffer.ToArray();
        var preambule = Encoding.UTF8.GetPreamble();
        var tekst = bytes.AsSpan().StartsWith(preambule)
            ? Encoding.UTF8.GetString(bytes, preambule.Length, bytes.Length - preambule.Length)
            : Windows1252.GetString(bytes);

        var records = Splits(tekst).Where(r => !(r.Velden.Count == 1 && string.IsNullOrWhiteSpace(r.Velden[0]))).ToList();
        if (records.Count == 0)
        {
            throw new Sol3ImportException("Het bestand is leeg.");
        }

        var kolommen = records[0].Velden.Select(k => k.Trim()).ToList();
        foreach (var (regelnummer, velden) in records.Skip(1))
        {
            if (velden.Count != kolommen.Count)
            {
                throw new Sol3ImportException(
                    $"Op regel {regelnummer} staan {velden.Count} velden in plaats van {kolommen.Count}.");
            }
        }

        return new CsvTabel(kolommen, records.Skip(1).Select(r => (r.Regelnummer, r.Velden.ToArray())));
    }

    private static IEnumerable<(int Regelnummer, List<string> Velden)> Splits(string tekst)
    {
        var velden = new List<string>();
        var veld = new StringBuilder();
        var tussenAanhalingstekens = false;
        var regel = 1;
        var startregel = 1;

        for (var i = 0; i < tekst.Length; i++)
        {
            var teken = tekst[i];
            if (tussenAanhalingstekens)
            {
                if (teken == '"' && i + 1 < tekst.Length && tekst[i + 1] == '"')
                {
                    veld.Append('"');
                    i++;
                }
                else if (teken == '"')
                {
                    tussenAanhalingstekens = false;
                }
                else
                {
                    if (teken == '\n')
                    {
                        regel++;
                    }

                    veld.Append(teken);
                }

                continue;
            }

            switch (teken)
            {
                case '"':
                    tussenAanhalingstekens = true;
                    break;
                case Scheidingsteken:
                    velden.Add(veld.ToString());
                    veld.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    velden.Add(veld.ToString());
                    veld.Clear();
                    yield return (startregel, velden);
                    velden = [];
                    regel++;
                    startregel = regel;
                    break;
                default:
                    veld.Append(teken);
                    break;
            }
        }

        if (tussenAanhalingstekens)
        {
            throw new Sol3ImportException($"Op regel {startregel} wordt een aanhalingsteken niet afgesloten.");
        }

        if (veld.Length > 0 || velden.Count > 0)
        {
            velden.Add(veld.ToString());
            yield return (startregel, velden);
        }
    }

    private static Encoding MaakWindows1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }
}
