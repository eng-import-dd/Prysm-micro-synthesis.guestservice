using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Synthesis.GuestService.Models
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum GuestState
    {
        InLobby = 0,
        InProject = 1,
        Ended = 2
    }
}