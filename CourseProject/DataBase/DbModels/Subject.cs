namespace CourseProject.DataBase.DbModels;

public class Subject
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }

    public virtual List<Lecture> Lectures { get; set; }
    public virtual List<Test> Tests { get; set; }
    public virtual List<Group> EnrolledGroups { get; set; }
}
