using Eshop.Constants;
using Hangfire.Dashboard;

namespace Eshop.Security
{
    public sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            var httpContext = context.GetHttpContext();

            return httpContext.User.Identity?.IsAuthenticated == true &&
                   httpContext.User.IsInRole(RoleNames.Admin);
        }
    }
}
