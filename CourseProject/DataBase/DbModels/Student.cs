namespace CourseProject.DataBase.DbModels;

public class Student : User
{
    public void ViewSubject() { }
    public void DownloadLecture() { }
    public void TakeTest() { }

    public virtual Group Group { get; set; }

    public virtual List<TestAttempt> TestAttempts { get; set; }
}