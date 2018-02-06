using System;
using Moq;
using Synthesis.EventBus.Events;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.EventHandlers;
using Synthesis.Logging;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Events
{
    public class RecalculateProjectLobbyStateHandlerTests
    {
        private readonly RecalculateProjectLobbyStateHandler _target;
        private readonly Mock<IProjectLobbyStateController> _projectLobbyStateControllerMock = new Mock<IProjectLobbyStateController>();

        public RecalculateProjectLobbyStateHandlerTests()
        {
            var loggerFactoryMock = new Mock<ILoggerFactory>();
            loggerFactoryMock
                .Setup(m => m.Get(It.IsAny<LogTopic>()))
                .Returns(new Mock<ILogger>().Object);

            _target = new RecalculateProjectLobbyStateHandler(loggerFactoryMock.Object, _projectLobbyStateControllerMock.Object);
        }

        [Fact]
        public void HandleTriggerRecalculateProjectLobbyStateEventRecalculatesLobbyState()
        {
            _target.HandleEvent(new GuidEvent(Guid.NewGuid()));
            _projectLobbyStateControllerMock.Verify(m => m.RecalculateProjectLobbyStateAsync(It.IsAny<Guid>()));
        }
    }
}
