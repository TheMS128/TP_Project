using System.ComponentModel.DataAnnotations;

namespace CourseProject.Models.AdminViewModels.Student;

public class EditStudentViewModel
{
    public string Id { get; set; } = null!;
    [Required] public string FullName { get; set; } = null!;
    [Required, EmailAddress] public string Email { get; set; } = null!;
    public string? Description { get; set; }
    public int? GroupId { get; set; }
}