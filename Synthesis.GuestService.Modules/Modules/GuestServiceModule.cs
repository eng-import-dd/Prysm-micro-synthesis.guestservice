using System;
using System.Threading.Tasks;
using Nancy;
using Nancy.Security;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Security;
using Synthesis.GuestService.Entity;
using Synthesis.GuestService.Exceptions;
using Synthesis.GuestService.Validators;
using Synthesis.GuestService.Workflow.Interfaces;
using Synthesis.GuestService.Workflow.Controllers;
using Synthesis.Logging;

namespace Synthesis.GuestService.Modules
{
    public class GuestServiceModule : NancyModule
    {
        private readonly IValidatorLocator _validatorLocator;
        private readonly ILoggingService _loggingService;
        private readonly IGuestInviteController _guestController;
        private readonly Serialization.IObjectSerializer _serializer;
        //private readonly IAuthorizationService _authorizationService;

        // -- GuestInvite Route Names
        private readonly string v1GuestInviteRouteName = "/v1/guestinvite";
        private readonly string apiv1GuestInviteRouteName = "/api/v1/guestinvite";

        // -- GuestSession Route Names
        private readonly string v1GuestSessionRouteName = "/v1/guestsession";
        private readonly string apiv1GuestSessionRouteName = "/api/v1/guestsession";

        // -- Guest Route Metadata Object
        
        

        #region Constructor
        public GuestServiceModule(IMetadataRegistry metadataRegistry, IValidatorLocator validatorLocator, IGuestInviteController guestController, ILoggingService loggingService, Serialization.IObjectSerializer serializer/*, IAuthorizationService authorizationService*/) :
            base("/")
        {
            _guestController = guestController;
            _validatorLocator = validatorLocator;
            _serializer = serializer;
            _loggingService = loggingService;

            //_authorizationService = AuthorizationService;
            //this.RequiresAuthentication();

            // -- Health
            SetupRoute_HealthCheck(metadataRegistry);
            // -- Create Routes
            SetupRoute_CreateGuestInvite(metadataRegistry);
            SetupRoute_CreateGuestSession(metadataRegistry);
            // -- Read Routes
            SetupRoute_GetGuestInviteById(metadataRegistry);
            SetupRoute_GetGuestInvitesByProjectId(metadataRegistry);
            SetupRoute_GetGuestInvitesbyEmail(metadataRegistry);
            SetupRoute_GetGuestSessionById(metadataRegistry);
            SetupRoute_GetGuestSessionsByProjectId(metadataRegistry);
            SetupRoute_GetGuestSessionsByUserId(metadataRegistry);
            // -- Update Routes
            SetupRoute_UpdateGuestInvite(metadataRegistry);
            SetupRoute_UpdateGuestSession(metadataRegistry);


            // add some additional data for the documentation module
            metadataRegistry.SetRouteMetadata("Hello", new SynthesisRouteMetadata()
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = "Some informational message",
                Description = "Gets a synthesis user by id."
            });
            // create a health check endpoint
            Get("/hello", (_) =>
            {
                try
                {
                /*
                    this.RequiresClaims(
                        c => c.Type == SynthesisStatelessAuthenticationConfiguration.PERMISSION_CLAIM_TYPE && GetPermission(c.Value) == PermissionEnum.CanLoginToAdminPortal);
                */
                }
                catch (Exception)
                {
                    return HttpStatusCode.Unauthorized;
                }
                // TODO: do some kind of health check if it passes return OK, otherwise 500
                return new Response()
                {
                    StatusCode = HttpStatusCode.OK,
                    ReasonPhrase = "Hello World"
                };
            }, null, "Hello");
        }
        #endregion

        #region HealthCheck Route Setup
        // -- Health Check Route
        private void SetupRoute_HealthCheck(IMetadataRegistry metadataRegistry)
        {

        }
        #endregion

        #region GuestInvite Route Setup
        // -- Create GuestInvite Route
        private void SetupRoute_CreateGuestInvite(IMetadataRegistry metadataRegistry)
        {
            var routeMetadata = new SynthesisRouteMetadata()
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Request = _serializer.SerializeToString(new GuestInvite()),
                Response = _serializer.SerializeToString(new GuestInvite()),
                Description = "Creates a new GuestInvite."
            };

            metadataRegistry.SetRouteMetadata(v1GuestInviteRouteName, routeMetadata);
            Post(v1GuestInviteRouteName + "{guestInvite:GuestInvite}", CreateGuestInvite, null, "CreateGuestInvite");

            metadataRegistry.SetRouteMetadata(apiv1GuestInviteRouteName, routeMetadata);
            Post(apiv1GuestInviteRouteName + "{guestInvite:GuestInvite}", CreateGuestInvite, null, "CreateGuestInvite");
        }

        // -- Get GuestInvite By Id Route
        private void SetupRoute_GetGuestInviteById(IMetadataRegistry metadataRegistry)
        {
            var routeMetadata = new SynthesisRouteMetadata()
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Request = _serializer.SerializeToString(new GuestInvite()),
                Response = _serializer.SerializeToString(new GuestInvite()),
                Description = "Gets a GuestInvite by passing in a GuestInviteId."
            };

            metadataRegistry.SetRouteMetadata(v1GuestInviteRouteName, routeMetadata);
            Get(v1GuestInviteRouteName + "{guestInviteId:guid}", GetGuestInviteById, null, "GetGuestInviteById");

            metadataRegistry.SetRouteMetadata(apiv1GuestInviteRouteName, routeMetadata);
            Get(apiv1GuestInviteRouteName + "{guestInviteId:guid}", GetGuestInviteById, null, "GetGuestInviteById");
        }
        // -- Get GuestInvites By Email Route
        private void SetupRoute_GetGuestInvitesbyEmail(IMetadataRegistry metadataRegistry)
        {
            var routeMetadata = new SynthesisRouteMetadata()
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Request = _serializer.SerializeToString(new GuestInvite()),
                Response = _serializer.SerializeToString(new GuestInvite()),
                Description = "Gets a list of GuestInvites by passing in a GuestEmail."
            };

            metadataRegistry.SetRouteMetadata(v1GuestInviteRouteName, routeMetadata);
            Get(v1GuestInviteRouteName + "{guestEmail:string}", GetGuestInvitesbyEmail, null, "GetGuestInvitesbyEmail");

            metadataRegistry.SetRouteMetadata(apiv1GuestInviteRouteName, routeMetadata);
            Get(apiv1GuestInviteRouteName + "{guestEmail:string}", GetGuestInvitesbyEmail, null, "GetGuestInvitesbyEmail");
        }
        // -- Get GuestInvites By ProjectId Route
        private void SetupRoute_GetGuestInvitesByProjectId(IMetadataRegistry metadataRegistry)
        {
            var routeMetadata = new SynthesisRouteMetadata()
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Request = _serializer.SerializeToString(new GuestInvite()),
                Response = _serializer.SerializeToString(new GuestInvite()),
                Description = "Gets a list of GuestInvites by passing in a ProjectId."
            };

            metadataRegistry.SetRouteMetadata(v1GuestInviteRouteName, routeMetadata);
            Get(v1GuestInviteRouteName + "{projectId:guid}", GetGuestInvitesByProjectId, null, "GetGuestInvitesByProjectId");

            metadataRegistry.SetRouteMetadata(apiv1GuestInviteRouteName, routeMetadata);
            Get(apiv1GuestInviteRouteName + "{projectId:guid}", GetGuestInvitesByProjectId, null, "GetGuestInvitesByProjectId");
        }

        // -- Update GuestInvite Route
        private void SetupRoute_UpdateGuestInvite(IMetadataRegistry metadataRegistry)
        {
            var routeMetadata = new SynthesisRouteMetadata()
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Request = _serializer.SerializeToString(new GuestInvite()),
                Response = _serializer.SerializeToString(new GuestInvite()),
                Description = "Updates a GuestInvites by passing in a GuestInvite object with the updated fields."
            };

            metadataRegistry.SetRouteMetadata(v1GuestInviteRouteName, routeMetadata);
            Put(v1GuestInviteRouteName + "{guestInvite:GuestInvite}", UpdateGuestInvite, null, "UpdateGuestInvite");

            metadataRegistry.SetRouteMetadata(apiv1GuestInviteRouteName, routeMetadata);
            Put(apiv1GuestInviteRouteName + "{guestInvite:GuestInvite}", UpdateGuestInvite, null, "UpdateGuestInvite");
        }
        #endregion

        #region GuestSession Route Setup
        // -- Create GuestSession Route
        private void SetupRoute_CreateGuestSession(IMetadataRegistry metadataRegistry)
        {
            var routeMetadata = new SynthesisRouteMetadata()
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Request = _serializer.SerializeToString(new GuestSession()),
                Response = _serializer.SerializeToString(new GuestSession()),
                Description = "Creates a new GuestSession."
            };

            metadataRegistry.SetRouteMetadata(v1GuestSessionRouteName, routeMetadata);
            Post(v1GuestSessionRouteName + "{guestSession:GuestSession}", CreateGuestSession, null, "CreateGuestSession");

            metadataRegistry.SetRouteMetadata(apiv1GuestSessionRouteName, routeMetadata);
            Post(apiv1GuestSessionRouteName + "{guestSession:GuestSession}", CreateGuestSession, null, "CreateGuestSession");
        }

        // -- Get GuestSession By Id Route
        private void SetupRoute_GetGuestSessionById(IMetadataRegistry metadataRegistry)
        {
            var routeMetadata = new SynthesisRouteMetadata()
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Request = _serializer.SerializeToString(new GuestSession()),
                Response = _serializer.SerializeToString(new GuestSession()),
                Description = "Gets a GuestSession by passing in a GuestSessionId."
            };

            metadataRegistry.SetRouteMetadata(v1GuestSessionRouteName, routeMetadata);
            Get(v1GuestSessionRouteName + "{guestSessionId:guid}", GetGuestSessionById, null, "GetGuestSessionById");

            metadataRegistry.SetRouteMetadata(apiv1GuestSessionRouteName, routeMetadata);
            Get(apiv1GuestSessionRouteName + "{guestSessionId:guid}", GetGuestSessionById, null, "GetGuestSessionById");
        }
        // -- Get GuestSessions By UserId Route
        private void SetupRoute_GetGuestSessionsByUserId(IMetadataRegistry metadataRegistry)
        {
            var routeMetadata = new SynthesisRouteMetadata()
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Request = _serializer.SerializeToString(new GuestSession()),
                Response = _serializer.SerializeToString(new GuestSession()),
                Description = "Gets a list of GuestSessions by passing in a UserId."
            };

            metadataRegistry.SetRouteMetadata(v1GuestSessionRouteName, routeMetadata);
            Get(v1GuestSessionRouteName + "{userId:guid}", GetGuestSessionsByUserId, null, "GetGuestSessionsByUserId");

            metadataRegistry.SetRouteMetadata(apiv1GuestSessionRouteName, routeMetadata);
            Get(apiv1GuestSessionRouteName + "{userId:guid}", GetGuestSessionsByUserId, null, "GetGuestSessionsByUserId");
        }
        // -- Get tGuestSessions By ProjectId Route
        private void SetupRoute_GetGuestSessionsByProjectId(IMetadataRegistry metadataRegistry)
        {
            var routeMetadata = new SynthesisRouteMetadata()
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Request = _serializer.SerializeToString(new GuestSession()),
                Response = _serializer.SerializeToString(new GuestSession()),
                Description = "Gets a list of GuestSessions by passing in a ProjectId."
            };

            metadataRegistry.SetRouteMetadata(v1GuestSessionRouteName, routeMetadata);
            Get(v1GuestSessionRouteName + "{projectId:guid}", GetGuestSessionsByProjectId, null, "GetGuestSessionsByProjectId");

            metadataRegistry.SetRouteMetadata(apiv1GuestSessionRouteName, routeMetadata);
            Get(apiv1GuestSessionRouteName + "{projectId:guid}", GetGuestSessionsByProjectId, null, "GetGuestSessionsByProjectId");
        }

        // -- Update GuestSession Route
        private void SetupRoute_UpdateGuestSession(IMetadataRegistry metadataRegistry)
        {
            var routeMetadata = new SynthesisRouteMetadata()
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Request = _serializer.SerializeToString(new GuestSession()),
                Response = _serializer.SerializeToString(new GuestSession()),
                Description = "Updates a GuestSession by passing in a GuestSession object with the updated fields."
            };

            metadataRegistry.SetRouteMetadata(v1GuestSessionRouteName, routeMetadata);
            Put(v1GuestSessionRouteName + "{guestSession:guid}", UpdateGuestSession, null, "UpdateGuestSession");

            metadataRegistry.SetRouteMetadata(apiv1GuestSessionRouteName, routeMetadata);
            Put(apiv1GuestSessionRouteName + "{guestSession:guid}", UpdateGuestSession, null, "UpdateGuestSession");
        }
        #endregion

        #region GuestInvite Route Methods
        // -- Create
        private async Task<object> CreateGuestInvite(dynamic input)
        {
            try
            {

            }
            catch (Exception e)
            {
                throw;
            }
        }

        // -- Read
        private async Task<object> GetGuestInviteById(dynamic input)
        {
            try
            {

            }
            catch (Exception e)
            {
                throw;
            }
        }
        private async Task<object> GetGuestInvitesbyEmail(dynamic input)
        {
            try
            {

            }
            catch (Exception e)
            {
                throw;
            }
        }
        private async Task<object> GetGuestInvitesByProjectId(dynamic inputy)
        {
            try
            {

            }
            catch (Exception e)
            {
                throw;
            }
        }

        // -- Update
        private async Task<object> UpdateGuestInvite(dynamic input)
        {
            try
            {

            }
            catch (Exception e)
            {
                throw;
            }
        }
        #endregion

        #region GuestSession Route Methods
        // -- Create
        private async Task<object> CreateGuestSession(dynamic input)
        {
            try
            {

            }
            catch (Exception e)
            {
                throw;
            }
        }

        // -- Read
        private async Task<object> GetGuestSessionById(dynamic inputd)
        {
            try
            {

            }
            catch (Exception e)
            {
                throw;
            }
        }
        private async Task<object> GetGuestSessionsByUserId(dynamic input)
        {
            try
            {

            }
            catch (Exception e)
            {
                throw;
            }
        }
        private async Task<object> GetGuestSessionsByProjectId(dynamic input)
        {
            try
            {

            }
            catch (Exception e)
            {
                throw;
            }
        }

        // -- Update
        private async Task<GuestSession> UpdateGuestSession(dynamic input)
        {
            try
            {

            }
            catch (Exception e)
            {
                throw;
            }
        }
        #endregion


        internal PermissionEnum GetPermission(string value)
        {
            return ((PermissionEnum)Int32.Parse(value));
        }
    }
}
