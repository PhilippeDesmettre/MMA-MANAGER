using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MmaManager.Models;

[Table("CombattantPartie")]
public class CombattantPartie
{
    [Key]
    public int CombattantPartieID { get; set; }

    public int CombattantID { get; set; }
    public int PartieID     { get; set; }

    public DateTime DateRecrutement { get; set; } = DateTime.UtcNow;

    // Navigation
    public Combattant? Combattant { get; set; }
    public Partie?     Partie     { get; set; }
}
