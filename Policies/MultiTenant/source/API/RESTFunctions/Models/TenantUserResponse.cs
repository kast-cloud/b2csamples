using System.Collections.Generic;

namespace RESTFunctions.Models
{
    public class TenantUserResponse
    {
        public string tenantId { get; set; }
        public string tenantName { get; set; }
        public IEnumerable<string> roles { get; set; }
        public IEnumerable<string> allTenants { get; set; }
        public bool requireMFA { get; set; }
        public bool newUser { get; set; }
    }
}
