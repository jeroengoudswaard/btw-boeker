using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Bijlagen;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.Infrastructure.Database.Financien;

/// <summary>Bijlagen bij journaalposten (stap 4c-3). De inhoud staat in een eigen tabel, zodat lijsten licht blijven.</summary>
internal static class BijlageConfiguratie
{
    private static readonly ValueConverter<Bedrag, long> BedragConverter = new(b => b.Centen, c => new Bedrag(c));

    public static void Configureer(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Bijlage>(b =>
        {
            b.ToTable("Bijlagen");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Bestandsnaam).IsRequired().HasMaxLength(Bijlage.MaximaleNaamLengte);
            b.Property(x => x.Controlecode).IsRequired().HasMaxLength(64);
            b.Property(x => x.Bedrag).HasConversion(BedragConverter);
            // Een verwijderd concept zet zijn bijlagen terug in het postvak; een bon gaat nooit ongemerkt verloren.
            b.HasOne<Journaalpost>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.Administratie);
            b.HasIndex(x => x.Controlecode);
            b.Ignore(x => x.IsInPostvak);
        });

        modelBuilder.Entity<BijlageInhoud>(b =>
        {
            b.ToTable("BijlageInhoud");
            b.HasKey(x => x.BijlageId);
            b.Property(x => x.Gegevens).IsRequired();
            b.HasOne<Bijlage>().WithOne().HasForeignKey<BijlageInhoud>(x => x.BijlageId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
