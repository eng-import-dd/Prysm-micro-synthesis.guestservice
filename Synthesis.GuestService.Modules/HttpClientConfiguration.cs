using Synthesis.Configuration;
using Synthesis.Http.Configuration;

namespace Synthesis.GuestService
{
    public class HttpClientConfiguration : IHttpClientConfiguration
    {
        private readonly IAppSettingsReader _appSettingsReader;
        public HttpClientConfiguration(IAppSettingsReader appSettingsReader)
        {
            _appSettingsReader = appSettingsReader;
        }

        /// <inheritdoc />
        public bool TrustAllCerts => _appSettingsReader.GetValue<bool>("TrustAllCertificates");
    }
}