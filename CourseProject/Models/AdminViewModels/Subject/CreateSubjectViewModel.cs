namespace CourseProject.Models.AdminViewModels.Subject;

using System.ComponentModel.DataAnnotations;
using DataBase.DbModels;

public class CreateSubjectViewModel
{
    [Required(ErrorMessage = "Название предмета обязательно")] public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public List<string> SelectedTeacherIds { get; set; } = new();
    public List<User> AvailableTeachers { get; set; } = new();
    public List<int> SelectedGroupIds { get; set; } = new();
    public List<Group> AvailableGroups { get; set; } = new();
}