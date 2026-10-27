using System.Reflection;
using Verenigd.Application.Import;
using Verenigd.Domain.Leden;
using Verenigd.Infrastructure.Database;

namespace Verenigd.Architectuur.Tests;

/// <summary>Bewaakt de lagen (docs/ARCHITECTUUR.md) en de scheiding leden–financiën (besluiten 36 en 39).</summary>
public class ArchitectuurTests
{
    private static readonly Assembly Domain = typeof(Persoon).Assembly;
    private static readonly Assembly Application = typeof(Sol3Inlezer).Assembly;
    private static readonly Assembly Infrastructure = typeof(DatabaseBeheer).Assembly;

    private static IEnumerable<string> Verwijzingen(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty);

    [Fact]
    public void Domain_verwijst_naar_geen_enkele_andere_laag_of_framework()
    {
        Assert.DoesNotContain(Verwijzingen(Domain), n =>
            n.StartsWith("Verenigd", StringComparison.Ordinal)
            || n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
            || n.StartsWith("Presentation", StringComparison.Ordinal)
            || n.StartsWith("System.Windows", StringComparison.Ordinal));
    }

    [Fact]
    public void Application_verwijst_alleen_naar_domain()
    {
        var verwijzingen = Verwijzingen(Application).ToList();

        Assert.Contains("Verenigd.Domain", verwijzingen);
        Assert.DoesNotContain(verwijzingen, n =>
            n is "Verenigd.Infrastructure" or "Verenigd"
            || n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
            || n.StartsWith("Microsoft.Data.Sqlite", StringComparison.Ordinal)
            || n.StartsWith("Presentation", StringComparison.Ordinal));
    }

    [Fact]
    public void Infrastructure_verwijst_niet_naar_de_app()
    {
        Assert.DoesNotContain(Verwijzingen(Infrastructure), n => n is "Verenigd" || n.StartsWith("Presentation", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Leden", "Financien")]
    [InlineData("Financien", "Leden")]
    public void Leden_en_financien_kennen_elkaar_niet(string kant, string andereKant)
    {
        var overtredingen = new List<string>();
        foreach (var assembly in new[] { Domain, Application, Infrastructure })
        {
            foreach (var type in assembly.GetTypes().Where(t => InNamespace(t, kant)))
            {
                overtredingen.AddRange(GebruikteTypen(type)
                    .Where(t => InNamespace(t, andereKant))
                    .Select(t => $"{type.FullName} -> {t.FullName}"));
            }
        }

        Assert.Empty(overtredingen.Distinct());
    }

    [Theory]
    [InlineData("Leden", "Financien")]
    [InlineData("Financien", "Leden")]
    public void Broncode_van_leden_en_financien_kent_elkaar_niet(string kant, string andereKant)
    {
        var overtredingen = Directory
            .EnumerateFiles(Path.Combine(Repositorymap(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => f.Split(Path.DirectorySeparatorChar).Contains(kant))
            .Where(f => File.ReadAllText(f).Contains("." + andereKant, StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(overtredingen);
    }

    private static bool InNamespace(Type type, string deel) =>
        type.Namespace?.Split('.').Contains(deel, StringComparer.Ordinal) == true;

    /// <summary>Typen in de signatuur: velden, eigenschappen, methoden, constructors en basistypen.</summary>
    private static IEnumerable<Type> GebruikteTypen(Type type)
    {
        const BindingFlags alles = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var typen = new List<Type>();
        if (type.BaseType is { } basis)
        {
            typen.Add(basis);
        }

        typen.AddRange(type.GetInterfaces());
        typen.AddRange(type.GetFields(alles).Select(f => f.FieldType));
        typen.AddRange(type.GetProperties(alles).Select(p => p.PropertyType));
        foreach (var methode in type.GetMethods(alles).Cast<MethodBase>().Concat(type.GetConstructors(alles)))
        {
            typen.AddRange(methode.GetParameters().Select(p => p.ParameterType));
            if (methode is MethodInfo info)
            {
                typen.Add(info.ReturnType);
            }

            if (methode.GetMethodBody() is { } body)
            {
                typen.AddRange(body.LocalVariables.Select(v => v.LocalType));
            }
        }

        return typen.SelectMany(Uitgepakt);
    }

    private static IEnumerable<Type> Uitgepakt(Type type)
    {
        if (type.HasElementType && type.GetElementType() is { } element)
        {
            return Uitgepakt(element);
        }

        return type.IsGenericType ? [type, .. type.GetGenericArguments().SelectMany(Uitgepakt)] : [type];
    }

    private static string Repositorymap()
    {
        var map = new DirectoryInfo(AppContext.BaseDirectory);
        while (map is not null && !File.Exists(Path.Combine(map.FullName, "Verenigd.slnx")))
        {
            map = map.Parent;
        }

        return map?.FullName ?? throw new InvalidOperationException("Repositorymap niet gevonden.");
    }
}
