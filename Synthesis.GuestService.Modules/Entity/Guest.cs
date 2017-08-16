using Newtonsoft.Json;
using System;

namespace Synthesis.GuestService.Modules.Entity
{
    public class Guest
    {
        //JTODO: replace the workspace fields with the correct guest fields

        [JsonProperty("id")]
        public Guid WorkspaceId { get; set; }

        public Guid ProjectId { get; set; }

        public string Name { get; set; }

        public byte[] Thumbnail { get; set; }

        public int Sort { get; set; }

        public string Background { get; set; }

        public bool? UsesSnapGrid { get; set; }

        public Guid? SnapGridId { get; set; }

        public int? WorkspaceMode { get; set; }

        public bool? ReadOnly { get; set; }

        public DateTime DateCreated { get; set; }
    }
}
