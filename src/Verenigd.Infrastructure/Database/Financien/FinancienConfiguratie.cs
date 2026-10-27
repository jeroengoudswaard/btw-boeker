using Microsoft.EntityFrameworkCore;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Gedeeld;

namespace Verenigd.Infrastructure.Database.Financien;

/// <summary>
/// Tabellen van de financiële gegevens (besluit 35). De koppeling met leden loopt alleen via het lidnummer,
/// zonder foreign key naar de ledentabellen (besluit 36).
/// </summary>
internal static class FinancienConfiguratie
{
    public static void Configureer(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Betaler>(b =>
        {
            b.ToTable("Betalers");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Iban).IsRequired().HasMaxLength(34).HasConversion(i => i.Waarde, s => Iban.Maak(s));
            b.HasIndex(x => x.Iban).IsUnique();
            b.Property(x => x.Rekeninghouder).HasMaxLength(Betaler.MaximaleNaamLengte);
        });

        modelBuilder.Entity<BetalerLid>(b =>
        {
            b.ToTable("BetalerLeden");
            b.HasKey(x => x.Lidnummer);
            b.Property(x => x.Lidnummer).HasMaxLength(20).HasConversion(l => l.Waarde, s => Lidnummer.Parse(s));
            b.HasOne<Betaler>().WithMany().HasForeignKey(x => x.BetalerId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
