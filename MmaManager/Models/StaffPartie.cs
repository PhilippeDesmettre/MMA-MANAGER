using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MmaManager.Models;

/// <summary>
/// Staff embauché pour une partie spécifique.
/// Chaque ligne = un entraîneur actif dans l'écurie du joueur.
/// </summary>
[Table("StaffPartie")]
public class StaffPartie
{
    [Key]
    public int StaffPartieID { get; set; }

    public int PartieID          { get; set; }
    public int StaffDisponibleID { get; set; }

    public DateTime DateEmbauche { get; set; } = DateTime.UtcNow;

    // Navigation
    public Partie?          Partie          { get; set; }
    public StaffDisponible? StaffDisponible { get; set; }
}
