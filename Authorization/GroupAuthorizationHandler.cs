using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using EmployeeMvc.Models;

namespace EmployeeMvc.Authorization
{
    /// <summary>
    /// Resource-based authorization კონკრეტულ ჯგუფზე:
    ///  - Manage: ჯგუფის მფლობელი Admin, ან ჯგუფის მფლობელი Manager
    ///  - View:   ჯგუფის მფლობელი ან ჯგუფის წევრი
    /// View-სთვის Group.Members აუცილებლად უნდა იყოს ჩატვირთული (Include).
    /// </summary>
    public class GroupAuthorizationHandler : AuthorizationHandler<OperationAuthorizationRequirement, Group>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            OperationAuthorizationRequirement requirement,
            Group resource)
        {
            var user = context.User;
            if (user.Identity?.IsAuthenticated != true) return Task.CompletedTask;

            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Task.CompletedTask;

            var isOwner = resource.OwnerId == userId;

            // Admin access is limited to groups owned by the current Admin.
            if (user.IsInRole(AppRoles.Admin) && isOwner)
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            if (user.IsInRole(AppRoles.Admin)) return Task.CompletedTask;

            if (requirement.Name == GroupOperations.Manage.Name)
            {
                if (isOwner && user.IsInRole(AppRoles.Manager))
                {
                    context.Succeed(requirement);
                }
            }
            else if (requirement.Name == GroupOperations.View.Name)
            {
                if (isOwner || resource.Members.Any(m => m.UserId == userId))
                {
                    context.Succeed(requirement);
                }
            }

            return Task.CompletedTask;
        }
    }
}
