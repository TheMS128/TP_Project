namespace CourseProject.DataBase.DbModels;

public class Test
{
    public int Id { get; set; }
    public string Title { get; set; }
    public int? DaysToComplete { get; set; }
    public int? TimeLimitMinutes { get; set; }
    public int? MaxAttempts { get; set; }
    public bool IsPublished { get; set; }

    public virtual List<Question> Questions { get; set; }
    public int SubjectId { get; set; }
    public virtual Subject Subject { get; set; }
}