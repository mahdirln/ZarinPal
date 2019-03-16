using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.Routing;
using Nop.Web.Framework.Mvc.Routes;

namespace Nop.Plugin.Payments.ZarinPal
{
    public class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(RouteCollection routes)
        {
            //Resualt
            routes.MapRoute("Plugin.Payments.PaymentZarinPal.Result",
                 "Plugins/PaymentZarinPal/Result",
                 new { controller = "PaymentZarinPal", action = "Result" },
                 new[] { "Nop.Plugin.Payments.PaymentZarinPal.Controllers" }
            );
        }

        public int Priority
        {
            get
            {
                return 0;
            }
        }
    }
}
