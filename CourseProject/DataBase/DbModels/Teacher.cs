namespace CourseProject.DataBase.DbModels;

public class Teacher : User
{
    public List<Subject> AssignedSubjects { get; set; }

    public void AddLecture() { }
    public void AddTest() { }
    public void AssignGroup(Group group) { }
    public void CheckResults() { }
}
