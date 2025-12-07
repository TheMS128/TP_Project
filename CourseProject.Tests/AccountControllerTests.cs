using System.Security.Claims;
using CourseProject.Controllers;
using CourseProject.DataBase.DbModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CourseProject.Tests;

[TestFixture]
public class AccountControllerTests
{
    private Mock<UserManager<User>> _mockUserManager;
    private Mock<SignInManager<User>> _mockSignInManager;
    private AccountController _controller;

    [SetUp]
    public void Setup()
    {
        var store = new Mock<IUserStore<User>>();
        _mockUserManager = new Mock<UserManager<User>>(store.Object, null, null, null, null, null, null, null, null);

        var contextAccessor = new Mock<IHttpContextAccessor>();
        var userPrincipalFactory = new Mock<IUserClaimsPrincipalFactory<User>>();

        _mockSignInManager = new Mock<SignInManager<User>>(
            _mockUserManager.Object,
            contextAccessor.Object,
            userPrincipalFactory.Object,
            null, null, null, null);

        _controller = new AccountController(_mockUserManager.Object, _mockSignInManager.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _controller.Dispose();
    }

    [Test]
    public void Login_Get_AuthenticatedUser_RedirectsHome()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.Name, "testuser"),
        }, "mock"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        var result = _controller.Login();
        var redirect = result as RedirectToActionResult;
        Assert.That(redirect, Is.Not.Null);
        Assert.That(redirect.ActionName, Is.EqualTo("Index"));
        Assert.That(redirect.ControllerName, Is.EqualTo("Home"));
    }

    [Test]
    public void Login_Get_AnonymousUser_ReturnsView()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var result = _controller.Login();
        Assert.That(result, Is.InstanceOf<ViewResult>());
    }

    [Test]
    public async Task Login_Post_ValidCredentials_Admin_RedirectsToAdmin()
    {
        string email = "admin@test.com";
        string password = "password";
        var user = new User { UserName = "admin", Email = email };

        _mockUserManager.Setup(x => x.FindByEmailAsync(email)).ReturnsAsync(user);
        _mockSignInManager.Setup(x => x.PasswordSignInAsync(user.UserName, password, false, false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);
        _mockUserManager.Setup(x => x.IsInRoleAsync(user, "Admin")).ReturnsAsync(true);

        var result = await _controller.Login(email, password);
        var redirect = result as RedirectToActionResult;

        Assert.That(redirect, Is.Not.Null);
        Assert.That(redirect.ActionName, Is.EqualTo("Index"));
        Assert.That(redirect.ControllerName, Is.EqualTo("Admin"));
    }

    [Test]
    public async Task Login_Post_ValidCredentials_User_RedirectsHome()
    {
        string email = "user@test.com";
        string password = "password";
        var user = new User { UserName = "user", Email = email };

        _mockUserManager.Setup(x => x.FindByEmailAsync(email)).ReturnsAsync(user);
        _mockSignInManager.Setup(x => x.PasswordSignInAsync(user.UserName, password, false, false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);
        _mockUserManager.Setup(x => x.IsInRoleAsync(user, "Admin")).ReturnsAsync(false);

        var result = await _controller.Login(email, password);
        var redirect = result as RedirectToActionResult;

        Assert.That(redirect, Is.Not.Null);
        Assert.That(redirect.ActionName, Is.EqualTo("Index"));
        Assert.That(redirect.ControllerName, Is.EqualTo("Home"));
    }

    [Test]
    public async Task Login_Post_InvalidCredentials_ReturnsViewWithModelError()
    {
        string email = "wrong@test.com";
        string password = "wrong";
        var user = new User { UserName = "wrong", Email = email };

        _mockUserManager.Setup(x => x.FindByEmailAsync(email)).ReturnsAsync(user);
        _mockSignInManager.Setup(x => x.PasswordSignInAsync(user.UserName, password, false, false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        var result = await _controller.Login(email, password);
        var viewResult = result as ViewResult;

        Assert.That(viewResult, Is.Not.Null);
        Assert.That(_controller.ModelState.IsValid, Is.False);
        Assert.That(_controller.ModelState.ErrorCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Logout_RedirectsToLogin()
    {
        _mockSignInManager.Setup(x => x.SignOutAsync()).Returns(Task.CompletedTask);

        var result = await _controller.Logout();
        var redirect = result as RedirectToActionResult;

        Assert.That(redirect, Is.Not.Null);
        Assert.That(redirect.ActionName, Is.EqualTo("Login"));
        Assert.That(redirect.ControllerName, Is.EqualTo("Account"));
    }
}