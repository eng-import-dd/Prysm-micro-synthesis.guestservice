using Synthesis.Http.Configuration;

namespace Synthesis.GuestService
{
    public class HttpClientConfiguration : IHttpClientConfiguration
    {
        /// <inheritdoc />
        public bool TrustAllCerts => true;
    }
}
