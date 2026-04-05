using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MmaManager.Models;

/// <summary>
/// Combat planifié pour un combattant de l'écurie.
/// L'agent négocie le contrat, l'organisation est choisie,
/// et la date du combat est prévue en tours.
/// </summary>
[Table("CombatPlanifie")]
public class CombatPlanifie
{
    [Key]
    public int CombatPlanifieID { get; set; }

    public int PartieID       { get; set; }
    public int CombattantID   { get; set; }
    public int AdversaireID   { get; set; }  // combattant adverse
    public int? AgentID       { get; set; }  // null = joueur agent par défaut
    public int OrganisationID { get; set; }

    /// <summary>Tour (mois) où le combat doit avoir lieu</summary>
    public int TourPrevu { get; set; }

    /// <summary>Planifie | Effectue | Annule</summary>
    [MaxLength(20)]
    public string Statut { get; set; } = "Planifie";

    /// <summary>Striking | Grappling | Balanced</summary>
    [MaxLength(20)]
    public string Gameplan { get; set; } = "Balanced";

    // Navigation
    public Combattant?         Combattant    { get; set; }
    public Combattant?         Adversaire    { get; set; }
    public Agent?              Agent         { get; set; }
    public CombatOrganisation? Organisation  { get; set; }
}
