using System.Collections.Generic;

namespace Synthesis.GuestService.Workflow.ServiceInterop
{
    public class ProjectInteropResponse : BaseInteropResponse
    {
        public List<Project> ProjectList { get; set; }
    }
}