namespace CourseProject.DataBase.DbModels;

public class TestAttempt
{
    public int Id { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public float Score { get; set; }

    public virtual List<StudentAnswer> StudentAnswers { get; set; }

    public string StudentId { get; set; }
    public virtual Student Student { get; set; }
}