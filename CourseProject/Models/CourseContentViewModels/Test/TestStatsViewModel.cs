namespace CourseProject.Models.CourseContentViewModels.Test;

public class TestStatsViewModel
{
    public int TestId { get; set; }
    public int SubjectId { get; set; }
    public string TestTitle { get; set; }
    public string SubjectTitle { get; set; }
    public List<StudentResultViewModel> Results { get; set; } = new();
}