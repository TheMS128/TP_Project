namespace CourseProject.DataBase.DbModels;

public class Group
{
    public int Id { get; set; }
    public string GroupName { get; set; }

    public virtual List<User>? Students { get; set; } = new(); 
    public virtual List<Subject>? Subjects { get; set; } = new();
}