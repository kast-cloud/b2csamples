using System.Collections.Generic;

namespace RESTFunctions.Models
{
    public class InvitationDetails
    {
        public string inviteEmail { get; set; }
        public Dictionary<string, string> additionalClaims { get; set; }
    }
}
