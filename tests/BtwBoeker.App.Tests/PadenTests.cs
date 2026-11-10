using BtwBoeker.App.Diensten;

namespace BtwBoeker.App.Tests;

public sealed class PadenTests
{
    private const string DataMapOmgeving = "BTW_BOEKER_DATAMAP";

    [Fact]
    public void Instellingen_en_logbestanden_gebruiken_de_eigen_datamap()
    {
        var eerder = Environment.GetEnvironmentVariable(DataMapOmgeving);
        var datamap = Path.Combine(Path.GetTempPath(), "btw-boeker-tests", Guid.NewGuid().ToString("N"));
        try
        {
            Environment.SetEnvironmentVariable(DataMapOmgeving, datamap);

            Assert.Equal(Path.Combine(datamap, "instellingen.json"), Paden.Instellingenbestand);
            Assert.Equal(Path.Combine(datamap, "logs"), Paden.Logmap);
        }
        finally
        {
            Environment.SetEnvironmentVariable(DataMapOmgeving, eerder);
        }
    }

    [Fact]
    public void Standaardpaden_staan_in_de_eigen_btwboeker_map()
    {
        var eerder = Environment.GetEnvironmentVariable(DataMapOmgeving);
        try
        {
            Environment.SetEnvironmentVariable(DataMapOmgeving, null);

            Assert.Equal(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BTW-Boeker", "instellingen.json"),
                Paden.Instellingenbestand);
            Assert.Equal(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BTW-Boeker", "logs"),
                Paden.Logmap);
        }
        finally
        {
            Environment.SetEnvironmentVariable(DataMapOmgeving, eerder);
        }
    }
}
