using System.ComponentModel.DataAnnotations;
using CourseProject.DataBase.Enums;

namespace CourseProject.Models.CourseContentViewModels.Lecture;

public class LectureViewModel
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    [Required] public string Title { get; set; } = null!;
    public IFormFile? UploadedFile { get; set; } 
    public string? ExistingFilePath { get; set; }
    public string? OriginalFileName { get; set; } 
    public ContentStatus Status { get; set; }
}