
using DraftView.Domain.Interfaces.Repositories;
using DraftView.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace DraftView.Web.Tests.Controllers;

public class HomeControllerTests
{
    [Fact]
    public void Privacy_ReturnsView()
    {
        var userRepo = new Mock<IUserRepository>();
        var controller = new HomeController(userRepo.Object);

        var result = controller.Privacy();

        Assert.IsType<ViewResult>(result);
    }
}
