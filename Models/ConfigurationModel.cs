using System.Collections.Generic;
using System.Web.Mvc;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc;

namespace Nop.Plugin.Payments.ZarinPal.Models
{
    public class ConfigurationModel : BaseNopModel
    {
        public ConfigurationModel()
        {
            AvailableCurency = new List<SelectListItem>();
        }

        public IList<SelectListItem> AvailableCurency { get; set; }

        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Plugins.Payments.ZarinPal.Fields.Currency")]
        public int CurrencyId { get; set; }

        public bool CurrencyId_OverrideForStore { get; set; }


        [NopResourceDisplayName("Plugins.Payments.ZarinPal.Fields.Description")]
        public string Description { get; set; }

        public bool Description_OverrideForStore { get; set; }


        [NopResourceDisplayName("Plugins.Payments.ZarinPal.Fields.MerchantCode")]
        public string MerchantCode { get; set; }

        public bool MerchantCode_OverrideForStore { get; set; }


        [NopResourceDisplayName("Plugins.Payments.ZarinPal.Fields.UseSsl")]
        public bool UseSsl { get; set; }

        public bool UseSsl_OverrideForStore { get; set; }

    }
}