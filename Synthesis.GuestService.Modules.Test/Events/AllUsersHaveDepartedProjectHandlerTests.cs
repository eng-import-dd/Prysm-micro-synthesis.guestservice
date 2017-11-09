using System;
using Moq;
using Synthesis.EventBus.Events;
using Synthesis.GuestService.Events;
using Synthesis.GuestService.Workflow.Controllers;
using Synthesis.Logging;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Events
{
    public class AllUsersHaveDepartedProjectHandlerTests
    {
        private readonly AllUsersHaveDepartedProjectHandler _target;
        private readonly Mock<IGuestSessionController> _controllerMock = new Mock<IGuestSessionController>();

        public AllUsersHaveDepartedProjectHandlerTests()
        {
            var loggerFactoryMock = new Mock<ILoggerFactory>();
            loggerFactoryMock
                .Setup(x => x.Get(It.IsAny<LogTopic>()))
                .Returns(new Mock<ILogger>().Object);

            _target = new AllUsersHaveDepartedProjectHandler(loggerFactoryMock.Object, _controllerMock.Object);
        }

        [Fact]
        public void HandleEventCallsSetFollowMeStateAsync()
        {
            var projectId = Guid.NewGuid();
            _target.HandleEvent(new GuidEvent(projectId));

            _controllerMock
                .Verify(x => x.DeleteGuestSessionsForProject(projectId, false));
        }
    }
}