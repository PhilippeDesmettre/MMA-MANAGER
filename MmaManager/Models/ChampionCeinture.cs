using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MmaManager.Models;

[Table("ChampionCeinture")]
public class ChampionCeinture
{
    [Key]
    public int ChampionCeintureID { get; set; }

    public int PartieID       { get; set; }
    public int OrganisationID { get; set; }
    public int CategorieID    { get; set; }

    [MaxLength(1)]
    public string Genre { get; set; } = "H";

    public int? CombattantID  { get; set; }
    public int  TourObtention { get; set; }
    public int  NbDefenses    { get; set; } = 0;

    public Partie?             Partie       { get; set; }
    public CombatOrganisation? Organisation { get; set; }
    public Combattant?         Combattant   { get; set; }
}
