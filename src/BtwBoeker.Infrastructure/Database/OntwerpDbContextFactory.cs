using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BtwBoeker.Infrastructure.Database;

/// <summary>Alleen voor <c>dotnet ef migrations</c>; wordt niet door de applicatie gebruikt.</summary>
internal sealed class OntwerpDbContextFactory : IDesignTimeDbContextFactory<BtwBoekerDbContext>
{
    public BtwBoekerDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<BtwBoekerDbContext>().UseSqlite("Data Source=:memory:").Options);
}
