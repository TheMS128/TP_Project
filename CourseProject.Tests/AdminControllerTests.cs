using CourseProject.Controllers;
using CourseProject.DataBase;
using CourseProject.DataBase.DbModels;
using CourseProject.Models.AdminViewModels.Student;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CourseProject.Tests;

[TestFixture]
public class AdminControllerTests
{
    private ApplicationDbContext _context;
    private Mock<UserManager<User>> _mockUserManager;
    private AdminController _controller;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        var store = new Mock<IUserStore<User>>();
        _mockUserManager = new Mock<UserManager<User>>(store.Object, null, null, null, null, null, null, null, null);
        _controller = new AdminController(_context, _mockUserManager.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _controller.Dispose();
    }

    [Test]
    public void Index_ReturnsViewResult()
    {
        var result = _controller.Index();
        Assert.That(result, Is.InstanceOf<ViewResult>());
    }

    [Test]
    public async Task ManageStudents_ReturnsViewWithFilteredStudents()
    {
        var studentRole = new IdentityRole { Id = "role_student", Name = "Student" };
        _context.Roles.Add(studentRole);

        var student1 = new User
            { Id = "s1", FullName = "Alice Smith", Email = "alice@test.com", UserName = "alice@test.com" };
        var student2 = new User
            { Id = "s2", FullName = "Bob Jones", Email = "bob@test.com", UserName = "bob@test.com" };
        var teacher = new User { Id = "t1", FullName = "Teacher One", Email = "teach@test.com" };

        _context.Users.AddRange(student1, student2, teacher);

        _context.UserRoles.Add(new IdentityUserRole<string> { UserId = student1.Id, RoleId = studentRole.Id });
        _context.UserRoles.Add(new IdentityUserRole<string> { UserId = student2.Id, RoleId = studentRole.Id });

        await _context.SaveChangesAsync();
        var result = await _controller.ManageStudents("Alice");
        var viewResult = result as ViewResult;
        Assert.That(viewResult, Is.Not.Null);
        Assert.That(viewResult.ViewName, Is.EqualTo("Students/ManageStudents"));

        var model = viewResult.Model as ManageStudentsViewModel;
        Assert.That(model, Is.Not.Null);
        Assert.That(model.Students.Count, Is.EqualTo(1));
        Assert.That(model.Students.First().FullName, Is.EqualTo("Alice Smith"));
    }

    [Test]
    public async Task CreateStudent_Post_ValidModel_CreatesUserAndRedirects()
    {
        var model = new CreateStudentViewModel
        {
            Email = "new@test.com",
            FullName = "New Student",
            Password = "Password123!",
            Description = "A new student"
        };

        _mockUserManager.Setup(um => um.CreateAsync(It.IsAny<User>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager.Setup(um => um.AddToRoleAsync(It.IsAny<User>(), "Student"))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _controller.CreateStudent(model);
        var redirectResult = result as RedirectToActionResult;

        Assert.That(redirectResult, Is.Not.Null);
        Assert.That(redirectResult.ActionName, Is.EqualTo(nameof(AdminController.ManageStudents)));

        _mockUserManager.Verify(um => um.CreateAsync(
            It.Is<User>(u => u.Email == model.Email && u.FullName == model.FullName),
            model.Password), Times.Once);

        _mockUserManager.Verify(um => um.AddToRoleAsync(
            It.Is<User>(u => u.Email == model.Email),
            "Student"), Times.Once);
    }

    [Test]
    public async Task CreateStudent_Post_InvalidModel_ReturnsViewWithModel()
    {
        _controller.ModelState.AddModelError("Email", "Required");
        var model = new CreateStudentViewModel();
        var result = await _controller.CreateStudent(model);
        var viewResult = result as ViewResult;

        Assert.That(viewResult, Is.Not.Null);
        Assert.That(viewResult.ViewName, Is.EqualTo("Students/CreateStudent"));
        Assert.That(viewResult.Model, Is.EqualTo(model));

        _mockUserManager.Verify(um => um.CreateAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task EditStudent_Post_ValidModel_UpdatesUserAndRedirects()
    {
        var existingStudent = new User { Id = "s1", FullName = "Old Name", Email = "old@test.com" };
        _context.Users.Add(existingStudent);
        await _context.SaveChangesAsync();

        var model = new EditStudentViewModel
        {
            Id = "s1",
            FullName = "Updated Name",
            Email = "updated@test.com",
            Description = "Updated desc"
        };

        _mockUserManager.Setup(um => um.UpdateAsync(It.IsAny<User>()))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _controller.EditStudent(model);
        var redirectResult = result as RedirectToActionResult;
        Assert.That(redirectResult, Is.Not.Null);
        Assert.That(redirectResult.ActionName, Is.EqualTo(nameof(AdminController.ManageStudents)));

        _mockUserManager.Verify(um => um.UpdateAsync(It.Is<User>(u =>
            u.Id == "s1" &&
            u.FullName == "Updated Name" &&
            u.Email == "updated@test.com"
        )), Times.Once);
    }
}