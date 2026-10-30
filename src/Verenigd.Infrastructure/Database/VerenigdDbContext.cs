using Microsoft.EntityFrameworkCore;
using Verenigd.Domain.Administraties;
using Verenigd.Domain.Financien;
using Verenigd.Domain.Financien.Boekhouding;
using Verenigd.Domain.Leden;
using Verenigd.Infrastructure.Database.Financien;
using Verenigd.Infrastructure.Database.Leden;

namespace Verenigd.Infrastructure.Database;

public sealed class VerenigdDbContext(DbContextOptions<VerenigdDbContext> options) : DbContext(options)
{
    public DbSet<Administratie> Administraties => Set<Administratie>();

    public DbSet<Persoon> Personen => Set<Persoon>();
    public DbSet<Groep> Groepen => Set<Groep>();
    public DbSet<Betaler> Betalers => Set<Betaler>();
    public DbSet<BetalerLid> BetalerLeden => Set<BetalerLid>();
    public DbSet<Contributiecategorie> Contributiecategorieen => Set<Contributiecategorie>();
    public DbSet<Tarief> Tarieven => Set<Tarief>();
    public DbSet<LidCategorie> LidCategorieen => Set<LidCategorie>();
    public DbSet<Incassoinstellingen> Incassoinstellingen => Set<Incassoinstellingen>();
    public DbSet<Incassorun> Incassoruns => Set<Incassorun>();
    public DbSet<Rubriek> Rubrieken => Set<Rubriek>();
    public DbSet<Grootboekrekening> Grootboekrekeningen => Set<Grootboekrekening>();
    public DbSet<Dagboek> Dagboeken => Set<Dagboek>();
    public DbSet<Boekjaar> Boekjaren => Set<Boekjaar>();
    public DbSet<Journaalpost> Journaalposten => Set<Journaalpost>();
    public DbSet<HistorischeRegel> HistorischeRegels => Set<HistorischeRegel>();
    public DbSet<HistorischTotaal> HistorischeTotalen => Set<HistorischTotaal>();
    public DbSet<Domain.Financien.Bank.Bankafschrift> Bankafschriften => Set<Domain.Financien.Bank.Bankafschrift>();
    public DbSet<Domain.Financien.Bank.Bankmutatie> Bankmutaties => Set<Domain.Financien.Bank.Bankmutatie>();
    public DbSet<Domain.Financien.Bank.Boekingsregel> Boekingsregels => Set<Domain.Financien.Bank.Boekingsregel>();
    public DbSet<Domain.Financien.Debiteuren.Debiteurmutatie> Debiteurmutaties => Set<Domain.Financien.Debiteuren.Debiteurmutatie>();
    public DbSet<Domain.Financien.Debiteuren.Herincasso> Herincassos => Set<Domain.Financien.Debiteuren.Herincasso>();
    public DbSet<Domain.Financien.Relaties.Relatie> Relaties => Set<Domain.Financien.Relaties.Relatie>();
    public DbSet<Domain.Financien.Facturen.Verkoopfactuur> Verkoopfacturen => Set<Domain.Financien.Facturen.Verkoopfactuur>();
    public DbSet<Domain.Financien.Facturen.Factuurinstellingen> Factuurinstellingen => Set<Domain.Financien.Facturen.Factuurinstellingen>();
    public DbSet<Domain.Financien.Bijlagen.Bijlage> Bijlagen => Set<Domain.Financien.Bijlagen.Bijlage>();
    public DbSet<Domain.Financien.Begrotingen.Begroting> Begrotingen => Set<Domain.Financien.Begrotingen.Begroting>();
    public DbSet<Domain.Financien.Begrotingen.Meerjarenplan> Meerjarenplannen => Set<Domain.Financien.Begrotingen.Meerjarenplan>();
    public DbSet<Domain.Financien.Bijlagen.BijlageInhoud> BijlageInhoud => Set<Domain.Financien.Bijlagen.BijlageInhoud>();
    internal DbSet<StartwizardRij> Startwizard => Set<StartwizardRij>();
    public DbSet<Domain.Communicatie.Emailsjabloon> Emailsjablonen => Set<Domain.Communicatie.Emailsjabloon>();
    public DbSet<Domain.Communicatie.VerzondenBericht> VerzondenBerichten => Set<Domain.Communicatie.VerzondenBericht>();
    public DbSet<Domain.Communicatie.Emailinstellingen> Emailinstellingen => Set<Domain.Communicatie.Emailinstellingen>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Administratie>(b =>
        {
            b.ToTable("Administraties");
            b.HasKey(a => a.Id);
            b.Property(a => a.Soort).IsRequired();
            b.HasIndex(a => a.Soort).IsUnique();
            b.Property(a => a.Naam).IsRequired().HasMaxLength(Administratie.MaximaleNaamLengte);
            b.Property(a => a.Accentkleur)
                .IsRequired()
                .HasMaxLength(7)
                .HasConversion(k => k.ToString(), s => Kleur.Parse(s));

            b.HasData(
                new
                {
                    Id = 1,
                    Soort = AdministratieSoort.Vereniging,
                    Naam = "Vereniging",
                    Accentkleur = Administratie.Standaardkleur(AdministratieSoort.Vereniging),
                },
                new
                {
                    Id = 2,
                    Soort = AdministratieSoort.Stichting,
                    Naam = "Stichting",
                    Accentkleur = Administratie.Standaardkleur(AdministratieSoort.Stichting),
                });
        });

        LedenConfiguratie.Configureer(modelBuilder);
        FinancienConfiguratie.Configureer(modelBuilder);
        IncassoConfiguratie.Configureer(modelBuilder);
        BoekhoudingConfiguratie.Configureer(modelBuilder);
        FactuurConfiguratie.Configureer(modelBuilder);
        BijlageConfiguratie.Configureer(modelBuilder);
        BegrotingConfiguratie.Configureer(modelBuilder);
        modelBuilder.Entity<StartwizardRij>(b =>
        {
            b.ToTable("Startwizard");
            b.HasKey(r => r.Id);
            b.Property(r => r.Id).ValueGeneratedNever();
            b.Property(r => r.Stap).HasConversion<string>().HasMaxLength(20);
        });
        modelBuilder.Entity<Domain.Communicatie.Emailsjabloon>(b =>
        {
            b.ToTable("Emailsjablonen");
            b.HasKey(s => s.Id);
            b.Property(s => s.Naam).IsRequired().HasMaxLength(Domain.Communicatie.Emailsjabloon.MaximaleNaamLengte);
            b.Property(s => s.Onderwerp).IsRequired().HasMaxLength(Domain.Communicatie.Emailsjabloon.MaximaleOnderwerpLengte);
            b.Property(s => s.Tekst).IsRequired();
        });
        modelBuilder.Entity<Domain.Communicatie.VerzondenBericht>(b =>
        {
            b.ToTable("VerzondenBerichten");
            b.HasKey(v => v.Id);
            b.Property(v => v.Naam).IsRequired().HasMaxLength(200);
            b.Property(v => v.Adres).IsRequired().HasMaxLength(320);
            b.Property(v => v.Onderwerp).IsRequired().HasMaxLength(300);
            b.Property(v => v.Sjabloon).IsRequired().HasMaxLength(100);
            b.Property(v => v.Fout).HasMaxLength(300);
            b.HasIndex(v => v.TijdstipUtc);
        });
        modelBuilder.Entity<Domain.Communicatie.Emailinstellingen>(b =>
        {
            b.ToTable("Emailinstellingen");
            b.HasKey(i => i.Id);
            b.Property(i => i.Id).ValueGeneratedNever();
            b.Property(i => i.Afzendernaam).HasMaxLength(200);
            b.Property(i => i.Afzenderadres).HasMaxLength(320);
            b.Property(i => i.ClientId).HasMaxLength(300);
            b.Property(i => i.ClientGeheim).HasMaxLength(300);
            b.Ignore(i => i.HeeftClient);
            b.Ignore(i => i.IsGekoppeld);
        });
    }
}
