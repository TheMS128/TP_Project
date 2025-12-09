using Microsoft.Data.Sqlite;
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
    private SubjectsController _controller;
    private SqliteConnection _connection;
    private Mock<UserManager<User>> _mockUserManager;
    private Mock<IWebHostEnvironment> _mockEnvironment;

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

        _mockEnvironment = new Mock<IWebHostEnvironment>();
        _mockEnvironment.Setup(m => m.WebRootPath).Returns("TestWebRootPath");
        _controller = new SubjectsController(_context, _mockUserManager.Object, _mockEnvironment.Object);

        SetUserRole("Student", "s1");
    }

    [TearDown]
    public void TearDown()
    {
        _context?.Dispose();
        _connection?.Close();
        _controller?.Dispose();
    }

    private void SetUserRole(string role, string userId)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "mock");
        var principal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        _mockUserManager.Setup(u => u.GetUserId(principal)).Returns(userId);
    }

    private async Task SeedDataAsync()
    {
        var group = new Group { Id = 1, GroupName = "IT-101" };

        var student = new User
        {
            Id = "s1",
            FullName = "Student One",
            UserName = "s1",
            GroupId = 1,
            Description = "Desc"
        };

        var teacher = new User
        {
            Id = "t1",
            FullName = "Teacher One",
            UserName = "t1",
            Description = "Desc"
        };

        var subject = new Subject
        {
            Id = 1,
            Title = "Math",
            Description = "Math Desc",
            Status = ContentStatus.Published,
            EnrolledGroups = new List<Group>(),
            Teachers = new List<User>()
        };

        _context.Groups.Add(group);
        _context.Users.AddRange(student, teacher);
        _context.Subjects.Add(subject);
        await _context.SaveChangesAsync();
    }

    [Test]
    public async Task Index_Student_ReturnsOnlyEnrolledAndPublishedSubjects()
    {
        await SeedDataAsync();
        var group = await _context.Groups.FindAsync(1);
        var subject = await _context.Subjects.FindAsync(1);

        subject.EnrolledGroups.Add(group);
        await _context.SaveChangesAsync();

        _context.Subjects.Add(new Subject
        {
            Id = 2, Title = "Hidden", Description = "D", Status = ContentStatus.Hidden,
            EnrolledGroups = new List<Group> { group }
        });
        await _context.SaveChangesAsync();
        SetUserRole("Student", "s1");
        var result = await _controller.Index();

        Assert.That(result, Is.InstanceOf<ViewResult>());
        var model = ((ViewResult)result).Model as List<Subject>;
        Assert.That(model, Has.Count.EqualTo(1));
        Assert.That(model[0].Title, Is.EqualTo("Math"));
    }

    [Test]
    public async Task Index_Teacher_ReturnsOnlyAssignedSubjects()
    {
        await SeedDataAsync();
        var subject = await _context.Subjects.FindAsync(1);
        var teacher = await _context.Users.FindAsync("t1");

        subject.Teachers.Add(teacher); 
        await _context.SaveChangesAsync();

        SetUserRole("Teacher", "t1");
        var result = await _controller.Index();

        Assert.That(result, Is.InstanceOf<ViewResult>());
        var model = ((ViewResult)result).Model as List<Subject>;
        Assert.That(model, Has.Count.EqualTo(1));
        Assert.That(model[0].Title, Is.EqualTo("Math"));
    }

    [Test]
    public async Task Index_Admin_ReturnsAllSubjects()
    {
        await SeedDataAsync();
        _context.Subjects.Add(
            new Subject { Id = 2, Title = "Hidden", Description = "D", Status = ContentStatus.Hidden });
        await _context.SaveChangesAsync();
        SetUserRole("Admin", "admin1");
        var result = await _controller.Index();

        var model = ((ViewResult)result).Model as List<Subject>;
        Assert.That(model, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Details_StudentEnrolled_ReturnsView()
    {
        await SeedDataAsync();
        var group = await _context.Groups.FindAsync(1);
        var subject = await _context.Subjects.FindAsync(1);
        subject.EnrolledGroups.Add(group);
        await _context.SaveChangesAsync();
        SetUserRole("Student", "s1");
        var result = await _controller.Details(1);

        Assert.That(result, Is.InstanceOf<ViewResult>());
        var model = ((ViewResult)result).Model as Subject;
        Assert.That(model.Id, Is.EqualTo(1));
    }

    [Test]
    public async Task Details_StudentNotEnrolled_ReturnsForbid()
    {
        await SeedDataAsync();
        SetUserRole("Student", "s1");
        var result = await _controller.Details(1);

        Assert.That(result, Is.InstanceOf<ForbidResult>());
    }

    [Test]
    public async Task DownloadLecture_Student_Published_ReturnsFile()
    {
        await SeedDataAsync();
        var group = await _context.Groups.FindAsync(1);
        var subject = await _context.Subjects.FindAsync(1);
        subject.EnrolledGroups.Add(group);

        var lecture = new Lecture
        {
            Id = 1,
            Title = "L1",
            FilePath = "lectures/test.pdf",
            Status = ContentStatus.Published,
            SubjectId = 1
        };
        _context.Lectures.Add(lecture);
        await _context.SaveChangesAsync();
        SetUserRole("Student", "s1");
        var result = await _controller.DownloadLecture(1);

        Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        var notFound = (NotFoundObjectResult)result;
        Assert.That(notFound.Value, Is.EqualTo("Файл не найден на сервере."));
    }

    [Test]
    public async Task DownloadLecture_Student_Hidden_ReturnsForbid()
    {
        await SeedDataAsync();
        var group = await _context.Groups.FindAsync(1);
        var subject = await _context.Subjects.FindAsync(1);
        subject.EnrolledGroups.Add(group);

        var lecture = new Lecture
        {
            Id = 1,
            Title = "L1",
            FilePath = "test.pdf",
            Status = ContentStatus.Hidden, 
            SubjectId = 1
        };
        _context.Lectures.Add(lecture);
        await _context.SaveChangesAsync();

        SetUserRole("Student", "s1");

        var result = await _controller.DownloadLecture(1);

        Assert.That(result, Is.InstanceOf<ForbidResult>());
    }

    [Test]
    public async Task StartTest_StudentEnrolled_ReturnsView()
    {
        await SeedDataAsync();
        var group = await _context.Groups.FindAsync(1);
        var subject = await _context.Subjects.FindAsync(1);
        subject.EnrolledGroups.Add(group);

        var test = new Test
        {
            Id = 1, Title = "T1", Status = ContentStatus.Published, SubjectId = 1,
            Questions = new List<Question>()
        };
        _context.Tests.Add(test);
        await _context.SaveChangesAsync();

        SetUserRole("Student", "s1");

        var result = await _controller.StartTest(1);

        Assert.That(result, Is.InstanceOf<ViewResult>());
        var model = ((ViewResult)result).Model as Test;
        Assert.That(model.Id, Is.EqualTo(1));
    }
}