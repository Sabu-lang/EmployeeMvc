using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using EmployeeMvc.Models;

namespace EmployeeMvc.Authorization
{






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
