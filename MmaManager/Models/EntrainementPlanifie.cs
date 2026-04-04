using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MmaManager.Models;

[Table("EntrainementPlanifie")]
public class EntrainementPlanifie
{
    [Key]
    public int EntrainementPlanifieID { get; set; }

    public int PartieID     { get; set; }
    public int CombattantID { get; set; }

    /// <summary>Striking | Lutte | Grappling | Conditionnement | Mental</summary>
    [MaxLength(20)]
    public string TypeEntrainement { get; set; } = string.Empty;

    // Navigation
    public Combattant? Combattant { get; set; }
}
