namespace CourseProject.Models.CourseContentViewModels.Test;

public class StudentResultViewModel
{
    public string StudentName { get; set; }
    public string GroupName { get; set; }
    public int AttemptsCount { get; set; }
    public float? BestScore { get; set; } 
    public DateTime? LastAttemptDate { get; set; }
    public bool IsPassed { get; set; } 
}