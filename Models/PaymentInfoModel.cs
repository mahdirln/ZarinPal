using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc;

namespace Nop.Plugin.Payments.ZarinPal.Models
{
    public class PaymentInfoModel : BaseNopModel
    {

        [NopResourceDisplayName("Plugins.Payments.ZarinPal.Fields.EMail")]
        public string EMail { get; set; }

        [NopResourceDisplayName("Plugins.Payments.ZarinPal.Fields.Phonenumber")]
        public string Phonenumber { get; set; }

    }
}
