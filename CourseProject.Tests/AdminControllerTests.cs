using System.Security.Claims;
using CourseProject.Controllers;
using CourseProject.DataBase;
using CourseProject.DataBase.DbModels;
using CourseProject.DataBase.Enums;
using CourseProject.Models.AdminViewModels.Group;
using CourseProject.Models.AdminViewModels.Student;
using CourseProject.Models.AdminViewModels.Subject;
using CourseProject.Models.AdminViewModels.Teacher;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CourseProject.Tests;

[TestFixture]
public class AdminControllerTests
{
    private Mock<UserManager<User>> _mockUserManager;
    private Mock<ApplicationDbContext> _mockContext;
    private AdminController _controller;

    [SetUp]
    public void Setup()
    {
        var store = new Mock<IUserStore<User>>();
        _mockUserManager = new Mock<UserManager<User>>(store.Object, null, null, null, null, null, null, null, null);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>().Options;
        _mockContext = new Mock<ApplicationDbContext>(options);

        var roles = new List<IdentityRole>
        {
            new IdentityRole { Name = "Student", Id = "role_student_id" },
            new IdentityRole { Name = "Teacher", Id = "role_teacher_id" },
            new IdentityRole { Name = "Admin", Id = "role_admin_id" }
        };
        _mockContext.Setup(c => c.Roles).Returns(DbSetMock.Create(roles).Object);

        var userRoles = new List<IdentityUserRole<string>>
        {
            new IdentityUserRole<string> { UserId = "1", RoleId = "role_student_id" }
        };
        var userRolesMock = DbSetMock.Create(userRoles);
        _mockContext.Setup(c => c.Set<IdentityUserRole<string>>()).Returns(userRolesMock.Object);

        _mockContext.Setup(c => c.Groups).Returns(DbSetMock.Create(new List<Group>()).Object);
        _mockContext.Setup(c => c.Users).Returns(DbSetMock.Create(new List<User>()).Object);
        _mockContext.Setup(c => c.Subjects).Returns(DbSetMock.Create(new List<Subject>()).Object);

        _controller = new AdminController(_mockContext.Object, _mockUserManager.Object);
        _controller.TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());

        SetupAdminUser();
    }

    [TearDown]
    public void TearDown()
    {
        _controller?.Dispose();
    }

    [Test]
    public void Index_ReturnsView()
    {
        var result = _controller.Index();
        Assert.That(result, Is.InstanceOf<ViewResult>());
    }

    [Test]
    public async Task ManageStudents_ReturnsFilteredList()
    {
        var s1 = new User { Id = "1", FullName = "Ivanov", Email = "i@test.com", UserName = "i@test.com" };
        var allUsers = new List<User> { s1 };

        var usersMock = DbSetMock.Create(allUsers);
        _mockContext.Setup(c => c.Users).Returns(usersMock.Object);

        var result = await _controller.ManageStudents("Ivan") as ViewResult;

        Assert.That(result, Is.Not.Null);
        var model = result.Model as ManageStudentsViewModel;
        Assert.That(model, Is.Not.Null);
        Assert.That(model.Students.Count, Is.EqualTo(1));
        Assert.That(model.Students[0].FullName, Is.EqualTo("Ivanov"));
    }

    [Test]
    public async Task CreateStudent_Get_ReturnsView()
    {
        var result = await _controller.CreateStudent();
        Assert.That(result, Is.InstanceOf<ViewResult>());
    }

    [Test]
    public async Task CreateStudent_ValidModel_SavesUserAndRedirects()
    {
        var model = new CreateStudentViewModel { Email = "new@t.com", FullName = "New", Password = "Pass" };

        _mockUserManager.Setup(u => u.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(u => u.AddToRoleAsync(It.IsAny<User>(), "Student"))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _controller.CreateStudent(model);

        Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
    }

    [Test]
    public async Task CreateStudent_InvalidModel_ReturnsViewWithModel()
    {
        _controller.ModelState.AddModelError("Error", "Error");
        var model = new CreateStudentViewModel();

        var result = await _controller.CreateStudent(model) as ViewResult;

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Model, Is.EqualTo(model));
    }

    [Test]
    public async Task EditStudent_Get_UserFound_ReturnsView()
    {
        var user = new User { Id = "u1" };
        var usersList = new List<User> { user };
        var usersMock = DbSetMock.Create(usersList);

        usersMock.Setup(m => m.FindAsync(It.IsAny<object[]>()))
            .ReturnsAsync(user);

        _mockContext.Setup(c => c.Users).Returns(usersMock.Object);

        var result = await _controller.EditStudent("u1") as ViewResult;

        Assert.That(result, Is.Not.Null, "Result is null (likely returned NotFound because FindAsync failed)");
        var model = result.Model as EditStudentViewModel;
        Assert.That(model.Id, Is.EqualTo("u1"));
    }

    [Test]
    public async Task EditStudent_Get_UserNotFound_ReturnsNotFound()
    {
        var result = await _controller.EditStudent("unknown");
        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }

    [Test]
    public async Task EditStudent_Post_UpdatesData_Redirects()
    {
        var user = new User { Id = "u1", FullName = "Old", Email = "old@t.com", UserName = "old@t.com" };
        var model = new EditStudentViewModel { Id = "u1", Email = "n@t.com", FullName = "New" };

        var usersMock = DbSetMock.Create(new List<User> { user });
        usersMock.Setup(m => m.FindAsync(It.IsAny<object[]>())).ReturnsAsync(user);
        _mockContext.Setup(c => c.Users).Returns(usersMock.Object);

        _mockUserManager.Setup(u => u.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var result = await _controller.EditStudent(model);

        _mockUserManager.Verify(u => u.UpdateAsync(It.Is<User>(x => x.FullName == "New")), Times.Once);
        Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
    }

    [Test]
    public async Task UpdateStudentGroup_ChangesGroup_Redirects()
    {
        var user = new User { Id = "u1", GroupId = 1 };
        var usersMock = DbSetMock.Create(new List<User> { user });
        usersMock.Setup(m => m.FindAsync(It.IsAny<object[]>())).ReturnsAsync(user);
        _mockContext.Setup(c => c.Users).Returns(usersMock.Object);

        var result = await _controller.UpdateStudentGroup("u1", 2);

        Assert.That(user.GroupId, Is.EqualTo(2));
        _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Once);
    }

    [Test]
    public async Task ManageGroups_Search_ReturnsFiltered()
    {
        var g1 = new Group { GroupName = "Alpha" };
        var g2 = new Group { GroupName = "Beta" };
        _mockContext.Setup(c => c.Groups).Returns(DbSetMock.Create(new List<Group> { g1, g2 }).Object);

        var result = await _controller.ManageGroups("Alp") as ViewResult;

        var model = result.Model as ManageGroupsViewModel;
        Assert.That(model.Groups.Count, Is.EqualTo(1));
        Assert.That(model.Groups[0].GroupName, Is.EqualTo("Alpha"));
    }

    [Test]
    public async Task CreateGroup_Get_ReturnsView()
    {
        var result = await _controller.CreateGroup();
        Assert.That(result, Is.InstanceOf<ViewResult>());
    }

    [Test]
    public async Task CreateGroup_UniqueName_Success()
    {
        var model = new CreateGroupViewModel { GroupName = "NewGroup" };
        var groupsMock = DbSetMock.Create(new List<Group>());
        _mockContext.Setup(c => c.Groups).Returns(groupsMock.Object);

        var result = await _controller.CreateGroup(model);

        groupsMock.Verify(m => m.Add(It.IsAny<Group>()), Times.Once);
        Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
    }

    [Test]
    public async Task CreateGroup_DuplicateName_ReturnsError()
    {
        var existing = new Group { GroupName = "G1" };
        _mockContext.Setup(c => c.Groups).Returns(DbSetMock.Create(new List<Group> { existing }).Object);

        var result = await _controller.CreateGroup(new CreateGroupViewModel { GroupName = "G1" }) as ViewResult;

        Assert.That(_controller.ModelState.IsValid, Is.False);
    }

    [Test]
    public async Task EditGroup_Get_Found_ReturnsView()
    {
        var group = new Group { Id = 1, GroupName = "G1" };
        _mockContext.Setup(c => c.Groups).Returns(DbSetMock.Create(new List<Group> { group }).Object);

        var result = await _controller.EditGroup(1) as ViewResult;

        Assert.That(result.Model, Is.InstanceOf<EditGroupViewModel>());
    }

    [Test]
    public async Task EditGroup_Get_NotFound_ReturnsNotFound()
    {
        var result = await _controller.EditGroup(999);
        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }

    [Test]
    public async Task EditGroup_Post_Success_UpdatesNameAndStudents()
    {
        var s1 = new User { Id = "s1", GroupId = 1 };
        var s2 = new User { Id = "s2", GroupId = 1 };
        var group = new Group { Id = 1, GroupName = "Old", Students = new List<User> { s1, s2 } };

        _mockContext.Setup(c => c.Groups).Returns(DbSetMock.Create(new List<Group> { group }).Object);
        _mockContext.Setup(c => c.Users).Returns(DbSetMock.Create(new List<User> { s1, s2 }).Object);

        var model = new EditGroupViewModel
        {
            Id = 1,
            GroupName = "New",
            SelectedStudentIds = new List<string> { "s1" }
        };

        var result = await _controller.EditGroup(model);

        Assert.That(group.GroupName, Is.EqualTo("New"));
        Assert.That(s2.GroupId, Is.Null, "s2 should be removed from group");
        Assert.That(s1.GroupId, Is.EqualTo(1));
        _mockContext.Verify(c => c.SaveChangesAsync(default), Times.Once);
        Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
    }

    [Test]
    public async Task UpdateGroupStudents_UpdatesRelations()
    {
        var s1 = new User { Id = "s1", GroupId = 1 };
        var sNew = new User { Id = "sNew", GroupId = null };

        var users = new List<User> { s1, sNew };
        _mockContext.Setup(c => c.Users).Returns(DbSetMock.Create(users).Object);

        var result = await _controller.UpdateGroupStudents(1, new List<string> { "sNew" });

        Assert.That(s1.GroupId, Is.Null);
        Assert.That(sNew.GroupId, Is.EqualTo(1));
    }

    [Test]
    public async Task DeleteGroup_CallsRemove()
    {
        var group = new Group { Id = 10, Students = new List<User> { new User { GroupId = 10 } } };
        var groupsMock = DbSetMock.Create(new List<Group> { group });
        groupsMock.Setup(m => m.FindAsync(It.IsAny<object[]>())).ReturnsAsync(group);
        _mockContext.Setup(c => c.Groups).Returns(groupsMock.Object);

        await _controller.DeleteGroup(10);

        groupsMock.Verify(m => m.Remove(group), Times.Once);
        Assert.That(group.Students[0].GroupId, Is.Null);
    }

    [Test]
    public async Task ManageTeachers_Search_ReturnsFiltered()
    {
        var t1 = new User { Id = "t1", FullName = "Teacher1", Email = "t@t.com" };
        var roles = new List<IdentityUserRole<string>>
        {
            new IdentityUserRole<string> { UserId = "t1", RoleId = "role_teacher_id" }
        };
        _mockContext.Setup(c => c.Set<IdentityUserRole<string>>()).Returns(DbSetMock.Create(roles).Object);
        _mockContext.Setup(c => c.Users).Returns(DbSetMock.Create(new List<User> { t1 }).Object);

        var result = await _controller.ManageTeachers("Teach") as ViewResult;

        var model = result.Model as ManageTeachersViewModel;
        Assert.That(model.Teachers.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task CreateTeacher_Get_ReturnsView()
    {
        var result = await _controller.CreateTeacher();
        Assert.That(result, Is.InstanceOf<ViewResult>());
    }

    [Test]
    public async Task CreateTeacher_Valid_CallsUserManager()
    {
        var model = new CreateTeacherViewModel
            { Email = "t@t.com", Password = "123", SelectedSubjectIds = new List<int> { 1 } };
        var subj = new Subject { Id = 1 };
        _mockContext.Setup(c => c.Subjects).Returns(DbSetMock.Create(new List<Subject> { subj }).Object);

        _mockUserManager.Setup(u => u.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(u => u.AddToRoleAsync(It.IsAny<User>(), "Teacher")).ReturnsAsync(IdentityResult.Success);

        var result = await _controller.CreateTeacher(model);

        Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
    }

    [Test]
    public async Task EditTeacher_Get_Found_ReturnsView()
    {
        var teacher = new User { Id = "t1" };
        _mockContext.Setup(c => c.Users).Returns(DbSetMock.Create(new List<User> { teacher }).Object);

        var result = await _controller.EditTeacher("t1") as ViewResult;
        Assert.That(result.Model, Is.InstanceOf<EditTeacherViewModel>());
    }

    [Test]
    public async Task EditTeacher_Post_Success_UpdatesSubjects()
    {
        var teacher = new User { Id = "t1", AssignedSubjects = new List<Subject>() };
        var subj = new Subject { Id = 10 };

        _mockContext.Setup(c => c.Users).Returns(DbSetMock.Create(new List<User> { teacher }).Object);
        _mockContext.Setup(c => c.Subjects).Returns(DbSetMock.Create(new List<Subject> { subj }).Object);
        _mockUserManager.Setup(u => u.UpdateAsync(teacher)).ReturnsAsync(IdentityResult.Success);

        var model = new EditTeacherViewModel { Id = "t1", SelectedSubjectIds = new List<int> { 10 } };

        var result = await _controller.EditTeacher(model);

        Assert.That(teacher.AssignedSubjects.Count, Is.EqualTo(1));
        Assert.That(teacher.AssignedSubjects[0].Id, Is.EqualTo(10));
    }

    [Test]
    public async Task UpdateTeacherSubjects_UpdatesList()
    {
        var teacher = new User { Id = "t1", AssignedSubjects = new List<Subject>() };
        var subj = new Subject { Id = 5 };

        _mockContext.Setup(c => c.Users).Returns(DbSetMock.Create(new List<User> { teacher }).Object);
        _mockContext.Setup(c => c.Subjects).Returns(DbSetMock.Create(new List<Subject> { subj }).Object);

        await _controller.UpdateTeacherSubjects("t1", new List<int> { 5 });

        Assert.That(teacher.AssignedSubjects.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task ManageSubjects_Search_ReturnsFiltered()
    {
        var s1 = new Subject { Title = "Math" };
        var s2 = new Subject { Title = "Physics" };
        _mockContext.Setup(c => c.Subjects).Returns(DbSetMock.Create(new List<Subject> { s1, s2 }).Object);

        var result = await _controller.ManageSubjects("Math") as ViewResult;
        var model = result.Model as ManageSubjectsViewModel;

        Assert.That(model.Subjects.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task CreateSubject_Get_ReturnsView()
    {
        var result = await _controller.CreateSubject();
        Assert.That(result, Is.InstanceOf<ViewResult>());
    }

    [Test]
    public async Task CreateSubject_Valid_SavesToDb()
    {
        var model = new CreateSubjectViewModel
            { Title = "NewSubj", SelectedTeacherIds = new List<string>(), SelectedGroupIds = new List<int>() };
        var subjectsMock = DbSetMock.Create(new List<Subject>());
        _mockContext.Setup(c => c.Subjects).Returns(subjectsMock.Object);

        await _controller.CreateSubject(model);

        subjectsMock.Verify(m => m.Add(It.IsAny<Subject>()), Times.Once);
    }

    [Test]
    public async Task EditSubject_Get_Found_ReturnsView()
    {
        var subject = new Subject { Id = 1, Teachers = new List<User>(), EnrolledGroups = new List<Group>() };
        _mockContext.Setup(c => c.Subjects).Returns(DbSetMock.Create(new List<Subject> { subject }).Object);

        var result = await _controller.EditSubject(1) as ViewResult;
        Assert.That(result.Model, Is.InstanceOf<EditSubjectViewModel>());
    }

    [Test]
    public async Task EditSubject_Post_ValidationFail_ReturnsView()
    {
        var subject = new Subject
            { Id = 1, Status = ContentStatus.Hidden, Lectures = new List<Lecture>(), Tests = new List<Test>() };
        _mockContext.Setup(c => c.Subjects).Returns(DbSetMock.Create(new List<Subject> { subject }).Object);

        var model = new EditSubjectViewModel { Id = 1, Status = ContentStatus.Published }; 

        var result = await _controller.EditSubject(model) as ViewResult;

        Assert.That(_controller.ModelState.IsValid, Is.False);
        Assert.That(_controller.ModelState["Status"], Is.Not.Null);
    }

    [Test]
    public async Task EditSubject_Post_Success_Updates()
    {
        var subject = new Subject { Id = 1, Title = "Old" };
        _mockContext.Setup(c => c.Subjects).Returns(DbSetMock.Create(new List<Subject> { subject }).Object);

        var model = new EditSubjectViewModel { Id = 1, Title = "New", Status = ContentStatus.Hidden };

        var result = await _controller.EditSubject(model);

        Assert.That(subject.Title, Is.EqualTo("New"));
        Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
    }

    [Test]
    public async Task ChangeSubjectStatus_InvalidStructure_ReturnsError()
    {
        var s = new Subject
        {
            Id = 1,
            Status = ContentStatus.Hidden,
            Lectures = new List<Lecture>(),
            Tests = new List<Test>()
        };
        _mockContext.Setup(c => c.Subjects).Returns(DbSetMock.Create(new List<Subject> { s }).Object);

        await _controller.ChangeSubjectStatus(1, ContentStatus.Published);

        Assert.That(s.Status, Is.EqualTo(ContentStatus.Hidden));
        Assert.That(_controller.TempData["SubjectStatusError"], Is.Not.Null);
    }

    [Test]
    public async Task UpdateSubjectTeachers_UpdatesList()
    {
        var subject = new Subject { Id = 1, Teachers = new List<User>() };
        var t1 = new User { Id = "t1" };

        _mockContext.Setup(c => c.Subjects).Returns(DbSetMock.Create(new List<Subject> { subject }).Object);
        _mockContext.Setup(c => c.Users).Returns(DbSetMock.Create(new List<User> { t1 }).Object);

        await _controller.UpdateSubjectTeachers(1, new List<string> { "t1" });

        Assert.That(subject.Teachers.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task UpdateSubjectGroups_UpdatesList()
    {
        var subject = new Subject { Id = 1, EnrolledGroups = new List<Group>() };
        var g1 = new Group { Id = 10 };

        _mockContext.Setup(c => c.Subjects).Returns(DbSetMock.Create(new List<Subject> { subject }).Object);
        _mockContext.Setup(c => c.Groups).Returns(DbSetMock.Create(new List<Group> { g1 }).Object);

        await _controller.UpdateSubjectGroups(1, new List<int> { 10 });

        Assert.That(subject.EnrolledGroups.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task DeleteSubject_CallsRemove()
    {
        var subject = new Subject { Id = 1 };
        var mockSet = DbSetMock.Create(new List<Subject> { subject });
        mockSet.Setup(m => m.FindAsync(It.IsAny<object[]>())).ReturnsAsync(subject);
        _mockContext.Setup(c => c.Subjects).Returns(mockSet.Object);

        await _controller.DeleteSubject(1);

        mockSet.Verify(m => m.Remove(subject), Times.Once);
    }

    [Test]
    public async Task DeleteUser_Student_RedirectsToManageStudents()
    {
        var user = new User { Id = "u1" };
        _mockUserManager.Setup(u => u.FindByIdAsync("u1")).ReturnsAsync(user);
        _mockUserManager.Setup(u => u.IsInRoleAsync(user, "Student")).ReturnsAsync(true);
        _mockUserManager.Setup(u => u.DeleteAsync(user)).ReturnsAsync(IdentityResult.Success);

        var result = await _controller.DeleteUser("u1") as RedirectToActionResult;

        Assert.That(result.ActionName, Is.EqualTo(nameof(AdminController.ManageStudents)));
    }

    [Test]
    public async Task DeleteUser_Teacher_RedirectsToManageTeachers()
    {
        var user = new User { Id = "t1" };
        _mockUserManager.Setup(u => u.FindByIdAsync("t1")).ReturnsAsync(user);
        _mockUserManager.Setup(u => u.IsInRoleAsync(user, "Student")).ReturnsAsync(false);
        _mockUserManager.Setup(u => u.IsInRoleAsync(user, "Teacher")).ReturnsAsync(true);
        _mockUserManager.Setup(u => u.DeleteAsync(user)).ReturnsAsync(IdentityResult.Success);

        var result = await _controller.DeleteUser("t1") as RedirectToActionResult;

        Assert.That(result.ActionName, Is.EqualTo(nameof(AdminController.ManageTeachers)));
    }

    private void SetupAdminUser()
    {
        var claims = new List<Claim> { new Claim(ClaimTypes.Role, "Admin") };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }
}