using System;
using System.Linq;
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
        /// <returns>The formatted message to be used to describe the results in a <see cref="MicroserviceResponse"/>"/>.</returns>
        public static string GetMicroserviceResponseResultMessage(this MicroserviceResponse response, string additionalMessage = "")
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

            if (response.ErrorResponse != null)
            {
                sb.Append("\"ErrorResponse\": {");
                if (!string.IsNullOrWhiteSpace(response.ErrorResponse.Code))
                {
                    sb.Append($"\"Code\": \"{response.ErrorResponse.Code}\",");
                }
                if (!string.IsNullOrWhiteSpace(response.ErrorResponse.Message))
                {
                    sb.Append($"\"Message\": \"{response.ErrorResponse.Message}\",");
                }
                if (response.ErrorResponse.Errors != null && response.ErrorResponse.Errors.Any())
                {
                    sb.Append("\"Errors\": [");
                    foreach (var propertyError in response.ErrorResponse.Errors)
                    {
                        sb.Append("{");
                        sb.Append($"\"PropertyName\": \"{propertyError.PropertyName}\",");
                        sb.Append($"\"Message\": \"{propertyError.Message}\",");
                        sb.Append($"\"ErrorCode\": \"{propertyError.ErrorCode}\"");
                        sb.Append("},");
                    }

                    sb.Remove(sb.Length - 1, 1);
                    sb.Append("]");
                }

                sb.Append("},");
            }

            sb.Remove(sb.Length - 1, 1);
            sb.Append("}");
            return sb.ToString();
        }
    }
}
