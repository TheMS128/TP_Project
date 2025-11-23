namespace CourseProject.DataBase.DbModels;

public class Group
{
    public int Id { get; set; }
    public string GroupName { get; set; }
    public virtual List<Student> Students { get; set; }
}
