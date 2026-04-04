using Microsoft.EntityFrameworkCore;
using MmaManager.Models;

namespace MmaManager.Data;

public class MmaContext(DbContextOptions<MmaContext> options) : DbContext(options)
{
    public DbSet<Gym>              Gyms              { get; set; }
    public DbSet<User>             Users             { get; set; }
    public DbSet<Pays>             Pays              { get; set; }
    public DbSet<StyleCombat>      StylesCombat      { get; set; }
    public DbSet<CategoriePoidsH>  CategoriesPoidsH  { get; set; }
    public DbSet<CategoriePoidsF>  CategoriesPoidsF  { get; set; }
    public DbSet<Background>       Backgrounds       { get; set; }
    public DbSet<Partie>           Parties           { get; set; }
    public DbSet<EntraineurJoueur> EntraineursJoueur { get; set; }
    public DbSet<Combattant>           Combattants           { get; set; }
    public DbSet<CombattantPartie>     CombattantsPartie     { get; set; }
    public DbSet<EntrainementPlanifie> EntrainementsPlanifies { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Partie -> EntraineurJoueur (one-to-one)
        modelBuilder.Entity<Partie>()
            .HasOne(p => p.Entraineur)
            .WithOne()
            .HasForeignKey<EntraineurJoueur>(e => e.PartieID)
            .OnDelete(DeleteBehavior.Cascade);

        // EntraineurJoueur -> Background
        modelBuilder.Entity<EntraineurJoueur>()
            .HasOne(e => e.Background)
            .WithMany()
            .HasForeignKey(e => e.BackgroundID)
            .OnDelete(DeleteBehavior.Restrict);

        // Combattant -> Pays d'origine
        modelBuilder.Entity<Combattant>()
            .HasOne(c => c.PaysOrigine)
            .WithMany()
            .HasForeignKey(c => c.PaysOrigineID)
            .OnDelete(DeleteBehavior.Restrict);

        // CombattantPartie -> Combattant
        modelBuilder.Entity<CombattantPartie>()
            .HasOne(cp => cp.Combattant)
            .WithMany()
            .HasForeignKey(cp => cp.CombattantID)
            .OnDelete(DeleteBehavior.Restrict);

        // CombattantPartie -> Partie
        modelBuilder.Entity<CombattantPartie>()
            .HasOne(cp => cp.Partie)
            .WithMany()
            .HasForeignKey(cp => cp.PartieID)
            .OnDelete(DeleteBehavior.Cascade);

        // EntrainementPlanifie -> Combattant
        modelBuilder.Entity<EntrainementPlanifie>()
            .HasOne(ep => ep.Combattant)
            .WithMany()
            .HasForeignKey(ep => ep.CombattantID)
            .OnDelete(DeleteBehavior.Restrict);

        // EntrainementPlanifie : unicité (PartieID, CombattantID)
        modelBuilder.Entity<EntrainementPlanifie>()
            .HasIndex(ep => new { ep.PartieID, ep.CombattantID })
            .IsUnique();

        // Gym n'a pas de navigation vers Pays, EF ne crée pas de relation automatique
    }
}
