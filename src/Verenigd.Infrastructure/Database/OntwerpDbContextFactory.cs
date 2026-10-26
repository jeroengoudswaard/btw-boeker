using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Verenigd.Infrastructure.Database;

/// <summary>Alleen voor <c>dotnet ef migrations</c>; wordt niet door de applicatie gebruikt.</summary>
internal sealed class OntwerpDbContextFactory : IDesignTimeDbContextFactory<VerenigdDbContext>
{
    public VerenigdDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<VerenigdDbContext>().UseSqlite("Data Source=:memory:").Options);
}
