using System.ComponentModel.DataAnnotations;

namespace CourseProject.Models.TeacherViewModels;

public class CreateLectureViewModel
{
    [Required(ErrorMessage = "Название лекции обязательно")]
    [Display(Name = "Название лекции")]
    public string Title { get; set; } = null!;
    [Required(ErrorMessage = "Необходимо выбрать файл")]
    [Display(Name = "Файл лекции (PDF, DOCX, PPTX)")] 
    public IFormFile UploadedFile { get; set; } = null!; 
    public int SubjectId { get; set; }
}