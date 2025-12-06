using System.ComponentModel.DataAnnotations;
using CourseProject.DataBase.DbModels;
using CourseProject.DataBase.Enums;

namespace CourseProject.Models.SubjectModels;

public class EditSubjectModel
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Название предмета обязательно")] public string Title { get; set; } = null!;
    public string? Description { get; set; }
    [Display(Name = "Статус публикации")] public ContentStatus Status { get; set; }
    public List<string> SelectedTeacherIds { get; set; } = new();
    public List<User> AvailableTeachers { get; set; } = new();
}