using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using TinyCrm.Models.Repositories;

namespace TinyCrm
{
    public class MvcApplication : HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            DataStore.Seed();
        }

        protected void Session_Start(object sender, System.EventArgs e)
        {
            // ensure session exists
        }

        protected void Application_Error(object sender, System.EventArgs e)
        {
            var ex = Server.GetLastError();
            System.IO.File.AppendAllText(
                System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data/errors.log"),
                System.DateTime.Now.ToString("u") + "  " + ex.GetType().Name + ": " + ex.Message + "\r\n" + ex.StackTrace + "\r\n\r\n");
        }
    }
}