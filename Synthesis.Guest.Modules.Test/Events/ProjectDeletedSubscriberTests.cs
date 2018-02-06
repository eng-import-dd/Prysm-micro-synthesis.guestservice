using System;
using Moq;
using Synthesis.EventBus.Events;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.EventHandlers;
using Synthesis.Logging;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Events
{
    public class ProjectDeletedSubscriberTests
    {
        private readonly ProjectDeletedEventHandler _target;
        private readonly Mock<IProjectLobbyStateController> _projectLobbyStateControllerMock = new Mock<IProjectLobbyStateController>();

        public ProjectDeletedSubscriberTests()
        {
            var loggerFactoryMock = new Mock<ILoggerFactory>();
            loggerFactoryMock
                .Setup(x => x.Get(It.IsAny<LogTopic>()))
                .Returns(new Mock<ILogger>().Object);

            _target = new ProjectDeletedEventHandler(loggerFactoryMock.Object, _projectLobbyStateControllerMock.Object);
        }

        [Fact]
        public void HandleProjectDeletedEventDeletesProjectLobbyState()
        {
            _target.HandleEvent(new GuidEvent(Guid.NewGuid()));
            _projectLobbyStateControllerMock.Verify(m => m.DeleteProjectLobbyStateAsync(It.IsAny<Guid>()));
        }
    }
}