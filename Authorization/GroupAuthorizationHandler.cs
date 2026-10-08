using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using EmployeeMvc.Models;

namespace EmployeeMvc.Authorization
{
    /// <summary>
    /// Resource-based authorization კონკრეტულ ჯგუფზე:
    ///  - Manage: Admin, ან ჯგუფის მფლობელი, რომელსაც ამჟამადაც აქვს Manager როლი
    ///  - View:   Admin, მფლობელი, ან ჯგუფის წევრი
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

            if (user.IsInRole(AppRoles.Admin))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Task.CompletedTask;

            var isOwner = resource.OwnerId == userId;

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
