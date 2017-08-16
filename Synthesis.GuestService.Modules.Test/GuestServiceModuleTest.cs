using System;
using System.Collections.Generic;
using System.Text;
using Jose;
using Moq;
using Nancy;
using Nancy.Testing;
using Org.BouncyCastle.Security;
using Synthesis.KeyManager;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService.Authorization;
using Synthesis.Nancy.MicroService.Security;
using Synthesis.GuestService.Modules;
using Xunit;
using Xunit.Abstractions;

namespace Synthesis.GuestService.Test
{
    public class GuestServiceModuleTest
    {
        private Browser _browser;
        private readonly ITestOutputHelper _output;
        private IKeyManager _keyManager;

        public GuestServiceModuleTest(ITestOutputHelper output)
        {
            _browser = new Browser(with =>
            {
                var mockKeyManager = new Mock<IKeyManager>();
                var key = new Mock<IKey>();
                key.Setup(k => k.GetContent()).Returns(Encoding.ASCII.GetBytes("This is a test of the emergency broadcast system...."));
                mockKeyManager.Setup(km => km.GetKey("JWT_KEY"))
                    .Returns(key.Object);

                var keyManager = mockKeyManager.Object;
                var mockLoggingService = new Mock<ILoggingService>();
                mockLoggingService.Setup(logger => logger.Log(It.IsAny<LogTopic>(), It.IsAny<string>())).Callback((LogTopic a, string b) => output.WriteLine(b));
                mockLoggingService.Setup(logger => logger.LogInfo(It.IsAny<LogTopic>(), It.IsAny<string>())).Callback((LogTopic a, string b) => output.WriteLine(b));
                mockLoggingService.Setup(logger => logger.LogError(It.IsAny<LogTopic>(), It.IsAny<string>())).Callback((LogTopic a, string b) => output.WriteLine(b));
                mockLoggingService.Setup(logger => logger.LogError(It.IsAny<Exception>(), It.IsAny<string>())).Callback((Exception a, string b) => output.WriteLine($"{b}:{a}"));
                mockLoggingService.Setup(logger => logger.LogError(It.IsAny<LogTopic>(), It.IsAny<Exception>(), It.IsAny<string>())).Callback((LogTopic a, Exception b, string c) => output.WriteLine($"{c}:{b.ToString()}"));
                var loggingService = mockLoggingService.Object;

                var config = new SynthesisStatelessAuthorization(keyManager, loggingService);

                with.EnableAutoRegistration();
                with.ApplicationStartup((container, pipelines) =>
                {
                    StatelessAuthorization.Enable(pipelines, config);
                });
                var configurableBootstrapperConfigurator = with.Dependency(keyManager);
                with.Dependency(loggingService);
                with.Module<GuestServiceModule>();
                _keyManager = keyManager;
            });
        }

        [Fact]
        public async void respond_with_unauthorized_no_bearer()
        {
            var actual = await _browser.Get($"/api/v1/guestservice/health", with =>
            {
                with.Header("Accept", "application/json");
                with.Header("Content-Type", "application/json");
                with.HttpRequest();
            });
            Assert.Equal(HttpStatusCode.Unauthorized, actual.StatusCode);
        }

        [Fact]
        public async void respond_with_ok()
        {
            var actual = await _browser.Get($"/api/v1/guestservice/health", with =>
            {
                with.Header("Accept", "application/json");
                with.Header("Content-Type", "application/json");
                with.Header("Authorization", $"Bearer {Token()}");
                with.HttpRequest();
            });
            Assert.Equal(HttpStatusCode.OK, actual.StatusCode);
        }

        private string Token()
        {
            var now = System.DateTime.Now.Ticks;
            var exp = now + (TimeSpan.TicksPerHour * 8);  // FIXME this should be configurable but is set for 8 hours
            var jti = Convert.ToBase64String(GeneratorUtilities.GetKeyGenerator("AES256").GenerateKey());
            var payload = new Dictionary<string, object>()
                {
                    {"sub", Guid.NewGuid()},
                    {"iat", now },
                    {"jti", jti },
                    {"exp" , exp},
                    {"roles", new string[] {} },
                    {"username", "noone@nowhere.com" },
                    {"account", Guid.NewGuid() },
                    {"permissions", new int[] {(int)PermissionEnum.CanLoginToAdminPortal}},
                    {"superadmin", false }
                };
            var secretKey = _keyManager.GetKey("JWT_KEY");
            var token = Jose.JWT.Encode(payload, secretKey.GetContent(), JwsAlgorithm.HS256);
            return token;
        }
    }
}