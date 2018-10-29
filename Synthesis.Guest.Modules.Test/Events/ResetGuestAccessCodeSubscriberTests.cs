using System;
using Moq;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.EventHandlers;
using Synthesis.Logging;
using Synthesis.ProjectService.InternalApi.Models;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Events
{
    public class GuestAccessCodeChangedEventHandlerTests
    {
        private readonly GuestAccessCodeChangedEventHandler _target;
        private readonly Mock<IGuestSessionController> _guestSessionControllerMock = new Mock<IGuestSessionController>();
        private readonly Mock<IGuestInviteController> _guestInviteControllerMock = new Mock<IGuestInviteController>();

        public GuestAccessCodeChangedEventHandlerTests()
        {
            var loggerFactoryMock = new Mock<ILoggerFactory>();
            loggerFactoryMock
                .Setup(x => x.Get(It.IsAny<LogTopic>()))
                .Returns(new Mock<ILogger>().Object);

            _target = new GuestAccessCodeChangedEventHandler(loggerFactoryMock.Object, _guestSessionControllerMock.Object, _guestInviteControllerMock.Object);
        }

        [Fact]
        public void HandleGuestAccessCodeChangedEvent_CallsEndGuestSessionsForProjectAsync()
        {
            var projectId = Guid.NewGuid();
            _target.HandleEvent(new GuestAccessCodeChanged { ProjectId = projectId });
            _guestSessionControllerMock.Verify(x => x.EndGuestSessionsForProjectAsync(It.Is<Guid>(v => v == projectId), It.IsAny<Guid>(), false));
        }
    }
}