using Microsoft.EntityFrameworkCore;
using CodexTest.Models;

namespace CodexTest.Data;

public class MmaContext(DbContextOptions<MmaContext> options) : DbContext(options)
{
    public DbSet<Gym>             Gyms             { get; set; }
    public DbSet<User>            Users            { get; set; }
    public DbSet<Pays>            Pays             { get; set; }
    public DbSet<Background>      Backgrounds      { get; set; }
    public DbSet<Partie>          Parties          { get; set; }
    public DbSet<EntraineurJoueur> EntraineursJoueur { get; set; }

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

        // Gym n'a pas de navigation vers Pays, EF ne crée pas de relation automatique
    }
}
