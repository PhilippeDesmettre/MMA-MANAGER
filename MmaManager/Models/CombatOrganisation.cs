using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MmaManager.Models;

[Table("CombatOrganisation")]
public class CombatOrganisation
{
    [Key]
    public int OrganisationID { get; set; }

    [MaxLength(100)]
    public string Nom { get; set; } = string.Empty;

    /// <summary>Pays du siège / d'origine (FK Pays.PaysID)</summary>
    public int? PaysOrigineID { get; set; }

    /// <summary>Année de fondation réelle ou fictive</summary>
    public int AnneeCreation { get; set; }

    /// <summary>Prestige de 1 (mineur/local) à 5 (mondiale, référence)</summary>
    public int Prestige { get; set; } = 1;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>True = organisation fictive créée pour le jeu</summary>
    public bool EstFictive { get; set; } = false;

    /// <summary>Non-null = organisation locale liée à une partie spécifique</summary>
    public int? PartieID { get; set; }

    // Navigation
    public Pays?   PaysOrigine { get; set; }
    public Partie? Partie      { get; set; }
}
