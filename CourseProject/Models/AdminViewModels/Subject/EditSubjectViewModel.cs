using System.ComponentModel.DataAnnotations;
using CourseProject.DataBase.DbModels;

namespace CourseProject.Models.AdminViewModels.Subject;

public class EditSubjectViewModel
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Название предмета обязательно")]
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public List<string> SelectedTeacherIds { get; set; } = new();
    public List<User> AvailableTeachers { get; set; } = new();
}