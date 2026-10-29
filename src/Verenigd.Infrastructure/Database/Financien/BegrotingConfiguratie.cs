using Microsoft.EntityFrameworkCore;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Begrotingen;
using Verenigd.Domain.Financien.Boekhouding;

namespace Verenigd.Infrastructure.Database.Financien;

/// <summary>Begrotingen per jaar en versie (fase 5a); staan los van journaal en grootboek.</summary>
internal static class BegrotingConfiguratie
{
    public static void Configureer(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Begroting>(b =>
        {
            b.ToTable("Begrotingen");
            b.HasKey(x => x.Id);
            b.Property(x => x.Omschrijving).IsRequired().HasMaxLength(Begroting.MaximaleOmschrijvingLengte);
            b.HasIndex(x => new { x.Administratie, x.Jaar, x.Versie }).IsUnique();
            b.HasMany(x => x.Regels).WithOne().HasForeignKey("BegrotingId").IsRequired().OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Regels).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.Ignore(x => x.IsVastgesteld);
        });

        modelBuilder.Entity<Begrotingsregel>(b =>
        {
            b.ToTable("Begrotingsregels");
            b.HasKey(x => x.Id);
            b.HasOne(x => x.Rekening).WithMany().HasForeignKey("RekeningId").IsRequired().OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Bedrag).HasConversion(v => v.Centen, c => new Bedrag(c));
            b.Property(x => x.Toelichting).HasMaxLength(Begrotingsregel.MaximaleToelichtingLengte);
            b.HasIndex("BegrotingId", "RekeningId").IsUnique();
        });
    }
}
