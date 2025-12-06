using Microsoft.AspNetCore.Identity;

namespace CourseProject.DataBase.DbModels;

public class User : IdentityUser
{
    public string FullName { get; set; }
    public string Description { get; set; }
    public int? GroupId { get; set; }
    public virtual Group? Group { get; set; }

    public virtual List<Subject>? AssignedSubjects { get; set; }
    public virtual List<TestAttempt>? TestAttempts { get; set; }
}