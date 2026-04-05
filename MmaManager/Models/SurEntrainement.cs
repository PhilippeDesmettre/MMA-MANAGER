using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MmaManager.Models;

/// <summary>
/// Enregistre une période de sur-entraînement pour un combattant.
/// Causé par trop de combats ou entraînements consécutifs.
/// Peut entraîner une régression de stats ou des blessures (futur).
/// </summary>
[Table("SurEntrainement")]
public class SurEntrainement
{
    [Key]
    public int SurEntrainementID { get; set; }

    public int CombattantID { get; set; }
    public int PartieID     { get; set; }

    /// <summary>Tour où le sur-entraînement commence</summary>
    public int TourDebut { get; set; }

    /// <summary>Tour où le sur-entraînement se termine (inclusif)</summary>
    public int TourFin { get; set; }

    [MaxLength(200)]
    public string? Cause { get; set; } // "CombatsConsecutifs" | "ExcesEntrainement"

    // Navigation
    public Combattant? Combattant { get; set; }
}
