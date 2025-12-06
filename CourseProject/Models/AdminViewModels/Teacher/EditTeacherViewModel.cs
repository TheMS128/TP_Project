using System.ComponentModel.DataAnnotations;

namespace CourseProject.Models.AdminViewModels.Teacher;

public class EditTeacherViewModel
{
    public string Id { get; set; } = null!;
    [Required] public string FullName { get; set; } = null!;
    [Required, EmailAddress] public string Email { get; set; } = null!;
    public string? Description { get; set; }
    public List<int> SelectedSubjectIds { get; set; } = new();
}