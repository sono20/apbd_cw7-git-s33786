using System.ComponentModel.DataAnnotations;

namespace Apbd_cw7.Models;

public class Patients
{
    [Required]
    public int IdPatient { get; set; }
    [Required]
    [MaxLength(80)]
    public string FirstName { get; set; } = string.Empty;
    [Required]
    [MaxLength(80)]
    public string LastName { get; set; } = string.Empty;
    [Required]
    [MaxLength(120)]
    public string Email { get; set; } = string.Empty;
    [Required]
    [MaxLength(30)]
    public string PhoneNumber { get; set; } = string.Empty;
    [Required]
    public DateOnly DateOfBirth { get; set; } = new DateOnly();
    public bool IsActive { get; set; } = true;
}