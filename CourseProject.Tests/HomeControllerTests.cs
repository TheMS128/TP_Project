using System.Diagnostics;
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
    public void Error_ReturnsViewResult_With_Populated_ErrorViewModel()
    {
        var controller = new HomeController();
        var testTraceId = "test-trace-id-123";
        
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { TraceIdentifier = testTraceId }
        };

        var result = controller.Error();

        Assert.That(result, Is.InstanceOf<ViewResult>());
        var viewResult = result as ViewResult;

        Assert.That(viewResult?.Model, Is.InstanceOf<ErrorViewModel>());
        var model = viewResult?.Model as ErrorViewModel;

        Assert.That(model?.RequestId, Is.Not.Null.And.Not.Empty);
        
        if (Activity.Current == null)
        {
            Assert.That(model.RequestId, Is.EqualTo(testTraceId));
        }
    }
}