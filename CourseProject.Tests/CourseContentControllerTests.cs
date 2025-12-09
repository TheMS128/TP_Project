using Microsoft.Data.Sqlite;
using System.Security.Claims;
using CourseProject.Controllers;
using CourseProject.DataBase;
using CourseProject.DataBase.DbModels;
using CourseProject.DataBase.Enums;
using CourseProject.Models.CourseContentViewModels.Lecture;
using CourseProject.Models.CourseContentViewModels.Test;
using CourseProject.Models.SubjectModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CourseProject.Tests;

[TestFixture]
public class CourseContentControllerTests
{
    private ApplicationDbContext _context;
    private CourseContentController _controller;
    private SqliteConnection _connection;
    private Mock<IWebHostEnvironment> _mockEnvironment;
    private Mock<UserManager<User>> _mockUserManager;

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

        _mockEnvironment = new Mock<IWebHostEnvironment>();
        _mockEnvironment.Setup(m => m.WebRootPath).Returns("TestWebRootPath");

        var userStoreMock = new Mock<IUserStore<User>>();
        _mockUserManager = new Mock<UserManager<User>>(
            userStoreMock.Object, null, null, null, null, null, null, null, null);

        _controller = new CourseContentController(_context, _mockUserManager.Object, _mockEnvironment.Object);
        _controller.TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
    }

    [TearDown]
    public void TearDown()
    {
        _context?.Dispose();
        _connection?.Close();
        _controller?.Dispose();
    }

    private void SetUser(string userId, string role)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, role)
        }, "mock"));

        _controller.ControllerContext = new ControllerContext()
        {
            HttpContext = new DefaultHttpContext() { User = user }
        };

        _mockUserManager.Setup(u => u.GetUserId(user)).Returns(userId);
    }

    private async Task SeedDataAsync()
    {
        var t1 = new User { Id = "t1", UserName = "t1", FullName = "Teacher One", Description = "Desc" };
        var t2 = new User { Id = "t2", UserName = "t2", FullName = "Teacher Two", Description = "Desc" };

        var subject1 = new Subject
        {
            Id = 1,
            Title = "My Subject",
            Description = "Desc",
            Teachers = new List<User>(),
            Lectures = new List<Lecture>(),
            Tests = new List<Test>()
        };

        var subject2 = new Subject
        {
            Id = 2,
            Title = "Other Subject",
            Description = "Desc",
            Teachers = new List<User>()
        };

        _context.Users.AddRange(t1, t2);
        _context.Subjects.AddRange(subject1, subject2);
        await _context.SaveChangesAsync();

        var s1 = await _context.Subjects.FindAsync(1);
        s1.Teachers.Add(t1);

        var s2 = await _context.Subjects.FindAsync(2);
        s2.Teachers.Add(t2);

        await _context.SaveChangesAsync();
    }

    [Test]
    public async Task Index_TeacherWithAccess_ReturnsView()
    {
        await SeedDataAsync();
        SetUser("t1", "Teacher");

        var result = await _controller.Index(1);

        Assert.That(result, Is.InstanceOf<ViewResult>());
        var model = ((ViewResult)result).Model as CourseContentModel;
        Assert.That(model.Id, Is.EqualTo(1));
    }

    [Test]
    public async Task Index_TeacherNoAccess_ReturnsForbid()
    {
        await SeedDataAsync();
        SetUser("t1", "Teacher");

        var result = await _controller.Index(2);

        Assert.That(result, Is.InstanceOf<ForbidResult>());
    }

    [Test]
    public async Task Index_Admin_ReturnsView()
    {
        await SeedDataAsync();
        SetUser("admin", "Admin");

        var result = await _controller.Index(2);

        Assert.That(result, Is.InstanceOf<ViewResult>());
    }

    [Test]
    public async Task ChangeSubjectStatus_PublishEmpty_ReturnsError()
    {
        await SeedDataAsync();
        SetUser("t1", "Teacher");

        var result = await _controller.ChangeSubjectStatus(1, ContentStatus.Published);
        var redirect = result as RedirectToActionResult;
        Assert.That(redirect.ActionName, Is.EqualTo("Index"));

        var subject = await _context.Subjects.FindAsync(1);
        Assert.That(subject.Status, Is.EqualTo(ContentStatus.Hidden)); // Не изменился
        Assert.That(_controller.TempData["SubjectStatusError"], Is.Not.Null);
    }

    [Test]
    public async Task ChangeSubjectStatus_PublishValid_Success()
    {
        await SeedDataAsync();
        SetUser("t1", "Teacher");

        var subject = await _context.Subjects.FindAsync(1);
        subject.Lectures.Add(new Lecture { Title = "L1", FilePath = "path", Status = ContentStatus.Hidden });
        subject.Tests.Add(new Test
        {
            Title = "T1",
            Questions = new List<Question> { new Question { Text = "Q1", Type = "Text", Points = 1 } }
        });
        await _context.SaveChangesAsync();

        var result = await _controller.ChangeSubjectStatus(1, ContentStatus.Published);

        var subjectDb = await _context.Subjects.FindAsync(1);
        Assert.That(subjectDb.Status, Is.EqualTo(ContentStatus.Published));
        Assert.That(_controller.TempData["SubjectStatusSuccess"], Is.Not.Null);
    }

    [Test]
    public async Task CreateLecture_ValidFile_SavesAndRedirects()
    {
        await SeedDataAsync();
        SetUser("t1", "Teacher");

        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns("doc.pdf");
        fileMock.Setup(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var model = new LectureViewModel
        {
            SubjectId = 1,
            Title = "New Lecture",
            UploadedFile = fileMock.Object
        };

        var result = await _controller.CreateLecture(model);

        var lecture = await _context.Lectures.FirstOrDefaultAsync();
        Assert.That(lecture, Is.Not.Null);
        Assert.That(lecture.Title, Is.EqualTo("New Lecture"));
        Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
    }
    
    [Test]
    public async Task CreateTest_ValidModel_SavesTest()
    {
        await SeedDataAsync();
        SetUser("t1", "Teacher");

        var model = new TestViewModel
        {
            SubjectId = 1,
            Title = "Quiz 1",
            TimeLimitMinutes = 30
        };

        var result = await _controller.CreateTest(model);

        var test = await _context.Tests.FirstOrDefaultAsync();
        Assert.That(test, Is.Not.Null);
        Assert.That(test.Title, Is.EqualTo("Quiz 1"));
        Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
    }

    [Test]
    public async Task ChangeTestStatus_PublishNoQuestions_ReturnsError()
    {
        await SeedDataAsync();
        SetUser("t1", "Teacher");

        var test = new Test { SubjectId = 1, Title = "Empty Test", Status = ContentStatus.Hidden };
        _context.Tests.Add(test);
        await _context.SaveChangesAsync();

        var result = await _controller.ChangeTestStatus(test.Id, ContentStatus.Published);

        var dbTest = await _context.Tests.FindAsync(test.Id);
        Assert.That(dbTest.Status, Is.EqualTo(ContentStatus.Hidden));
        Assert.That(_controller.TempData["ErrorMessage"], Is.Not.Null);
    }

    [Test]
    public async Task CreateQuestion_ValidSingle_Saves()
    {
        await SeedDataAsync();
        SetUser("t1", "Teacher");

        var test = new Test { SubjectId = 1, Title = "T1" };
        _context.Tests.Add(test);
        await _context.SaveChangesAsync();

        var model = new QuestionViewModel
        {
            TestId = test.Id,
            SubjectId = 1,
            Text = "2+2?",
            Type = "Single",
            Points = 1,
            Answers = new List<AnswerOptionViewModel>
            {
                new AnswerOptionViewModel { Text = "4", IsCorrect = true },
                new AnswerOptionViewModel { Text = "5", IsCorrect = false }
            }
        };
        var result = await _controller.CreateQuestion(model);
        var question = await _context.Questions.Include(q => q.AnswerOptions).FirstOrDefaultAsync();
        
        Assert.That(question, Is.Not.Null);
        Assert.That(question.AnswerOptions.Count, Is.EqualTo(2));
        Assert.That(result, Is.InstanceOf<RedirectToActionResult>());
    }

    [Test]
    public async Task CreateQuestion_InvalidLogic_ReturnsViewWithError()
    {
        await SeedDataAsync();
        SetUser("t1", "Teacher");

        var test = new Test { SubjectId = 1, Title = "T1" };
        _context.Tests.Add(test);
        await _context.SaveChangesAsync();

        var model = new QuestionViewModel
        {
            TestId = test.Id,
            SubjectId = 1,
            Text = "Invalid?",
            Type = "Single", 
            Points = 1,
            Answers = new List<AnswerOptionViewModel>
            {
                new AnswerOptionViewModel { Text = "A", IsCorrect = false },
                new AnswerOptionViewModel { Text = "B", IsCorrect = false }
            }
        };
        var result = await _controller.CreateQuestion(model);

        Assert.That(result, Is.InstanceOf<ViewResult>());
        Assert.That(_controller.ModelState.IsValid, Is.False);
    }
}