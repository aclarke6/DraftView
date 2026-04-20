using System.Reflection;
using DraftView.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Services;
using DraftView.Web.Controllers;
using DraftView.Web.Models;

namespace DraftView.Web.Tests.Controllers;

public class SupportControllerTests
{
    // ---------------------------------------------------------------------------
    // Authorization
    // ---------------------------------------------------------------------------

    [Fact]
    public void PostMessage_RequiresSystemSupportRole()
    {
        var type = typeof(SupportController);
        var attr = type.GetCustomAttributes(inherit: true)
            .OfType<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(attr);
        Assert.Equal("SystemSupport", attr!.Roles);
    }

    // ---------------------------------------------------------------------------
    // PostMessage
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task PostMessage_CallsCreateMessageAsync()
    {
        var service    = new Mock<ISystemStateMessageService>();
        var dashboard  = new Mock<IDashboardService>();
        var controller = new SupportController(service.Object, dashboard.Object)
        {
            TempData = new Mock<ITempDataDictionary>().Object
        };

        await controller.PostMessage("Scheduled maintenance.", SystemStateMessageSeverity.Info);

        service.Verify(s => s.CreateMessageAsync(
            "Scheduled maintenance.",
            SystemStateMessageSeverity.Info,
            default), Times.Once);
    }

    // ---------------------------------------------------------------------------
    // RevokeMessage
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task RevokeMessage_CallsDeactivateMessageAsync()
    {
        var service    = new Mock<ISystemStateMessageService>();
        var dashboard  = new Mock<IDashboardService>();
        var messageId  = Guid.NewGuid();
        var controller = new SupportController(service.Object, dashboard.Object)
        {
            TempData = new Mock<ITempDataDictionary>().Object
        };

        await controller.RevokeMessage(messageId);

        service.Verify(s => s.DeactivateMessageAsync(messageId, default), Times.Once);
    }

    [Fact]
    public async Task Readers_ReturnsDisplayNameOnlyRows()
    {
        var service = new Mock<ISystemStateMessageService>();
        var dashboard = new Mock<IDashboardService>();
        var activeReader = User.Create("active.reader@example.test", "Active Reader", Role.BetaReader);
        activeReader.Activate();

        var softDeletedReader = User.Create("deleted.reader@example.test", "Deleted Reader", Role.BetaReader);
        softDeletedReader.SoftDelete();

        dashboard.Setup(r => r.GetReaderSummaryAsync(default))
            .ReturnsAsync(new List<User> { activeReader, softDeletedReader });

        var controller = new SupportController(service.Object, dashboard.Object)
        {
            TempData = new Mock<ITempDataDictionary>().Object
        };

        var result = await controller.Readers();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<SupportReadersViewModel>(view.Model);
        Assert.Single(model.Readers);
        Assert.Equal("Active Reader", model.Readers[0].DisplayName);
    }
}
