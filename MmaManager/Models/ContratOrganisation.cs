using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MmaManager.Models;

/// <summary>
/// Contrat entre un combattant et une organisation de combat.
/// Peut être exclusif (le combattant ne peut combattre que dans cette org
/// jusqu'à avoir effectué tous les combats du contrat).
/// </summary>
[Table("ContratOrganisation")]
public class ContratOrganisation
{
    [Key]
    public int ContratID { get; set; }

    public int PartieID       { get; set; }
    public int CombattantID   { get; set; }
    public int OrganisationID { get; set; }

    /// <summary>Nombre total de combats prévus dans le contrat</summary>
    public int NombreCombats { get; set; } = 1;

    /// <summary>Nombre de combats déjà effectués</summary>
    public int CombatsEffectues { get; set; } = 0;

    /// <summary>Si vrai, le combattant ne peut combattre que dans cette organisation tant que le contrat est actif</summary>
    public bool EstExclusif { get; set; } = false;

    /// <summary>Tour de début du contrat</summary>
    public int TourDebut { get; set; }

    /// <summary>Actif | Termine</summary>
    [MaxLength(20)]
    public string Statut { get; set; } = "Actif";

    // Navigation
    public Partie?             Partie       { get; set; }
    public Combattant?         Combattant   { get; set; }
    public CombatOrganisation? Organisation { get; set; }
}
