using System;
using Moq;
using Synthesis.EventBus.Events;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.EventHandlers;
using Synthesis.Logging;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Events
{
    public class ExpirationNotifierEventHandlerTests
    {
        private readonly KickGuestsFromProjectHandler _target;
        private readonly Mock<IGuestSessionController> _guestSessionControllerMock = new Mock<IGuestSessionController>();

        public ExpirationNotifierEventHandlerTests()
        {
            var loggerFactoryMock = new Mock<ILoggerFactory>();
            loggerFactoryMock.Setup(m => m.Get(It.IsAny<LogTopic>()))
                .Returns(new Mock<ILogger>().Object);

            _target = new KickGuestsFromProjectHandler(loggerFactoryMock.Object, _guestSessionControllerMock.Object);
        }

        [Fact]
        public void HandleTriggerKickGuestsFromProjectEventDeletesGuestSessionsForProject()
        {
            _target.HandleEvent(new GuidEvent(Guid.NewGuid()));
            _guestSessionControllerMock.Verify(m => m.DeleteGuestSessionsForProjectAsync(It.IsAny<Guid>(), true));
        }
    }
}
