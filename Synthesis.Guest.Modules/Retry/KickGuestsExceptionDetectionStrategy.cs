using System;
using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;

namespace Synthesis.GuestService.Retry
{
    public class KickGuestsExceptionDetectionStrategy : ITransientErrorDetectionStrategy
    {
        /// <inheritdoc />
        public bool IsTransient(Exception ex)
        {
            return true;    // retry on any exception
        }
    }
}
