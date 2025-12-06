namespace CourseProject.Models.AdminViewModels.Group;

using DataBase.DbModels;

public class ManageGroupsViewModel
{
    public List<Group>? Groups { get; set; }
    public List<User>? AllStudents { get; set; }
}
