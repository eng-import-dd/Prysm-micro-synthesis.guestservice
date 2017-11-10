using System;
using Moq;
using Synthesis.EventBus.Events;
using Synthesis.GuestService.Events;
using Synthesis.GuestService.Workflow.Controllers;
using Synthesis.Logging;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Events
{
    public class ResetGuestAccessCodeSubscriberTests
    {
        private readonly ResetGuestAccessCodeHandler _target;
        private readonly Mock<IGuestSessionController> _controllerMock = new Mock<IGuestSessionController>();

        public ResetGuestAccessCodeSubscriberTests()
        {
            var loggerFactoryMock = new Mock<ILoggerFactory>();
            loggerFactoryMock
                .Setup(x => x.Get(It.IsAny<LogTopic>()))
                .Returns(new Mock<ILogger>().Object);

            _target = new ResetGuestAccessCodeHandler(loggerFactoryMock.Object, _controllerMock.Object);
        }

        [Fact]
        public void HandleEventCallsDeleteGuestSessionsForProjectAsync()
        {
            var projectId = Guid.NewGuid();
            _target.HandleEvent(new GuidEvent(projectId));

            _controllerMock
                .Verify(x => x.DeleteGuestSessionsForProjectAsync(projectId, true));
        }
    }
}