using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace EmployeeMvc.Authorization
{
    public static class GroupOperations
    {
        public static readonly OperationAuthorizationRequirement View = new() { Name = "View" };
        public static readonly OperationAuthorizationRequirement Manage = new() { Name = "Manage" };
    }
}
