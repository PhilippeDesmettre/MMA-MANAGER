using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MmaManager.Models;

[Table("CategoriePoidsF")]
public class CategoriePoidsF
{
    [Key]
    public int CategorieID { get; set; }

    [MaxLength(40)]
    public string Nom { get; set; } = string.Empty;
}
