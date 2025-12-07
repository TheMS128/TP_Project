using CourseProject.Controllers;
using CourseProject.DataBase;
using CourseProject.DataBase.DbModels;
using CourseProject.DataBase.Enums;
using CourseProject.Models.CourseContentViewModels.Lecture;
using CourseProject.Models.SubjectModels;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CourseProject.Tests;

[TestFixture]
public class CourseContentControllerTests
{
    private ApplicationDbContext _context;
    private Mock<UserManager<User>> _mockUserManager;
    private Mock<IWebHostEnvironment> _mockEnvironment;
    private CourseContentController _controller;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        var store = new Mock<IUserStore<User>>();
        _mockUserManager =
            new Mock<UserManager<User>>(store.Object, null, null, null, null, null, null, null, null);
        _mockEnvironment = new Mock<IWebHostEnvironment>();
        _mockEnvironment.Setup(e => e.WebRootPath).Returns("wwwroot");

        _controller = new CourseContentController(_context, _mockUserManager.Object, _mockEnvironment.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _controller.Dispose();
    }

    private void MockUser(string userId, string role = "Teacher")
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
        _mockUserManager.Setup(u => u.IsInRoleAsync(It.IsAny<User>(), "Admin")).ReturnsAsync(role == "Admin");
    }

    [Test]
    public async Task Index_TeacherWithAccess_ReturnsViewWithModel()
    {
        var teacherId = "teacher1";
        var subject = new Subject
            { Id = 1, Title = "Math", Teachers = new List<User> { new User { Id = teacherId } } };
        _context.Subjects.Add(subject);
        await _context.SaveChangesAsync();

        MockUser(teacherId);

        var result = await _controller.Index(1);
        var viewResult = result as ViewResult;
        Assert.That(viewResult, Is.Not.Null);

        var model = viewResult.Model as CourseContentModel;
        Assert.That(model, Is.Not.Null);
        Assert.That(model.Title, Is.EqualTo("Math"));
    }

    [Test]
    public async Task Index_TeacherNoAccess_ReturnsForbid()
    {
        var subject = new Subject { Id = 1, Title = "Math", Teachers = new List<User>() };
        _context.Subjects.Add(subject);
        await _context.SaveChangesAsync();

        MockUser("other_teacher");

        var result = await _controller.Index(1);
        Assert.That(result, Is.InstanceOf<ForbidResult>());
    }

    [Test]
    public async Task ConfigureGroups_Get_ReturnsCorrectModel()
    {
        var teacherId = "t1";
        var group1 = new Group { Id = 10, GroupName = "Group A" };
        var subject = new Subject
        {
            Id = 1,
            Teachers = new List<User> { new User { Id = teacherId } },
            EnrolledGroups = new List<Group> { group1 }
        };
        _context.Groups.Add(group1);
        _context.Subjects.Add(subject);
        await _context.SaveChangesAsync();

        MockUser(teacherId);

        var result = await _controller.ConfigureGroups(1);
        var viewResult = result as ViewResult;
        Assert.That(viewResult, Is.Not.Null);

        var model = viewResult.Model as ConfigureGroupsModel;
        Assert.That(model, Is.Not.Null);
        Assert.That(model.SelectedGroupIds.Count, Is.EqualTo(1));
        Assert.That(model.SelectedGroupIds.First(), Is.EqualTo(10));
    }

    [Test]
    public async Task CreateLecture_Post_ValidModel_SavesLecture()
    {
        var teacherId = "t1";
        var subject = new Subject { Id = 1, Teachers = new List<User> { new User { Id = teacherId } } };
        _context.Subjects.Add(subject);
        await _context.SaveChangesAsync();

        MockUser(teacherId);

        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns("test.pdf");
        fileMock.Setup(f => f.Length).Returns(100);

        var model = new LectureViewModel
        {
            SubjectId = 1,
            Title = "New Lecture",
            UploadedFile = fileMock.Object,
            Status = ContentStatus.Published
        };

        var result = await _controller.CreateLecture(model);
        var lecture = await _context.Lectures.FirstOrDefaultAsync();

        Assert.That(lecture, Is.Not.Null);
        Assert.That(lecture.Title, Is.EqualTo("New Lecture"));
        Assert.That(lecture.Status, Is.EqualTo(ContentStatus.Published));

        var redirect = result as RedirectToActionResult;
        Assert.That(redirect, Is.Not.Null);
        Assert.That(redirect.ActionName, Is.EqualTo("ManageLectures"));
    }

    [Test]
    public async Task ChangeSubjectStatus_DraftWithoutQuestions_ShowsError()
    {
        var teacherId = "t1";
        var subject = new Subject
        {
            Id = 1,
            Status = ContentStatus.Draft,
            Teachers = new List<User> { new User { Id = teacherId } }
        };
        _context.Subjects.Add(subject);
        await _context.SaveChangesAsync();
        MockUser(teacherId);

        var result = await _controller.ChangeSubjectStatus(1, ContentStatus.Published);
        var dbSubject = await _context.Subjects.FindAsync(1);
        Assert.That(dbSubject.Status, Is.EqualTo(ContentStatus.Draft));
        Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
    }
}