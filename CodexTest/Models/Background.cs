using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MmaManager.Models;

[Table("Background")]
public class Background
{
    [Key]
    public int BackgroundID { get; set; }

    [MaxLength(120)]
    public string Nom { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(10)]
    public string Icone { get; set; } = string.Empty;

    // Bonus ajoutés au stat de base (30) lors de la création
    public int BonusStriking     { get; set; }
    public int BonusLutte        { get; set; }
    public int BonusGrappling    { get; set; }
    public int BonusConditioning { get; set; }
    public int BonusStamina      { get; set; }
    public int BonusMental       { get; set; }
    public int BonusStrategie    { get; set; }
    public int BonusNegociation  { get; set; }
    public int BonusMotivation   { get; set; }
}
