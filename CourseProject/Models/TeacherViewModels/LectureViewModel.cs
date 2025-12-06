using System.ComponentModel.DataAnnotations;
using CourseProject.DataBase.Enums;

namespace CourseProject.Models.TeacherViewModels;

public class LectureViewModel
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Название обязательно")] public string Title { get; set; } = null!;
    public string? FilePath { get; set; }
    public ContentStatus Status { get; set; } = ContentStatus.Draft;
    public int SubjectId { get; set; }
}