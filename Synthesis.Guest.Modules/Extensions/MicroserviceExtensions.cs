using System;
using System.Text;
using Synthesis.Http.Microservice;

namespace Synthesis.GuestService.Extensions
{
    public static class MicroserviceExtensions
    {
        public static bool IsSuccess(this MicroserviceResponse response)
        {
            if (response == null)
            {
                return false;
            }

            var code = response.ResponseCode;
            return (int)code >= 200
                   && (int)code <= 299;
        }

        /// <summary>
        /// Formats a message using the properties of a <see cref="MicroserviceResponse"/> instance.
        /// </summary>
        /// <param name="response">The <see cref="MicroserviceResponse"/>.</param>
        /// <param name="additionalMessage">An additional message.</param>
        /// <returns>The formatted message to be used as the value of <see cref="ServiceResult{T}.Message"/>.</returns>
        public static string GetServiceResultMessage(this MicroserviceResponse response, string additionalMessage = "")
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"ResponseCode\": \"{response.ResponseCode}\",");
            if (!string.IsNullOrWhiteSpace(response.ReasonPhrase))
            {
                sb.Append($"\"ReasonPhrase\": \"{response.ReasonPhrase}\",");
            }

            if (!string.IsNullOrWhiteSpace(additionalMessage))
            {
                sb.Append($"\"Message\": \"{additionalMessage}\",");
            }

            sb.Remove(sb.Length - 1, 1);
            sb.Append("}");
            return sb.ToString();
        }
    }
}
