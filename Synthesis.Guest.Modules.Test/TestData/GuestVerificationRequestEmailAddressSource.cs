using System;
using System.Collections;
using System.Collections.Generic;
using Synthesis.EmailService.InternalApi.TestData;
using Synthesis.GuestService.InternalApi.Requests;

namespace Synthesis.GuestService.Modules.Test.TestData
{
    public class GuestVerificationRequestEmailAddressGenerator : IEnumerable<object[]>
    {
        private readonly List<object[]> _requestData;

        public GuestVerificationRequestEmailAddressGenerator()
        {
            _requestData = new List<object[]>();

            var emailAddresses = new EmailAddressSource();
            foreach (var emailAddress in emailAddresses)
            {
                var request = new object[] {
                    new GuestVerificationRequest
                    {
                        ProjectAccessCode = "9F4AC529-8860-42CB-ACFB-722BB668BC06",     // needs to be a constant access code value
                        ProjectId = Guid.NewGuid(),
                        Username = emailAddress.ToString()
                    }
                };

                _requestData.Add(request);
            }
        }

        public IEnumerator<object[]> GetEnumerator() => _requestData.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
