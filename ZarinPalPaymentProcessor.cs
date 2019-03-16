using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Routing;
using System.Web.UI.WebControls;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Plugins;
using Nop.Plugin.Payments.ZarinPal.Controllers;
using Nop.Plugin.Payments.ZarinPal.Models;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Stores;

namespace Nop.Plugin.Payments.ZarinPal
{
    public class ZarinPalPaymentProcessor : BasePlugin, IPaymentMethod
    {

        private readonly ISettingService _settingService;
        private readonly ILocalizationService _localizationService;
        private readonly HttpContextBase _httpContext;
        private readonly IWebHelper _webHelper;
        private readonly ICurrencyService _currencyService;
        private readonly IWorkContext _workContext;
        private readonly ILanguageService _languageService;
        private readonly IOrderService _orderService;
        private readonly IStoreService _storeService;

        public ZarinPalPaymentProcessor(ISettingService settingService,
            ILocalizationService localizationService,
            HttpContextBase httpContext,
            IWebHelper webHelper, 
            ICurrencyService currencyService, 
            IWorkContext workContext1,
            ILanguageService languageService, 
            IOrderService orderService, 
            IStoreService storeService)
        {
            _settingService = settingService;
            _localizationService = localizationService;
            _httpContext = httpContext;
            _webHelper = webHelper;
            _currencyService = currencyService;
            _workContext = workContext1;
            _languageService = languageService;
            _orderService = orderService;
            _storeService = storeService;
        }

        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            string Authority;

            System.Net.ServicePointManager.Expect100Continue = false;
            var zp = new ZarinPalWebService.PaymentGatewayImplementationService();

            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var payPalStandardPaymentSettings = _settingService.LoadSetting<ZarinPalPaymentSettings>(storeScope);

            var email = processPaymentRequest.CustomValues["EMail"];
            var merchantCode = payPalStandardPaymentSettings.MerchantCode;
            var description = payPalStandardPaymentSettings.Description;
            var phonenumber = processPaymentRequest.CustomValues["Phonenumber"];
            var userSsl = payPalStandardPaymentSettings.UseSsl;
            var currencyId = payPalStandardPaymentSettings.CurrencyId;

            //processPaymentRequest.CustomValues.Clear();

            //check configurations fileds
            if (string.IsNullOrWhiteSpace(merchantCode) || string.IsNullOrWhiteSpace(description) || currencyId == 0)
            {
                result.AddError(
                    _localizationService.GetResource("Plugins.Payments.ZarinPal.ErrorOccurred"));

                result.NewPaymentStatus = PaymentStatus.Voided;
                return result;
            }

            //get base url
            var baseUrl = _webHelper.GetStoreHost(userSsl);

            //get currency for convert to target currency
            var sourceCurrency = _currencyService.GetCurrencyById(_workContext.WorkingCurrency.Id);
            var targetCurrency = _currencyService.GetCurrencyById(currencyId);

            //get converted price
            var finalPrice = _currencyService.ConvertCurrency(processPaymentRequest.OrderTotal, sourceCurrency, targetCurrency);

            if (email == null) email = "";
            if (phonenumber == null) phonenumber = "";

            //send information to bank and get status
            var status = zp.PaymentRequest(merchantCode, (int)finalPrice, 
                description, email.ToString(), phonenumber.ToString(), 
                baseUrl + "Plugins/PaymentZarinPal/Result", out Authority);

            //retuened status from bank
            if (status == 100)
                result.NewPaymentStatus = PaymentStatus.Pending;
            else
            {
                result.NewPaymentStatus = PaymentStatus.Voided;
                result.AddError(_localizationService.GetResource("Plugins.Payments.ZarinPal.ErrorOccurred"));
            }

            result.AuthorizationTransactionCode = Authority;

            return result;
        }

        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            _httpContext.Response.Clear();
            _httpContext.Server.ClearError();
            _httpContext.Session["OrderId"] = postProcessPaymentRequest.Order.Id;
            _httpContext.Session["Amount"] = postProcessPaymentRequest.Order.OrderTotal;
           
            //redirecd to bank page
            _httpContext.Response
                .Redirect("https://www.zarinpal.com/pg/StartPay/" + postProcessPaymentRequest.Order.AuthorizationTransactionCode);
        }

        public virtual int GetActiveStoreScopeConfiguration(IStoreService storeService, IWorkContext workContext)
        {
            //ensure that we have 2 (or more) stores
            if (storeService.GetAllStores().Count < 2)
                return 0;


            var storeId = workContext.CurrentCustomer.GetAttribute<int>(SystemCustomerAttributeNames.AdminAreaStoreScopeConfiguration);
            var store = storeService.GetStoreById(storeId);
            return store != null ? store.Id : 0;
        }

        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            return false;
        }

        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            return 0;
        }

        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();
            result.AddError("Capture method not supported");
            return result;
        }

        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();
            result.AddError("Refund method not supported");
            return result;
        }

        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();
            result.AddError("Void method not supported");
            return result;
        }

        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.AddError("Recurring payment not supported");
            return result;
        }

        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            var result = new CancelRecurringPaymentResult();
            result.AddError("Recurring payment not supported");
            return result;
        }

        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");

            //let's ensure that at least 5 seconds passed after order is placed
            //P.S. there's no any particular reason for that. we just do it
            if ((DateTime.UtcNow - order.CreatedOnUtc).TotalSeconds < 5)
                return false;

            return true;
        }

        public void GetConfigurationRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "PaymentZarinPal";
            routeValues = new RouteValueDictionary { { "Namespaces", "Nop.Plugin.Payments.ZarinPal.Controllers" }, { "area", null } };
        }

        public void GetPaymentInfoRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "PaymentInfo";
            controllerName = "PaymentZarinPal";
            routeValues = new RouteValueDictionary { { "Namespaces", "Nop.Plugin.Payments.ZarinPal.Controllers" }, { "area", null } };
        }

        public override void Install()
        {
            //settings
            var settings = new ZarinPalPaymentSettings()
            {
                MerchantCode = "",
                Description = "",
                UseSsl = false,
                CurrencyId = 0
            };

            //localization
            this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService, 
                "Plugins.Payments.ZarinPal.Fields.EMail", "پست الکترونیکی");
            this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,
                "Plugins.Payments.ZarinPal.Fields.Description", "توضیحات");
            this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,
                "Plugins.Payments.ZarinPal.Fields.MerchantCode", "مرچنت کد");
            this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,
                "Plugins.Payments.ZarinPal.Fields.Phonenumber", "شماره همراه");
            this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,
                "Plugins.Payments.ZarinPal.Fields.Currency", "واحد پول");

            this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,
                "Plugins.Payments.ZarinPal.Success", "تراکنش با موافقیت انجام شد");
            this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,
                "Plugins.Payments.ZarinPal.Failed", "تراکنش با موافقیت انجام نشد");
            this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,
                "Plugins.Payments.ZarinPal.Authority", "شناسه پرداخت");
            this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,
                "Plugins.Payments.ZarinPal.PaymentMethodDescription", "درگاه پرداخت زرین پال");
            this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,
                "Plugins.Payments.ZarinPal.Fields.RedirectionTip", "به درگاه زرین پال هدایت میشوید");
            this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,
                "Plugins.Payments.ZarinPal.Fields.UseSsl", "از Ssl استفاده می کنید؟");
            this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,
                "Plugins.Payments.ZarinPal.Fields.EMail.IsWrong", "لطفا، پست الکترونیکی را درست وارد کنید");
            this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,
                "Plugins.Payments.ZarinPal.Fields.Phonenumber.TypeIsNotValid", "لطفا، شماره همراه را به درستی وارد کنید");
            this.AddOrUpdatePluginLocaleResource(_localizationService, _languageService,
                "Plugins.Payments.ZarinPal.PleaseSelect", "لطفا، انتخاب کنید");
            
            _settingService.SaveSetting(settings);
            
            base.Install();
        }

        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<ZarinPalPaymentSettings>();

            //localization
            this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Payments.ZarinPal.Fields.EMail");
            this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Payments.ZarinPal.Fields.Description");
            this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Payments.ZarinPal.Fields.MerchantCode");
            this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Payments.ZarinPal.Fields.Phonenumber");
            this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Payments.ZarinPal.Fields.Currency");
            this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Payments.ZarinPal.Success");
            this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Payments.ZarinPal.Failed");
            this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Payments.ZarinPal.Authority");
            this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Payments.ZarinPal.PaymentMethodDescription");
            this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Payments.ZarinPal.RedirectionTip");
            this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Payments.ZarinPal.UseSsl");
            this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Payments.ZarinPal.Fields.EMail.IsWrong");
            this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Payments.ZarinPal.Fields.Phonenumber.TypeIsNotValid");
            this.DeletePluginLocaleResource(_localizationService, _languageService, "Plugins.Payments.ZarinPal.PleaseSelect");

            base.Uninstall();
        }

        public Type GetControllerType()
        {
            return typeof(PaymentZarinPalController);
        }

        public bool SupportCapture
        {
            get { return false; }
        }

        public bool SupportPartiallyRefund
        {
            get { return false; }
        }

        public bool SupportRefund
        {
            get { return false; }
        }

        public bool SupportVoid
        {
            get { return false; }
        }

        public RecurringPaymentType RecurringPaymentType
        {
            get { return RecurringPaymentType.NotSupported; }
        }

        public PaymentMethodType PaymentMethodType
        {
            get { return PaymentMethodType.Redirection; }
        }

        public bool SkipPaymentInfo
        {
            get { return false; }
        }

        public string PaymentMethodDescription
        {
            get { return _localizationService.GetResource("Plugins.Payments.ZarinPal.PaymentMethodDescription"); }
        }
    }
}
