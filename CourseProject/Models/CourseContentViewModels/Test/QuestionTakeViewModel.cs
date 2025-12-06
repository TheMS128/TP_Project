namespace CourseProject.Models.CourseContentViewModels.Test;

public class QuestionTakeViewModel
{
    public int QuestionId { get; set; }
    public string Text { get; set; }
    public string Type { get; set; } 
    public int Points { get; set; }
    public List<AnswerOptionTakeViewModel> Options { get; set; } = new();
}