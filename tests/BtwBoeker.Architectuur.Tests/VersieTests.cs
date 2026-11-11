using System.Text.RegularExpressions;

namespace BtwBoeker.Architectuur.Tests;

/// <summary>
/// Het versienummer staat op één plek: <c>Directory.Build.props</c>. Een eigen versie in een project maakte dat de app
/// zich in het logbestand en onder Help › Over als 0.1.0 meldde, terwijl de MSI 0.19.0 heette (gevonden met de
/// snelheidsmeting).
/// </summary>
public partial class VersieTests
{
    [GeneratedRegex(@"<(Version|AssemblyVersion|FileVersion|VersionPrefix)>")]
    private static partial Regex Versie();

    private static string Repositorymap()
    {
        var map = new DirectoryInfo(AppContext.BaseDirectory);
        while (map is not null && !File.Exists(Path.Combine(map.FullName, "BtwBoeker.slnx")))
        {
            map = map.Parent;
        }

        return map?.FullName ?? throw new InvalidOperationException("De hoofdmap van de repository is niet gevonden.");
    }

    [Fact]
    public void Geen_project_zet_een_eigen_versie()
    {
        var wortel = Repositorymap();
        var gevonden = Directory.EnumerateFiles(wortel, "*.csproj", SearchOption.AllDirectories)
            .Where(p => !p.Split(Path.DirectorySeparatorChar).Any(d => d is "bin" or "obj"))
            .Where(p => Versie().IsMatch(File.ReadAllText(p)))
            .Select(p => Path.GetRelativePath(wortel, p))
            .ToList();

        Assert.Empty(gevonden);
        Assert.Matches(@"<Version>\d+\.\d+\.\d+</Version>", File.ReadAllText(Path.Combine(wortel, "Directory.Build.props")));
    }
}
