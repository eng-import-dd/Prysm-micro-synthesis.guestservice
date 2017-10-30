using System;
using System.Linq;
using System.Runtime.Serialization;

namespace Synthesis.GuestService.Workflow.Utilities
{
    [Serializable]
    [DataContract]
    public class SynthesisUserBasicDto
    {
        private string _firstName = "";
        private string _lastName = "";
        public string FullName => $"{FirstName} {LastName}";
        public string Initials => $"{FirstName.ToUpper().FirstOrDefault()}{LastName.ToUpper().FirstOrDefault()}";

        [DataMember]
        public string Email { get; set; }

        [DataMember]
        public string FirstName
        {
            get => _firstName;
            set
            {
                if (_firstName != null && _firstName != value)
                {
                    _firstName = value;

                    //OnPropertyChanged();
                    //OnPropertyChanged("FullName");
                    //OnPropertyChanged("Initials");
                }
            }
        }

        [DataMember]
        public string LastName
        {
            get => _lastName;
            set
            {
                if (_lastName != null && _lastName != value)
                {
                    _lastName = value;

                    //OnPropertyChanged();
                    //OnPropertyChanged("FullName");
                    //OnPropertyChanged("Initials");
                }
            }
        }

        [DataMember]
        public Guid UserId { get; set; }
    }
}