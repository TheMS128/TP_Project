using System.ComponentModel.DataAnnotations;
using CourseProject.DataBase.DbModels;

namespace CourseProject.Models.AdminViewModels.Teacher;

public class AssignTeacherViewModel
{
    public int SubjectId { get; set; }
    public string SubjectTitle { get; set; } = null!;
    [Display(Name = "Преподаватель для назначения")] public string? SelectedTeacherId { get; set; }
    public List<User> AvailableTeachers { get; set; } = new();
    [Display(Name = "Преподаватель для снятия")] public string? SelectedTeacherToRemoveId { get; set; }
    public List<User> AssignedTeachers { get; set; } = new();
}