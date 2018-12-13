using System;
using Moq;
using Synthesis.EventBus;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.EventHandlers;
using Synthesis.GuestService.InternalApi.Constants;
using Synthesis.GuestService.InternalApi.Enums;
using Synthesis.GuestService.InternalApi.Models;
using Synthesis.Logging;
using Synthesis.ParticipantService.InternalApi.Models;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Events
{
    public class SessionEndedEventHandlerTests
    {
        private readonly Mock<IProjectLobbyStateController> _projectLobbyStateControllerMock = new Mock<IProjectLobbyStateController>();
        private readonly Mock<IGuestSessionController> _guestSessionControllerMock = new Mock<IGuestSessionController>();
        private readonly Mock<ILoggerFactory> _loggerFactoryMock = new Mock<ILoggerFactory>();
        private readonly SessionEndedEventHandler _target;

        public SessionEndedEventHandlerTests()
        {
            _loggerFactoryMock.Setup(m => m.Get(It.IsAny<LogTopic>()))
                .Returns(new Mock<ILogger>().Object);

            _target = new SessionEndedEventHandler(
                _guestSessionControllerMock.Object,
                _projectLobbyStateControllerMock.Object,
                _loggerFactoryMock.Object);
        }

        [Fact]
        public void HandleEvent_WhenSessionNotFound_DoesNotUpdateGuestSession()
        {
            _guestSessionControllerMock.Setup(m => m.GetGuestSessionBySessionIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((GuestSession)null);

            _target.HandleEvent(new SessionEnded());

            _guestSessionControllerMock.Verify(m => m.UpdateGuestSessionAsync(It.IsAny<GuestSession>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public void HandleEvent_WhenSessionFoundAndStateIsEnded_DoesNotUpdateGuestSession()
        {
            _guestSessionControllerMock.Setup(m => m.GetGuestSessionBySessionIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new GuestSession { GuestSessionState = GuestState.Ended });

            _target.HandleEvent(new SessionEnded());

            _guestSessionControllerMock.Verify(m => m.UpdateGuestSessionAsync(It.IsAny<GuestSession>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public void HandleEvent_WhenSessionFoundAndStateIsNotEnded_ChangesStateToEnded()
        {
            _guestSessionControllerMock.Setup(m => m.GetGuestSessionBySessionIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new GuestSession { GuestSessionState = GuestState.InLobby });

            _target.HandleEvent(new SessionEnded());

            _guestSessionControllerMock.Verify(m => m.UpdateGuestSessionAsync(It.Is<GuestSession>(s => s.GuestSessionState == GuestState.Ended), It.IsAny<Guid>()), Times.Once);
        }

        [Fact]
        public void HandleEvent_WhenSessionFoundAndStateIsNotEnded_RecalculatesProjectLobbyState()
        {
            _guestSessionControllerMock.Setup(m => m.GetGuestSessionBySessionIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new GuestSession { GuestSessionState = GuestState.InProject });

            _target.HandleEvent(new SessionEnded());

            _projectLobbyStateControllerMock.Verify(m => m.RecalculateProjectLobbyStateAsync(It.IsAny<Guid>()), Times.Once);
        }
    }
}