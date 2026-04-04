using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CodexTest.Models;

[Table("Gym")]
public class Gym
{
    [Key]
    public int GymID { get; set; }

    [Required]
    [MaxLength(120)]
    public string Nom { get; set; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string Ville { get; set; } = string.Empty;
}
