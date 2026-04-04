using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MmaManager.Models;

[Table("CategoriePoidsH")]
public class CategoriePoidsH
{
    [Key]
    public int CategorieID { get; set; }

    [MaxLength(40)]
    public string Nom { get; set; } = string.Empty;
}
