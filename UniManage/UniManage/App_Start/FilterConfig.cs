using System.Web;
using System.Web.Mvc;
using UniManage.App_Start;

namespace UniManage
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
            filters.Add(new SystemUsageLogFilter());
        }
    }
}
