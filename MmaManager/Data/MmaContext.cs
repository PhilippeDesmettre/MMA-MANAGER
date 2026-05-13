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
    public DbSet<Combattant>           Combattants            { get; set; }
    public DbSet<CombattantPartie>     CombattantsPartie      { get; set; }
    public DbSet<EntrainementPlanifie> EntrainementsPlanifies { get; set; }
    public DbSet<Agent>               Agents                  { get; set; }
    public DbSet<SurEntrainement>     SurEntrainements        { get; set; }
    public DbSet<CombatOrganisation>    CombatOrganisations     { get; set; }
    public DbSet<CombatPlanifie>        CombatsPlanifies        { get; set; }
    public DbSet<ResultatCombatPartie>  ResultatsCombat         { get; set; }
    public DbSet<StaffDisponible>       StaffDisponibles        { get; set; }
    public DbSet<StaffPartie>           StaffParties            { get; set; }
    public DbSet<ContratOrganisation>   ContratsOrganisation    { get; set; }
    public DbSet<Rivalite>              Rivalites               { get; set; }
    public DbSet<RankingEntry>          RankingEntries          { get; set; }
    public DbSet<ChampionCeinture>      ChampionCeintures       { get; set; }
    public DbSet<OrganisationCategorie> OrganisationCategories  { get; set; }

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

        // EntrainementPlanifie -> EntraineurJoueur
        modelBuilder.Entity<EntrainementPlanifie>()
            .HasOne(ep => ep.EntraineurJoueur)
            .WithMany()
            .HasForeignKey(ep => ep.EntraineurJoueurID)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        // EntrainementPlanifie : unicité (PartieID, CombattantID)
        modelBuilder.Entity<EntrainementPlanifie>()
            .HasIndex(ep => new { ep.PartieID, ep.CombattantID })
            .IsUnique();

        // SurEntrainement -> Combattant
        modelBuilder.Entity<SurEntrainement>()
            .HasOne(s => s.Combattant)
            .WithMany()
            .HasForeignKey(s => s.CombattantID)
            .OnDelete(DeleteBehavior.Restrict);

        // CombatOrganisation -> Pays
        modelBuilder.Entity<CombatOrganisation>()
            .HasOne(o => o.PaysOrigine)
            .WithMany()
            .HasForeignKey(o => o.PaysOrigineID)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        // OrganisationCategorie -> CombatOrganisation
        modelBuilder.Entity<OrganisationCategorie>()
            .HasOne(oc => oc.Organisation).WithMany().HasForeignKey(oc => oc.OrganisationID)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<OrganisationCategorie>()
            .HasIndex(oc => new { oc.OrganisationID, oc.CategorieID, oc.Genre }).IsUnique();

        // CombatOrganisation -> Partie (orgs locales)
        modelBuilder.Entity<CombatOrganisation>()
            .HasOne(o => o.Partie)
            .WithMany()
            .HasForeignKey(o => o.PartieID)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        // CombatPlanifie -> Combattant (le combattant de l'écurie)
        modelBuilder.Entity<CombatPlanifie>()
            .HasOne(cp => cp.Combattant)
            .WithMany()
            .HasForeignKey(cp => cp.CombattantID)
            .OnDelete(DeleteBehavior.Restrict);

        // CombatPlanifie -> Adversaire
        modelBuilder.Entity<CombatPlanifie>()
            .HasOne(cp => cp.Adversaire)
            .WithMany()
            .HasForeignKey(cp => cp.AdversaireID)
            .OnDelete(DeleteBehavior.Restrict);

        // CombatPlanifie -> Agent
        modelBuilder.Entity<CombatPlanifie>()
            .HasOne(cp => cp.Agent)
            .WithMany()
            .HasForeignKey(cp => cp.AgentID)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        // CombattantPartie -> Agent
        modelBuilder.Entity<CombattantPartie>()
            .HasOne(cp => cp.Agent)
            .WithMany()
            .HasForeignKey(cp => cp.AgentID)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        // CombatPlanifie -> Organisation
        modelBuilder.Entity<CombatPlanifie>()
            .HasOne(cp => cp.Organisation)
            .WithMany()
            .HasForeignKey(cp => cp.OrganisationID)
            .OnDelete(DeleteBehavior.Restrict);

        // ResultatCombatPartie -> Combattant (notre combattant)
        modelBuilder.Entity<ResultatCombatPartie>()
            .HasOne(r => r.Combattant)
            .WithMany()
            .HasForeignKey(r => r.CombattantID)
            .OnDelete(DeleteBehavior.Restrict);

        // ResultatCombatPartie -> Adversaire
        modelBuilder.Entity<ResultatCombatPartie>()
            .HasOne(r => r.Adversaire)
            .WithMany()
            .HasForeignKey(r => r.AdversaireID)
            .OnDelete(DeleteBehavior.Restrict);

        // StaffPartie -> Partie
        modelBuilder.Entity<StaffPartie>()
            .HasOne(sp => sp.Partie)
            .WithMany()
            .HasForeignKey(sp => sp.PartieID)
            .OnDelete(DeleteBehavior.Cascade);

        // StaffPartie -> StaffDisponible
        modelBuilder.Entity<StaffPartie>()
            .HasOne(sp => sp.StaffDisponible)
            .WithMany()
            .HasForeignKey(sp => sp.StaffDisponibleID)
            .OnDelete(DeleteBehavior.Restrict);

        // EntrainementPlanifie -> StaffPartie (NoAction pour éviter cycle cascade SQL Server)
        modelBuilder.Entity<EntrainementPlanifie>()
            .HasOne(ep => ep.StaffPartie)
            .WithMany()
            .HasForeignKey(ep => ep.StaffPartieID)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .IsRequired(false);

        // ContratOrganisation -> Combattant
        modelBuilder.Entity<ContratOrganisation>()
            .HasOne(c => c.Combattant)
            .WithMany()
            .HasForeignKey(c => c.CombattantID)
            .OnDelete(DeleteBehavior.Restrict);

        // ContratOrganisation -> Organisation
        modelBuilder.Entity<ContratOrganisation>()
            .HasOne(c => c.Organisation)
            .WithMany()
            .HasForeignKey(c => c.OrganisationID)
            .OnDelete(DeleteBehavior.Restrict);

        // Rivalite -> Partie
        modelBuilder.Entity<Rivalite>()
            .HasOne(r => r.Partie)
            .WithMany()
            .HasForeignKey(r => r.PartieID)
            .OnDelete(DeleteBehavior.Cascade);

        // Rivalite -> Combattant1
        modelBuilder.Entity<Rivalite>()
            .HasOne(r => r.Combattant1)
            .WithMany()
            .HasForeignKey(r => r.Combattant1ID)
            .OnDelete(DeleteBehavior.Restrict);

        // Rivalite -> Combattant2
        modelBuilder.Entity<Rivalite>()
            .HasOne(r => r.Combattant2)
            .WithMany()
            .HasForeignKey(r => r.Combattant2ID)
            .OnDelete(DeleteBehavior.Restrict);

        // Rivalite : unicité (PartieID, Combattant1ID, Combattant2ID)
        modelBuilder.Entity<Rivalite>()
            .HasIndex(r => new { r.PartieID, r.Combattant1ID, r.Combattant2ID })
            .IsUnique();

        // RankingEntry
        modelBuilder.Entity<RankingEntry>()
            .HasOne(r => r.Combattant).WithMany().HasForeignKey(r => r.CombattantID).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RankingEntry>()
            .HasOne(r => r.Organisation).WithMany().HasForeignKey(r => r.OrganisationID).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RankingEntry>()
            .HasOne(r => r.Partie).WithMany().HasForeignKey(r => r.PartieID).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RankingEntry>()
            .HasIndex(r => new { r.PartieID, r.OrganisationID, r.CombattantID }).IsUnique();

        // ChampionCeinture
        modelBuilder.Entity<ChampionCeinture>()
            .HasOne(c => c.Combattant).WithMany().HasForeignKey(c => c.CombattantID)
            .OnDelete(DeleteBehavior.SetNull).IsRequired(false);
        modelBuilder.Entity<ChampionCeinture>()
            .HasOne(c => c.Organisation).WithMany().HasForeignKey(c => c.OrganisationID).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ChampionCeinture>()
            .HasOne(c => c.Partie).WithMany().HasForeignKey(c => c.PartieID).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ChampionCeinture>()
            .HasIndex(c => new { c.PartieID, c.OrganisationID, c.CategorieID, c.Genre }).IsUnique();

        // Gym n'a pas de navigation vers Pays, EF ne crée pas de relation automatique
    }
}
