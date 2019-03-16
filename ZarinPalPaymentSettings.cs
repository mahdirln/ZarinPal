using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.ZarinPal
{
    public class ZarinPalPaymentSettings : ISettings
    {

        public string Description { get; set; }

        public string MerchantCode { get; set; }

        public bool UseSsl { get; set; }

        public int CurrencyId { get; set; }

    }
}
