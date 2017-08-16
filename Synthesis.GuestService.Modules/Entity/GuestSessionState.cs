using Newtonsoft.Json;
using System;

namespace Synthesis.GuestService.Modules.Entity
{
    public class GuestSessionState
    {
        [JsonProperty("id")]
        public Guid GuestSessionStateId { get; set; }

        public string Name { get; set; }
    }
}
