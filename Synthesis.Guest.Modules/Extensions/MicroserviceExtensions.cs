using System;
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

        public static void VerifySuccess(this MicroserviceResponse response, string message)
        {
            if (!response.IsSuccess())
            {
                throw new Exception(message);
            }
        }
    }
}
