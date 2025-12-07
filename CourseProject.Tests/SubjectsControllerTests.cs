using System.Security.Claims;
using CourseProject.Controllers;
using CourseProject.DataBase;
using CourseProject.DataBase.DbModels;
using CourseProject.DataBase.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CourseProject.Tests;

[TestFixture]
public class SubjectsControllerTests
{
    private ApplicationDbContext _context;
    private Mock<UserManager<User>> _mockUserManager;
    private Mock<IWebHostEnvironment> _mockEnvironment;
    private SubjectsController _controller;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        var store = new Mock<IUserStore<User>>();
        _mockUserManager = new Mock<UserManager<User>>(store.Object, null, null, null, null, null, null, null, null);

        _mockEnvironment = new Mock<IWebHostEnvironment>();
        _mockEnvironment.Setup(e => e.WebRootPath).Returns("wwwroot_test"); // Use a distinct test folder

        _controller = new SubjectsController(_context, _mockUserManager.Object, _mockEnvironment.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _controller.Dispose();

        // Clean up test files
        if (Directory.Exists("wwwroot_test"))
        {
            try
            {
                Directory.Delete("wwwroot_test", true);
            }
            catch
            {
            }
        }
    }

    private void MockUser(string userId, string role = "Student")
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, role)
        }, "mock"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
        _mockUserManager.Setup(u => u.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns(userId);
        _mockUserManager.Setup(u => u.IsInRoleAsync(It.IsAny<User>(), role)).ReturnsAsync(true);
    }

    [Test]
    public async Task Index_Student_ReturnsOnlyEnrolledAndPublishedSubjects()
    {
        // Arrange
        var studentId = "student1";
        var group = new Group { Id = 1, Students = new List<User> { new User { Id = studentId } } };

        var subjectPublished = new Subject
            { Id = 1, Title = "Pub", Status = ContentStatus.Published, EnrolledGroups = new List<Group> { group } };
        var subjectDraft = new Subject
            { Id = 2, Title = "Draft", Status = ContentStatus.Hidden, EnrolledGroups = new List<Group> { group } };
        var subjectNotEnrolled = new Subject { Id = 3, Title = "Other", Status = ContentStatus.Published };

        _context.Groups.Add(group);
        _context.Subjects.AddRange(subjectPublished, subjectDraft, subjectNotEnrolled);
        await _context.SaveChangesAsync();
        MockUser(studentId, "Student");

        var result = await _controller.Index();

        var viewResult = result as ViewResult;
        Assert.That(viewResult, Is.Not.Null);
        var model = viewResult.Model as List<Subject>;
        Assert.That(model, Is.Not.Null);
        Assert.That(model.Count, Is.EqualTo(1));
        Assert.That(model[0].Title, Is.EqualTo("Pub"));
    }

    [Test]
    public async Task Details_StudentNotEnrolled_ReturnsForbid()
    {
        var subject = new Subject { Id = 1, Status = ContentStatus.Published };
        _context.Subjects.Add(subject);
        await _context.SaveChangesAsync();

        MockUser("student1", "Student");

        var result = await _controller.Details(1);
        Assert.That(result, Is.InstanceOf<ForbidResult>());
    }

    [Test]
    public async Task DownloadLecture_Student_Published_ReturnsFile()
    {
        var studentId = "s1";
        var group = new Group { Id = 1, Students = new List<User> { new User { Id = studentId } } };
        var subject = new Subject { Id = 1, EnrolledGroups = new List<Group> { group } };

        var lecture = new Lecture
        {
            Id = 10,
            SubjectId = 1,
            FilePath = "/files/test.pdf",
            Status = ContentStatus.Published
        };

        _context.Groups.Add(group);
        _context.Subjects.Add(subject);
        _context.Lectures.Add(lecture);
        await _context.SaveChangesAsync();

        MockUser(studentId, "Student");

        var path = Path.Combine("wwwroot_test", "files");
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "test.pdf"), "dummy content");

        var result = await _controller.DownloadLecture(10);
        Assert.That(result, Is.InstanceOf<FileContentResult>());
    }
}