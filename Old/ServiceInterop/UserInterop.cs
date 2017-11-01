using System;
using System.Linq;
using System.Threading.Tasks;
using Synthesis.GuestService.ApiWrappers;
using Synthesis.Http.Microservice;
using Synthesis.Logging;

namespace Synthesis.GuestService.Workflow.ServiceInterop
{
    public class UserInterop : BaseInterop, IUserInterop
    {
        public UserInterop(IServiceLocator serviceLocator, IMicroserviceHttpClient httpClient, ILoggerFactory loggerFactory)
            : base(httpClient, loggerFactory)
        {
            ServiceUrl = serviceLocator.UserUrl;
        }

        public async Task<UserInteropResponse> GetUserAsync(string username)
        {
            try
            {
                // TODO: Verify route
                var result = await HttpClient.GetAsync<User>($"{ServiceUrl}/v1/{username}");

                if (!IsSuccess(result))
                {
                    Logger.Warning($"Call to user service failed to retrieve a user: {result.ResponseCode}");
                    return new UserInteropResponse
                    {
                        ResponseCode = InteropResponseCode.FailRouteCall
                    };
                }

                if (result.Payload != null)
                {
                    return new UserInteropResponse
                    {
                        //SynthesisUser = Mapper.Map<User, SynthesisUserDTO>(result.Payload),
                        ResponseCode = InteropResponseCode.Success
                    };
                }

                Logger.Warning("No user records found for the given username");
                return new UserInteropResponse
                {
                    ResponseCode = InteropResponseCode.NoRecordsReturned
                };
            }
            catch (Exception ex)
            {
                const string message = "Exception thrown calling the user service to retrieve a user by username";
                Logger.Error(message, ex);
                return new UserInteropResponse
                {
                    ResponseCode = InteropResponseCode.FailException
                };
            }
        }

        public async Task<UserInteropResponse> IsUniqueEmail(string email)
        {
            try
            {
                // TODO: Verify route
                var result = await HttpClient.GetManyAsync<User>($"{ServiceUrl}/v1/projects/{email}");

                if (!IsSuccess(result))
                {
                    Logger.Warning($"Call to user service failed to retrieve a user: {result.ResponseCode}");
                    return new UserInteropResponse
                    {
                        ResponseCode = InteropResponseCode.FailRouteCall
                    };
                }

                if (result.Payload.Any())
                {
                    if (result.Payload.Count() > 1)
                    {
                        return new UserInteropResponse
                        {
                            IsEmailUnique = false,
                            ResponseCode = InteropResponseCode.Success
                        };
                    }

                    return new UserInteropResponse
                    {
                        IsEmailUnique = true,
                        ResponseCode = InteropResponseCode.Success
                    };
                }

                Logger.Warning("No user records found for the given username");
                return new UserInteropResponse
                {
                    ResponseCode = InteropResponseCode.NoRecordsReturned
                };
            }
            catch (Exception ex)
            {
                const string message = "Exception thrown calling the user service to verify if email is unique";
                Logger.Error(message, ex);
                return new UserInteropResponse
                {
                    ResponseCode = InteropResponseCode.FailException
                };
            }
        }

        public async Task<UserInteropResponse> IsUniqueUsername(string username)
        {
            try
            {
                var result = await HttpClient.GetManyAsync<User>($"{ServiceUrl}/v1/projects/{username}");

                if (!IsSuccess(result))
                {
                    Logger.Warning($"Call to user service failed to retrieve a user: {result.ResponseCode}");
                    return new UserInteropResponse
                    {
                        ResponseCode = InteropResponseCode.FailRouteCall
                    };
                }

                if (result.Payload.Any())
                {
                    if (result.Payload.Count() > 1)
                    {
                        return new UserInteropResponse
                        {
                            IsUsernameUnique = false,
                            ResponseCode = InteropResponseCode.Success
                        };
                    }

                    return new UserInteropResponse
                    {
                        IsUsernameUnique = true,
                        ResponseCode = InteropResponseCode.Success
                    };
                }

                Logger.Warning("No user records found for the given username");
                return new UserInteropResponse
                {
                    ResponseCode = InteropResponseCode.NoRecordsReturned
                };
            }
            catch (Exception ex)
            {
                const string message = "Exception thrown calling the user service to verify if username is unique";
                Logger.Error(message, ex);
                return new UserInteropResponse
                {
                    ResponseCode = InteropResponseCode.FailException
                };
            }
        }

        public async Task<UserInteropResponse> ProvisionGuestUser(User user)
        {
            try
            {
                var result = await HttpClient.PostAsync($"{ServiceUrl}/v1/users", user);

                if (!IsSuccess(result))
                {
                    Logger.Warning($"Call to user service failed to create and provision a user: {result.ResponseCode}");
                    return new UserInteropResponse
                    {
                        ResponseCode = InteropResponseCode.FailRouteCall
                    };
                }

                return new UserInteropResponse
                {
                    SynthesisUser = result.Payload,
                    ResponseCode = InteropResponseCode.Success
                };
            }
            catch (Exception ex)
            {
                const string message = "Exception thrown calling the user service to create and provision a new user";
                Logger.Error(message, ex);
                return new UserInteropResponse
                {
                    ResponseCode = InteropResponseCode.FailException
                };
            }
        }
    }
}