using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;
using DraftView.Domain.Enumerations;
using DraftView.Domain.Interfaces.Services;
using DraftView.Web.Controllers;

namespace DraftView.Web.Tests;

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
        var controller = new SupportController(service.Object)
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
        var messageId  = Guid.NewGuid();
        var controller = new SupportController(service.Object)
        {
            TempData = new Mock<ITempDataDictionary>().Object
        };

        await controller.RevokeMessage(messageId);

        service.Verify(s => s.DeactivateMessageAsync(messageId, default), Times.Once);
    }
}
