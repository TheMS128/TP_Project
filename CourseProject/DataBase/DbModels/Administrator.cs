namespace CourseProject.DataBase.DbModels;

public class Administrator : User
{
    public int Id { get; set; }

    public void ManageSubjects() { }
    public void ManageUsers() { }
    public void ManageGroups() { }

    public int UserId { get; set; } 
}
