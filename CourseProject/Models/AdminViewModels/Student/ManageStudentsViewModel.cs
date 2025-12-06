namespace CourseProject.Models.AdminViewModels.Student;

using DataBase.DbModels;

public class ManageStudentsViewModel
{
    public List<User> Students { get; set; } = new();
    public List<Group> AllGroups { get; set; } = new();
}