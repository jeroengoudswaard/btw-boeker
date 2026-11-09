using Microsoft.EntityFrameworkCore;
using Verenigd.Domain.Financien.Relaties;

namespace Verenigd.Infrastructure.Database.Financien;

internal static class RelatieConfiguratie
{
    public static void Configureer(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Relatie>(b =>
        {
            b.ToTable("Relaties");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Naam).IsRequired().HasMaxLength(100);
            b.Property(x => x.Contactpersoon).HasMaxLength(100);
            b.Property(x => x.Straat).HasMaxLength(100);
            b.Property(x => x.Postcode).HasMaxLength(10);
            b.Property(x => x.Plaats).HasMaxLength(100);
            b.Property(x => x.Email).HasMaxLength(254);
            b.Property(x => x.Telefoon).HasMaxLength(20);
            b.Property(x => x.Iban).HasMaxLength(34);
            b.Property(x => x.Notitie).HasMaxLength(2000);
            b.Ignore(x => x.Adresregels);
            b.HasIndex(x => x.Naam);
        });
    }
}
