using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MmaManager.Models;

[Table("RankingEntry")]
public class RankingEntry
{
    [Key]
    public int RankingEntryID { get; set; }

    public int PartieID       { get; set; }
    public int CombattantID   { get; set; }
    public int OrganisationID { get; set; }

    [MaxLength(1)]
    public string Genre      { get; set; } = "H";

    public int CategorieID   { get; set; }
    public int Points         { get; set; } = 0;
    public int Rang           { get; set; } = 0;
    public int PointsMondiaux { get; set; } = 0;

    public Partie?             Partie       { get; set; }
    public Combattant?         Combattant   { get; set; }
    public CombatOrganisation? Organisation { get; set; }
}
