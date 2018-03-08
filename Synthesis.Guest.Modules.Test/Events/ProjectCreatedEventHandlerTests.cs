using System;
using Moq;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.EventHandlers;
using Synthesis.Logging;
using Synthesis.ProjectService.InternalApi.Models;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Events
{
    public class ProjectCreatedEventHandlerTests
    {
        private readonly ProjectCreatedEventHandler _target;
        private readonly Mock<IProjectLobbyStateController> _projectLobbyStateControllerMock = new Mock<IProjectLobbyStateController>();

        public ProjectCreatedEventHandlerTests()
        {
            var loggerFactoryMock = new Mock<ILoggerFactory>();
            loggerFactoryMock
                .Setup(x => x.Get(It.IsAny<LogTopic>()))
                .Returns(new Mock<ILogger>().Object);

            _target = new ProjectCreatedEventHandler(loggerFactoryMock.Object, _projectLobbyStateControllerMock.Object);
        }

        [Fact]
        public void HandleProjectCreatedEventCreatesProjectLobbyState()
        {
            _target.HandleEvent(new Project());
            _projectLobbyStateControllerMock.Verify(m => m.CreateProjectLobbyStateAsync(It.IsAny<Guid>()));
        }
    }
}