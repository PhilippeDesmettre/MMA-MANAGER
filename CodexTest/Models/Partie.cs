using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MmaManager.Models;

[Table("Partie")]
public class Partie
{
    [Key]
    public int PartieID { get; set; }

    public int UserID { get; set; }

    /// <summary>NoRules | GoldenAge | Modern</summary>
    [MaxLength(30)]
    public string Epoque { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Argent { get; set; } = 1000m;

    public DateTime DateCreation          { get; set; } = DateTime.UtcNow;
    public DateTime DateDerniereConnexion { get; set; } = DateTime.UtcNow;
    public bool     EstActive             { get; set; } = true;

    // Navigation
    public EntraineurJoueur? Entraineur { get; set; }
}
