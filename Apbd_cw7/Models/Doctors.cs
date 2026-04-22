using System.ComponentModel.DataAnnotations;

namespace Apbd_cw7.Models;

public class Doctors
{
    [Required]
    public int IdDoctor { get; set; }
    [Required]
    public int IdSpecialization { get; set; }
    [Required]
    [MaxLength(80)]
    public string FirstName { get; set; } = string.Empty;
    [Required]
    [MaxLength(80)]
    public string LastName { get; set; } = string.Empty;
    [Required]
    [MaxLength(40)]
    public string LicenseNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}