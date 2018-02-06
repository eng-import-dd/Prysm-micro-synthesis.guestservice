using Moq;
using Synthesis.EventBus.Events;
using Synthesis.GuestService.Controllers;
using Synthesis.Logging;
using System;
using Synthesis.GuestService.EventHandlers;
using Synthesis.GuestService.Models;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Events
{
    public class ResetGuestAccessCodeSubscriberTests
    {
        private readonly GuestAccessCodeChangedEventHandler _target;
        private readonly Mock<IGuestSessionController> _guestSessionControllerMock = new Mock<IGuestSessionController>();
        private readonly Mock<IProjectLobbyStateController> _projectLobbyStateControllerMock = new Mock<IProjectLobbyStateController>();

        public ResetGuestAccessCodeSubscriberTests()
        {
            var loggerFactoryMock = new Mock<ILoggerFactory>();
            loggerFactoryMock
                .Setup(x => x.Get(It.IsAny<LogTopic>()))
                .Returns(new Mock<ILogger>().Object);

            _target = new GuestAccessCodeChangedEventHandler(loggerFactoryMock.Object,
                _guestSessionControllerMock.Object,
                _projectLobbyStateControllerMock.Object);
        }

        [Fact]
        public void HandleGuestAccessCodeChangedEventCallsDeleteGuestSessionsForProjectAsync()
        {
            _target.HandleGuestAccessCodeChangedEvent(new GuidEvent(Guid.NewGuid()));
            _guestSessionControllerMock.Verify(x => x.DeleteGuestSessionsForProjectAsync(It.IsAny<Guid>(), true));
        }

        [Fact]
        public void HandleProjectCreatedEventCreatesProjectLobbyState()
        {
            _target.HandleProjectCreatedEvent(new Project());
            _projectLobbyStateControllerMock.Verify(m => m.CreateProjectLobbyStateAsync(It.IsAny<Guid>()));
        }

        [Fact]
        public void HandleProjectDeletedEventDeletesProjectLobbyState()
        {
            _target.HandleProjectDeletedEvent(new GuidEvent(Guid.NewGuid()));
            _projectLobbyStateControllerMock.Verify(m => m.DeleteProjectLobbyStateAsync(It.IsAny<Guid>()));
        }
    }
}