using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MmaManager.Models;

[Table("ResultatCombatPartie")]
public class ResultatCombatPartie
{
    [Key]
    public int ResultatID { get; set; }

    public int PartieID     { get; set; }
    public int CombattantID { get; set; }
    public int AdversaireID { get; set; }

    [MaxLength(100)]
    public string OrganisationNom { get; set; } = string.Empty;

    public int TourCombat { get; set; }

    /// <summary>true = victoire, false = défaite, null = nul</summary>
    public bool? EstVictoire { get; set; }

    public bool EstNul { get; set; }

    [MaxLength(40)]
    public string? MethodeVictoire { get; set; }

    public byte RoundFin { get; set; } = 3;

    [MaxLength(200)]
    public string? Details { get; set; }

    // Navigation
    public Combattant? Combattant { get; set; }
    public Combattant? Adversaire { get; set; }
}
