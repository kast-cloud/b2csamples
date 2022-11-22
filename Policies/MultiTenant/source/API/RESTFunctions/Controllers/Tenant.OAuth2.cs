﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using RESTFunctions.Models;

namespace RESTFunctions.Controllers
{
    // This partial class contains methods invoked with a delegated user token
    [Route("[controller]/oauth2")]
    public partial class Tenant : ControllerBase
    {
        // Get user tenants
        [Authorize(Roles = "Tenant.admin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> Get()
        {
            var id = User.FindFirst("appTenantId").Value;
            Guid guid;
            if (!Guid.TryParse(id, out guid))
                return BadRequest("Invalid id");
            var http = await _graph.CreateClient();
            try
            {
                var json = await http.GetStringAsync($"groups/{id}");
                var result = JObject.Parse(json);
                var tenant = new TenantDetails()
                {
                    id = id,
                    name = result["displayName"].Value<string>(),
                    description = result["description"].Value<string>(),
                };
                await _ext.GetAsync(tenant);
                return new JsonResult(tenant);
            }
            catch (HttpRequestException)
            {
                return NotFound();
            }
        }
 
        // POST api/values
        [HttpPut]
        [Authorize(Roles = "Tenant.admin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> Put([FromBody] TenantDetails tenant)
        {
            using (_logger.BeginScope("PUT tenant"))
            {
                var tenantId = User.FindFirstValue("appTenantId");
                if (tenantId == null) return null;
                if (string.IsNullOrEmpty(tenant.name))
                    return BadRequest("Invalid parameters");
                tenant.id = tenantId;
                var groupUrl = $"groups/{tenantId}";
                var groupData = new
                {
                    description = tenant.description,
                    mailNickname = tenant.name,
                    displayName = tenant.name.ToUpper()
                };
                var req = new HttpRequestMessage(HttpMethod.Patch, groupUrl)
                {
                    Content = new StringContent(JObject.FromObject(groupData).ToString(), Encoding.UTF8, "application/json")
                };
                var http = await _graph.CreateClient();
                var resp = await http.SendAsync(req);
                if (!resp.IsSuccessStatusCode)
                    return BadRequest("Update failed");
                if (!(await _ext.UpdateAsync(tenant)))
                    return BadRequest("Update of extension attributes failed");
                return new OkObjectResult(new { tenantId, name = tenant.name });
            }
        }
        [HttpPost("invite")]
        [Authorize(Roles = "Tenant.admin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<string> Invite([FromBody] InvitationDetails invite)
        {
            return await _inviter.GetInvitationUrl(User, invite);
        }

        [Authorize( AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        //[HttpGet("members/{tenantId}")] //TODO: tenantId may not go first as that would prevent ecluding this path from client cert requirement
        [HttpGet("members")]
        public async Task<IActionResult> GetMembers()
        {
            _logger.LogInformation("Tenant:GetMembers");
            var tenantId = User.FindFirstValue("appTenantId");
            if (tenantId == null) return null;
            _logger.LogInformation($"Tenant:GetMembers: {tenantId}");
            var result = new List<Member>();
            var appId = User.FindFirstValue("azp");
            var http = await _graph.CreateClient();
            foreach (var role in new string[] { "Tenant.admin", "Tenant.member" })
            {
                var entType = (role == "Tenant.admin") ? "owners" : "members";
                var json = await http.GetStringAsync($"groups/{tenantId}/{entType}");
                foreach (var memb in JObject.Parse(json)["value"].Value<JArray>())
                {
                    var user = result.FirstOrDefault(m => m.userId == memb["id"].Value<string>());
                    if (user != null) // already exists; can only be because already owner; add member role
                        user.roles.Add("Tenant.member");
                    else
                    {
                        var userId = memb["id"].Value<string>();
                        var appRoles = await GetAppRoles(appId, userId);
                        user = new Member()
                        {
                            tenantId = tenantId,
                            userId = userId,
                            roles = appRoles.Concat(new List<string>() { role }).ToList()
                        };
                        var userJson = await http.GetStringAsync($"users/{user.userId}?$select=displayName,identities,otherMails");
                        var userObj = JObject.Parse(userJson);
                        user.name = userObj["displayName"].Value<string>();
                        var otherMails = (JArray)userObj["otherMails"];
                        if (otherMails.Count > 0)
                            user.email = otherMails[0].Value<string>();
                        else
                        {
                            try
                            {
                                user.email = ((JArray)userObj["identities"]).First((i) => i["signInType"].Value<string>() == "emailAddress")["issuerAssignedId"].Value<string>();
                            } catch
                            {
                                user.email = "unable to parse";
                            }
                        }
                        result.Add(user);
                    }
                }
            }
            return new JsonResult(result);
        }
    }

    /* public class TenantDef
     {
         public string name { get; set; }
         public string description { get; set; }
         public string ownerId { get; set; }
         public bool requireMFA { get; set; }
         public string identityProvider { get; set; }
         public string tenantId { get; set; }
     } */
}
