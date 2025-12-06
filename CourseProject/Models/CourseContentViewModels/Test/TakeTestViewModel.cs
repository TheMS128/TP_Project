namespace CourseProject.Models.CourseContentViewModels.Test;

public class TakeTestViewModel
{
    public int TestId { get; set; }
    public int TestAttemptId { get; set; } 
    public string Title { get; set; }
    public int TimeLimitMinutes { get; set; }
    public DateTime StartTime { get; set; }
    public List<QuestionTakeViewModel> Questions { get; set; } = new();
}