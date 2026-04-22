using System.ComponentModel.DataAnnotations;

namespace Apbd_cw7.Models;

public class Specializations
{
    [Required]
    public int IdSpecialization { get; set; }
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}
