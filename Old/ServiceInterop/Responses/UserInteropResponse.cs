using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synthesis.GuestService.Workflow.ServiceInterop
{
    public class UserInteropResponse : BaseInteropResponse
    {
        public User SynthesisUser { get; set; }
        public bool IsEmailUnique { get; set; }
        public bool IsUsernameUnique { get; set; }
        public ProvisionGuestUserReturnCode ProvisionReturnCode { get; set; }
    }
}
