using System;
using Moq;
using Synthesis.Cache;
using Synthesis.EventBus.Events;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.Enumerations;
using Synthesis.GuestService.EventHandlers;
using Synthesis.GuestService.Utilities.Interfaces;
using Synthesis.Logging;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Events
{
    public class KickGuestsFromProjectHandlerTests
    {
        private readonly KickGuestsFromProjectHandler _target;
        private readonly Mock<IGuestSessionController> _guestSessionControllerMock = new Mock<IGuestSessionController>();
        private readonly Mock<ICache> _cacheMock = new Mock<ICache>();
        private readonly Mock<ICacheSelector> _cacheSelectorMock = new Mock<ICacheSelector>();

        public KickGuestsFromProjectHandlerTests()
        {
            _cacheSelectorMock.Setup(x => x[It.IsAny<CacheConnection>()])
                .Returns(_cacheMock.Object);

            var loggerFactoryMock = new Mock<ILoggerFactory>();
            loggerFactoryMock.Setup(m => m.Get(It.IsAny<LogTopic>()))
                .Returns(new Mock<ILogger>().Object);

            _target = new KickGuestsFromProjectHandler(loggerFactoryMock.Object, _guestSessionControllerMock.Object, _cacheSelectorMock.Object);
        }

        [Fact]
        public void HandleTriggerKickGuestsFromProjectEventDeletesGuestSessionsForProject()
        {
            _target.HandleEvent(new GuidEvent(Guid.NewGuid()));
            _guestSessionControllerMock.Verify(m => m.DeleteGuestSessionsForProjectAsync(It.IsAny<Guid>(), true));
        }

        [Fact]
        public void HandleTriggerKickGuestsFromProjectEventRetriesToDeletesGuestSessionsForProject()
        {
            _guestSessionControllerMock.Setup(x => x.DeleteGuestSessionsForProjectAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
                .ThrowsAsync(new Exception());

            _target.HandleEvent(new GuidEvent(Guid.NewGuid()));

            _guestSessionControllerMock.Verify(m => m.DeleteGuestSessionsForProjectAsync(It.IsAny<Guid>(), true), Times.Exactly(2));
        }

        [Fact]
        public void HandleTriggerKickGuestsFromProjectEventSetsKickKeyOnKickFailure()
        {
            _guestSessionControllerMock.Setup(x => x.DeleteGuestSessionsForProjectAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
                .ThrowsAsync(new Exception());

            _target.HandleEvent(new GuidEvent(Guid.NewGuid()));

            _cacheMock.Verify(x => x.ItemSet(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan?>(), It.IsAny<CacheCommandOptions>()));
        }
    }
}
