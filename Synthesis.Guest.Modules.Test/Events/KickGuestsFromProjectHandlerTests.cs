using System;
using Moq;
using Synthesis.ExpirationNotifierService.InternalApi.Models;
using Synthesis.ExpirationNotifierService.InternalApi.Services;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.EventHandlers;
using Synthesis.Logging;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Events
{
    public class KickGuestsFromProjectHandlerTests
    {
        private readonly KickGuestsFromProjectHandler _target;
        private readonly Mock<IGuestSessionController> _guestSessionControllerMock = new Mock<IGuestSessionController>();
        private readonly Mock<INotificationService> _cacheNotificationMock = new Mock<INotificationService>();

        private readonly KickGuestsFromProjectRequest _kickRequest = KickGuestsFromProjectRequest.Example();

        public KickGuestsFromProjectHandlerTests()
        {
            var loggerFactoryMock = new Mock<ILoggerFactory>();
            loggerFactoryMock.Setup(m => m.Get(It.IsAny<LogTopic>()))
                .Returns(new Mock<ILogger>().Object);

            _target = new KickGuestsFromProjectHandler(_cacheNotificationMock.Object, loggerFactoryMock.Object, _guestSessionControllerMock.Object);
        }

        [Fact]
        public void HandleTriggerKickGuestsFromProject_ForValidEvent_DeletesGuestSessionsForProject()
        {
            _target.HandleEvent(_kickRequest);
            _guestSessionControllerMock.Verify(m => m.EndGuestSessionsForProjectAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), true));
        }

        [Fact]
        public void HandleTriggerKickGuestsFromProject_OnException_RetriesDeletingGuestSessionsForProject()
        {
            _guestSessionControllerMock.Setup(x => x.EndGuestSessionsForProjectAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<bool>()))
                .ThrowsAsync(new Exception());

            _target.HandleEvent(_kickRequest);

            _guestSessionControllerMock.Verify(m => m.EndGuestSessionsForProjectAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), true), Times.Exactly(2));
        }
    }
}
