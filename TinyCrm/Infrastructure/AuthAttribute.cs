using System.Web.Mvc;

namespace TinyCrm.Infrastructure
{
    public class AuthAttribute : FilterAttribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationContext filterContext)
        {
            var controller = filterContext.ActionDescriptor.ControllerDescriptor.ControllerName;

            if (controller == "Account") return;

            var session = filterContext.HttpContext.Session;
            if (session == null || session["UserId"] == null)
            {
                var url = new UrlHelper(filterContext.RequestContext);
                var loginUrl = url.Action("Login", "Account", new { returnUrl = filterContext.HttpContext.Request.RawUrl });
                filterContext.Result = new RedirectResult(loginUrl);
            }
        }
    }
}