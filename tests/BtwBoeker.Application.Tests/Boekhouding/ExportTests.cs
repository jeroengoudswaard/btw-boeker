using BtwBoeker.Application.Boekhouding;
using BtwBoeker.Domain.Financien;

namespace BtwBoeker.Application.Tests.Boekhouding;

public class ExportTests
{
    [Fact]
    public void Csv_met_nederlandse_bedragen_en_puntkomma()
    {
        var tabel = new RapportTabel("Saldibalans 2026", "Mijn administratie",
            [new RapportKolom("Rekening", false), new RapportKolom("Debet", true)],
            [new RapportRij(["1100 Bank; hoofd", new Bedrag(123456)], false, false), new RapportRij(["Totaal", new Bedrag(123456)], false, true)]);

        var csv = CsvExport.Schrijf(tabel);

        var regels = csv.TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("\"Rekening\";\"Debet\"", regels[0]);
        Assert.Equal("\"1100 Bank; hoofd\";\"1234,56\"", regels[1]);
        Assert.StartsWith("﻿", csv, StringComparison.Ordinal);
    }
}
