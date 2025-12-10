using CourseProject.Controllers;
using CourseProject.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CourseProject.Tests;

[TestFixture]
public class HomeControllerTests
{
    [Test]
    public void Index_ReturnsViewResult()
    {
        var controller = new HomeController();
        var result = controller.Index();

        Assert.That(result, Is.InstanceOf<ViewResult>());
    }

    [Test]
    public void Error_ReturnsViewResult_With_ErrorViewModel()
    {
        var controller = new HomeController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var result = controller.Error();
        Assert.That(result, Is.InstanceOf<ViewResult>());

        var viewResult = result as ViewResult;
        Assert.That(viewResult?.Model, Is.InstanceOf<ErrorViewModel>());
    }
}