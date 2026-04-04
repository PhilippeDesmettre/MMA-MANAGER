using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CodexTest.Models;

[Table("Pays")]
public class Pays
{
    [Key]
    public int PaysID { get; set; }

    [MaxLength(3)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(80)]
    public string Nom { get; set; } = string.Empty;

    [MaxLength(40)]
    public string Continent { get; set; } = string.Empty;
}
