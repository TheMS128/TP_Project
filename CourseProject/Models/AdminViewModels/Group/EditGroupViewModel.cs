using System.ComponentModel.DataAnnotations;
using CourseProject.DataBase.DbModels;

namespace CourseProject.Models.AdminViewModels.Group;

public class EditGroupViewModel
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Название группы обязательно")] public string GroupName { get; set; } = null!;
    public List<string> SelectedStudentIds { get; set; } = new();
    public List<User> AvailableStudents { get; set; } = new();
}