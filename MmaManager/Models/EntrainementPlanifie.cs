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

    /// <summary>Coach joueur assigné — null si staff embauché utilisé</summary>
    public int? EntraineurJoueurID { get; set; }

    /// <summary>Staff embauché assigné — null si entraîneur joueur utilisé</summary>
    public int? StaffPartieID { get; set; }

    // Navigation
    public Combattant?       Combattant       { get; set; }
    public EntraineurJoueur? EntraineurJoueur { get; set; }
    public StaffPartie?      StaffPartie      { get; set; }
}
