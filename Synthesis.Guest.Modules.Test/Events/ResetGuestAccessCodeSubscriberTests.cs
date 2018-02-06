using Moq;
using Synthesis.EventBus.Events;
using Synthesis.GuestService.Controllers;
using Synthesis.Logging;
using System;
using Synthesis.GuestService.EventHandlers;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Events
{
    public class GuestAccessCodeChangedEventHandlerTests
    {
        private readonly GuestAccessCodeChangedEventHandler _target;
        private readonly Mock<IGuestSessionController> _guestSessionControllerMock = new Mock<IGuestSessionController>();

        public GuestAccessCodeChangedEventHandlerTests()
        {
            var loggerFactoryMock = new Mock<ILoggerFactory>();
            loggerFactoryMock
                .Setup(x => x.Get(It.IsAny<LogTopic>()))
                .Returns(new Mock<ILogger>().Object);

            _target = new GuestAccessCodeChangedEventHandler(loggerFactoryMock.Object, _guestSessionControllerMock.Object);
        }

        [Fact]
        public void HandleGuestAccessCodeChangedEventCallsDeleteGuestSessionsForProjectAsync()
        {
            _target.HandleEvent(new GuidEvent(Guid.NewGuid()));
            _guestSessionControllerMock.Verify(x => x.DeleteGuestSessionsForProjectAsync(It.IsAny<Guid>(), true));
        }
    }
}