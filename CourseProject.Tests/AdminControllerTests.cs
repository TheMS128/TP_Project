using Microsoft.Data.Sqlite;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using CourseProject.Controllers;
using CourseProject.DataBase;
using CourseProject.DataBase.DbModels;
using CourseProject.DataBase.Enums;
using CourseProject.Models.AdminViewModels.Student;
using CourseProject.Models.AdminViewModels.Group;
using CourseProject.Models.AdminViewModels.Teacher;
using CourseProject.Models.AdminViewModels.Subject;

namespace CourseProject.Tests;

[TestFixture]
public class AdminControllerTests
{
    private ApplicationDbContext _context;
    private Mock<UserManager<User>> _mockUserManager;
    private AdminController _controller;
    private SqliteConnection _connection;

    [SetUp]
    public void Setup()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .EnableSensitiveDataLogging()
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        var userStoreMock = new Mock<IUserStore<User>>();
        _mockUserManager = new Mock<UserManager<User>>(
            userStoreMock.Object, null, null, null, null, null, null, null, null);

        _controller = new AdminController(_context, _mockUserManager.Object);

        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext() { HttpContext = httpContext };
        _controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
    }

    [TearDown]
    public void TearDown()
    {
        _context?.Dispose();
        _connection?.Close();
        _controller?.Dispose();
    }

    private async Task SeedRolesAsync()
    {
        if (!await _context.Roles.AnyAsync())
        {
            _context.Roles.AddRange(
                new IdentityRole { Id = "role_student", Name = "Student", NormalizedName = "STUDENT" },
                new IdentityRole { Id = "role_teacher", Name = "Teacher", NormalizedName = "TEACHER" }
            );
            await _context.SaveChangesAsync();
        }
    }

    private async Task SeedUserWithRoleAsync(string userId, string name, string roleId, int? groupId = null)
    {
        var user = new User
        {
            Id = userId,
            FullName = name,
            Email = $"{name}@test.com",
            UserName = $"{name}@test.com",
            GroupId = groupId,
            Description = "Default Description" 
        };

        _context.Users.Add(user);
        _context.UserRoles.Add(new IdentityUserRole<string> { UserId = userId, RoleId = roleId });
        await _context.SaveChangesAsync();
    }

    [Test]
    public async Task ManageStudents_ReturnsFilteredList()
    {
        await SeedRolesAsync();
        var group = new Group { Id = 1, GroupName = "IT-101" };
        _context.Groups.Add(group);
        await _context.SaveChangesAsync();

        await SeedUserWithRoleAsync("u1", "Ivan Ivanov", "role_student", 1);
        await SeedUserWithRoleAsync("u2", "Petr Petrov", "role_student", null);
        await SeedUserWithRoleAsync("t1", "Teacher One", "role_teacher"); 

        var result = await _controller.ManageStudents("Ivan");

        Assert.That(result, Is.InstanceOf<ViewResult>());
        var model = ((ViewResult)result).Model as ManageStudentsViewModel;
        Assert.That(model.Students, Has.Count.EqualTo(1));
        Assert.That(model.Students[0].FullName, Is.EqualTo("Ivan Ivanov"));
        Assert.That(model.Students[0].Group, Is.Not.Null);
    }

    [Test]
    public async Task CreateStudent_ValidModel_Redirects()
    {
        var model = new CreateStudentViewModel
        {
            Email = "new@test.com",
            Password = "Pass",
            FullName = "New Student",
            Description = "Desc"
        };

        _mockUserManager.Setup(u => u.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(u => u.AddToRoleAsync(It.IsAny<User>(), "Student"))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _controller.CreateStudent(model);

        Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
        _mockUserManager.Verify(u => u.CreateAsync(It.IsAny<User>(), "Pass"), Times.Once);
        _mockUserManager.Verify(u => u.AddToRoleAsync(It.IsAny<User>(), "Student"), Times.Once);
    }

    [Test]
    public async Task EditStudent_UpdatesData_Redirects()
    {
        await SeedRolesAsync();
        await SeedUserWithRoleAsync("u1", "Old Name", "role_student");

        var model = new EditStudentViewModel
        {
            Id = "u1",
            FullName = "New Name",
            Email = "new@test.com",
            Description = "New Desc"
        };

        _mockUserManager.Setup(u => u.UpdateAsync(It.IsAny<User>())).ReturnsAsync(IdentityResult.Success);

        var result = await _controller.EditStudent(model);

        var userInDb = await _context.Users.FindAsync("u1");
        Assert.That(userInDb.FullName, Is.EqualTo("New Name"));
        Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
    }

    [Test]
    public async Task UpdateStudentGroup_ChangesGroup()
    {
        await SeedRolesAsync();
        var group = new Group { Id = 5, GroupName = "NewGroup" };
        _context.Groups.Add(group);
        await _context.SaveChangesAsync();
        await SeedUserWithRoleAsync("u1", "Student", "role_student");

        var result = await _controller.UpdateStudentGroup("u1", 5);

        var user = await _context.Users.FindAsync("u1");
        Assert.That(user.GroupId, Is.EqualTo(5));
        Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
    }

    [Test]
    public async Task ManageGroups_ReturnsList()
    {
        _context.Groups.Add(new Group { GroupName = "G1" });
        _context.Groups.Add(new Group { GroupName = "G2" });
        await _context.SaveChangesAsync();

        var result = await _controller.ManageGroups(null);

        var model = ((ViewResult)result).Model as ManageGroupsViewModel;
        Assert.That(model.Groups, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task CreateGroup_UniqueName_Success()
    {
        var model = new CreateGroupViewModel { GroupName = "Unique Group" };

        var result = await _controller.CreateGroup(model);

        Assert.That(_context.Groups.Count(), Is.EqualTo(1));
        Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
    }

    [Test]
    public async Task CreateGroup_DuplicateName_ReturnsError()
    {
        _context.Groups.Add(new Group { GroupName = "Existing" });
        await _context.SaveChangesAsync();

        var model = new CreateGroupViewModel { GroupName = "Existing" };

        var result = await _controller.CreateGroup(model);

        Assert.That(result, Is.InstanceOf<ViewResult>());
        Assert.That(_controller.ModelState.IsValid, Is.False);
    }

    [Test]
    public async Task DeleteGroup_RemovesGroup_NullifiesStudents()
    {
        var group = new Group { Id = 1, GroupName = "DeleteMe" };
        _context.Groups.Add(group);
        await _context.SaveChangesAsync();
        await SeedRolesAsync();
        await SeedUserWithRoleAsync("s1", "Student", "role_student", 1);

        var result = await _controller.DeleteGroup(1);

        Assert.That(_context.Groups.Any(), Is.False);
        var student = await _context.Users.FindAsync("s1");
        Assert.That(student.GroupId, Is.Null);
    }

    [Test]
    public async Task ManageTeachers_ReturnsTeachers()
    {
        await SeedRolesAsync();
        await SeedUserWithRoleAsync("t1", "Teacher", "role_teacher");
        await SeedUserWithRoleAsync("s1", "Student", "role_student");

        var result = await _controller.ManageTeachers(null);

        var model = ((ViewResult)result).Model as ManageTeachersViewModel;
        Assert.That(model.Teachers, Has.Count.EqualTo(1));
        Assert.That(model.Teachers[0].Id, Is.EqualTo("t1"));
    }

    [Test]
    public async Task CreateTeacher_Valid_CallsUserManager()
    {
        _context.Subjects.Add(new Subject { Id = 10, Title = "Math", Description = "Desc" });
        await _context.SaveChangesAsync();

        var model = new CreateTeacherViewModel
        {
            Email = "teach@t.com",
            Password = "Pass",
            FullName = "Mr. Teacher",
            Description = "Desc",
            SelectedSubjectIds = new List<int> { 10 }
        };

        _mockUserManager.Setup(u => u.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _controller.CreateTeacher(model);

        _mockUserManager.Verify(u => u.AddToRoleAsync(It.IsAny<User>(), "Teacher"), Times.Once);
        Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
    }

    [Test]
    public async Task ManageSubjects_ReturnsList()
    {
        _context.Subjects.Add(new Subject { Title = "History", Description = "Desc" });
        await _context.SaveChangesAsync();

        var result = await _controller.ManageSubjects(null);

        var model = ((ViewResult)result).Model as ManageSubjectsViewModel;
        Assert.That(model.Subjects, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task CreateSubject_Valid_SavesToDb()
    {
        var model = new CreateSubjectViewModel { Title = "Biology", Description = "Bio Desc" };

        var result = await _controller.CreateSubject(model);

        Assert.That(_context.Subjects.Count(), Is.EqualTo(1));
        Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
    }

    [Test]
    public async Task EditSubject_Valid_UpdatesDb()
    {
        _context.Subjects.Add(new Subject { Id = 1, Title = "Old", Description = "Old Desc" });
        await _context.SaveChangesAsync();

        var model = new EditSubjectViewModel
        {
            Id = 1,
            Title = "New",
            Description = "New Desc",
            Status = ContentStatus.Hidden
        };

        var result = await _controller.EditSubject(model);

        var sub = await _context.Subjects.FindAsync(1);
        Assert.That(sub.Title, Is.EqualTo("New"));
        Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
    }

    [Test]
    public async Task ChangeSubjectStatus_ValidStructure_Publishes()
    {
        var subject = new Subject
        {
            Id = 1,
            Title = "Full Subject",
            Description = "Desc",
            Status = ContentStatus.Hidden,
            Lectures = new List<Lecture> { new Lecture { Title = "L1", FilePath = "path" } },
            Tests = new List<Test>
            {
                new Test
                {
                    Title = "T1",
                    Questions = new List<Question>
                    {
                        new Question { Text = "Q1", Type = "Text", Points = 1 }
                    }
                }
            }
        };
        _context.Subjects.Add(subject);
        await _context.SaveChangesAsync();

        var result = await _controller.ChangeSubjectStatus(1, ContentStatus.Published);

        var dbSub = await _context.Subjects.FindAsync(1);
        Assert.That(dbSub.Status, Is.EqualTo(ContentStatus.Published));
        Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
    }

    [Test]
    public async Task ChangeSubjectStatus_InvalidStructure_ReturnsError()
    {
        _context.Subjects.Add(new Subject
            { Id = 1, Title = "Empty", Description = "Desc", Status = ContentStatus.Hidden });
        await _context.SaveChangesAsync();

        var result = await _controller.ChangeSubjectStatus(1, ContentStatus.Published);

        var dbSub = await _context.Subjects.FindAsync(1);
        Assert.That(dbSub.Status, Is.EqualTo(ContentStatus.Hidden)); 
        Assert.That(_controller.TempData["SubjectStatusError"], Is.Not.Null); 
    }

    [Test]
    public async Task DeleteUser_Student_RedirectsToStudents()
    {
        var user = new User { Id = "u1", FullName = "S", Description = "D" };

        _mockUserManager.Setup(u => u.FindByIdAsync("u1")).ReturnsAsync(user);
        _mockUserManager.Setup(u => u.IsInRoleAsync(user, "Student")).ReturnsAsync(true);
        _mockUserManager.Setup(u => u.DeleteAsync(user)).ReturnsAsync(IdentityResult.Success);

        var result = await _controller.DeleteUser("u1");

        var redirect = (RedirectToActionResult)result;
        Assert.That(redirect.ActionName, Is.EqualTo("ManageStudents"));
    }

    [Test]
    public async Task DeleteUser_Teacher_RedirectsToTeachers()
    {
        var user = new User { Id = "t1", FullName = "T", Description = "D" };

        _mockUserManager.Setup(u => u.FindByIdAsync("t1")).ReturnsAsync(user);
        _mockUserManager.Setup(u => u.IsInRoleAsync(user, "Student")).ReturnsAsync(false);
        _mockUserManager.Setup(u => u.IsInRoleAsync(user, "Teacher")).ReturnsAsync(true);
        _mockUserManager.Setup(u => u.DeleteAsync(user)).ReturnsAsync(IdentityResult.Success);

        var result = await _controller.DeleteUser("t1");

        var redirect = (RedirectToActionResult)result;
        Assert.That(redirect.ActionName, Is.EqualTo("ManageTeachers"));
    }
}