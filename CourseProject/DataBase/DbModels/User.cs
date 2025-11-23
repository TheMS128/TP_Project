using Microsoft.AspNetCore.Identity;

namespace CourseProject.DataBase.DbModels;

public class User : IdentityUser
{
    public string FullName { get; set; }
    public string Description { get; set; }
}
