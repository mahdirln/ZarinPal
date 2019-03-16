using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Xml;
using System.Xml.Serialization;
using Nop.Core;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.ZarinPal.Models;
using Nop.Plugin.Payments.ZarinPal.Validator;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Stores;
using Nop.Web.Framework.Controllers;

namespace Nop.Plugin.Payments.ZarinPal.Controllers
{
    public class PaymentZarinPalController : BasePaymentController
    {

        #region Fileds

        private readonly ILocalizationService _localizationService;
        private readonly HttpContextBase _httpContext;
        private readonly IStoreService _storeService;
        private readonly IWorkContext _workContext;
        private readonly ISettingService _settingService;
        private readonly ICurrencyService _currencyService;
        private readonly IOrderService _orderService;

        #endregion

        #region Countructor

        public PaymentZarinPalController(ILocalizationService localizationService,
            HttpContextBase httpContext,
            IStoreService storeService,
            IWorkContext workContext,
            ISettingService settingService,
            ICurrencyService currencyService,
            IOrderService orderService)
        {
            _localizationService = localizationService;
            _httpContext = httpContext;
            _storeService = storeService;
            _workContext = workContext;
            _settingService = settingService;
            _currencyService = currencyService;
            _orderService = orderService;
        }

        #endregion

        #region Methods

        public override IList<string> ValidatePaymentForm(FormCollection form)
        {
            var warnings = new List<string>();

            //validate
            var validator = new PaymentInfoValidator(_localizationService);
            var model = new PaymentInfoModel
            {
                EMail = form["EMail"],
                Phonenumber = form["Phonenumber"]
            };
            var validationResult = validator.Validate(model);
            if (!validationResult.IsValid)
                warnings.AddRange(validationResult.Errors.Select(error => error.ErrorMessage));

            return warnings;
        }

        public override ProcessPaymentRequest GetPaymentInfo(FormCollection form)
        {
            var paymentInfo = new ProcessPaymentRequest();

            paymentInfo.CustomValues.Add("EMail", form["EMail"]);
            paymentInfo.CustomValues.Add("Phonenumber", form["Phonenumber"]);

            return paymentInfo;
        }

        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure()
        {
            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var payPalStandardPaymentSettings = _settingService.LoadSetting<ZarinPalPaymentSettings>(storeScope);

            var model = new ConfigurationModel
            {
                Description = payPalStandardPaymentSettings.Description,
                MerchantCode = payPalStandardPaymentSettings.MerchantCode,
                UseSsl = payPalStandardPaymentSettings.UseSsl,
                CurrencyId = payPalStandardPaymentSettings.CurrencyId
            };

            model.ActiveStoreScopeConfiguration = storeScope;
            if (storeScope > 0)
            {
                model.Description_OverrideForStore = _settingService.SettingExists(payPalStandardPaymentSettings, x => x.Description, storeScope);
                model.MerchantCode_OverrideForStore = _settingService.SettingExists(payPalStandardPaymentSettings, x => x.MerchantCode, storeScope);
                model.UseSsl_OverrideForStore = _settingService.SettingExists(payPalStandardPaymentSettings, x => x.UseSsl, storeScope);
                model.CurrencyId_OverrideForStore = _settingService.SettingExists(payPalStandardPaymentSettings, x => x.CurrencyId, storeScope);
            }

            //initial currency list

            model.AvailableCurency.Add(new SelectListItem()
            {
                Value = 0.ToString(),
                Text = _localizationService.GetResource("Plugins.Payments.ZarinPal.PleaseSelect")
            });

            var currencies = _currencyService.GetAllCurrencies();
            foreach (var currency in currencies)
            {
                if (currency.Id == model.CurrencyId)
                    // if selected
                    model.AvailableCurency.Add(new SelectListItem()
                    {
                        Value = currency.Id.ToString(),
                        Text = currency.GetLocalized(p => p.Name),
                        Selected = true
                    });
                else
                    model.AvailableCurency.Add(new SelectListItem()
                    {
                        Value = currency.Id.ToString(),
                        Text = currency.GetLocalized(p => p.Name)
                    });
            }

            return View("~/Plugins/Payments.ZarinPal/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var zarinPalPaymentSettings = _settingService.LoadSetting<ZarinPalPaymentSettings>(storeScope);

            //save settings
            zarinPalPaymentSettings.Description = model.Description;
            zarinPalPaymentSettings.MerchantCode = model.MerchantCode;
            zarinPalPaymentSettings.UseSsl = model.UseSsl;
            zarinPalPaymentSettings.CurrencyId = model.CurrencyId;

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */
            _settingService.SaveSettingOverridablePerStore(zarinPalPaymentSettings, x => x.Description, 
                model.Description_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(zarinPalPaymentSettings, x => x.MerchantCode, 
                model.MerchantCode_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(zarinPalPaymentSettings, x => x.UseSsl,
                model.UseSsl_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(zarinPalPaymentSettings, x => x.CurrencyId,
                model.CurrencyId_OverrideForStore, storeScope, false);

            //now clear settings cache
            _settingService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }

        [ChildActionOnly]
        public ActionResult PaymentInfo()
        {
            return View("~/Plugins/Payments.ZarinPal/Views/PaymentInfo.cshtml");
        }

        public ActionResult Result()
        {
            if (_httpContext.Request.QueryString["Status"] != ""
                && _httpContext.Request.QueryString["Status"] != null
                && _httpContext.Request.QueryString["Authority"] != ""
                && _httpContext.Request.QueryString["Authority"] != null)
            {
                if (_httpContext.Request.QueryString["Status"].Equals("OK"))
                {
                    decimal amount = (decimal)_httpContext.Session["Amount"];
                    long refId;

                    System.Net.ServicePointManager.Expect100Continue = false;
                    var zp = new ZarinPalWebService.PaymentGatewayImplementationService();

                    //load settings for a chosen store scope
                    var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
                    var zarinPalPaymentSettings = _settingService.LoadSetting<ZarinPalPaymentSettings>(storeScope);

                    //get currency for convert to target currency
                    var sourceCurrency = _currencyService.GetCurrencyById(_workContext.WorkingCurrency.Id);
                    var targetCurrency = _currencyService.GetCurrencyById(zarinPalPaymentSettings.CurrencyId);

                    //get converted price
                    var finalPrice = _currencyService.ConvertCurrency(amount, sourceCurrency, targetCurrency);

                    //payment verification
                    var status = zp.PaymentVerification(zarinPalPaymentSettings.MerchantCode,
                        _httpContext.Request.QueryString["Authority"], (int)finalPrice, out refId);

                    // check payment status
                    if (status == 100)
                    {
                        var orderId = _httpContext.Session["OrderId"];

                        var order = _orderService.GetOrderById((int)orderId);
                        order.PaymentStatus = PaymentStatus.Paid;
                        var de = order.DeserializeCustomValues();
                        de.Add("TransactionCode", refId);
                        order.CustomValuesXml = SerializeCustomValues(de);
                        _orderService.UpdateOrder(order);

                        return RedirectToAction("Completed", "Checkout", new { orderId });
                    }
                }
            }

            return View("~/Plugins/Payments.ZarinPal/Views/Result.cshtml");
        }

        #endregion

        #region Ultimate

        public static string SerializeCustomValues(Dictionary<string, object> customValueXml)
        {
            if (customValueXml == null)
                throw new ArgumentNullException("customValueXml");

            if (!customValueXml.Any())
                return null;

            var ds = new PaymentExtensions.DictionarySerializer(customValueXml);
            var xs = new XmlSerializer(typeof(PaymentExtensions.DictionarySerializer));

            using (var textWriter = new StringWriter())
            {
                using (var xmlWriter = XmlWriter.Create(textWriter))
                {
                    xs.Serialize(xmlWriter, ds);
                }
                var result = textWriter.ToString();
                return result;
            }
        }

        #endregion

    }
}
