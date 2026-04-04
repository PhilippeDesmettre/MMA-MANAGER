using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MmaManager.Models;

[Table("StyleCombat")]
public class StyleCombat
{
    [Key]
    public int StyleID { get; set; }

    [MaxLength(60)]
    public string Nom { get; set; } = string.Empty;
}
