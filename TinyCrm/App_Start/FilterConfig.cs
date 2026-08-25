using System.Web.Mvc;

namespace TinyCrm
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
            filters.Add(new TinyCrm.Infrastructure.AuthAttribute());
        }
    }
}