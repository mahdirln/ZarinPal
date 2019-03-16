using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentValidation;
using Nop.Plugin.Payments.ZarinPal.Models;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.Payments.ZarinPal.Validator
{
    public class PaymentInfoValidator : BaseNopValidator<PaymentInfoModel>
    {
        public PaymentInfoValidator(ILocalizationService localizationService)
        {
            string _emailPattern =
                "^[_A-Za-z0-9-\\+]+(\\.[_A-Za-z0-9-]+)*@"
                + "[A-Za-z0-9-]+(\\.[A-Za-z0-9]+)*(\\.[A-Za-z]{2,})$";

            //RuleFor(x => x.EMail).Matches(_emailPattern)
            //    .WithMessage(localizationService.GetResource("Plugins.Payments.ZarinPal.Fields.EMail.IsWrong"));

            RuleFor(p => p.EMail)
                .EmailAddress()
                .WithMessage(localizationService.GetResource("Plugins.Payments.ZarinPal.Fields.EMail.IsWrong"))
                .When(CheckEmptyEmail);

            RuleFor(p => p.Phonenumber)
                .Must(CheckPhonenumberType)
                .WithMessage(localizationService.GetResource("Plugins.Payments.ZarinPal.Fields.Phonenumber.TypeIsNotValid"));

        }

        private bool CheckEmptyEmail(PaymentInfoModel model)
        {
            return !string.IsNullOrEmpty(model.EMail);
        }

        private bool CheckPhonenumberType(string phonenumber)
        {
            if (phonenumber.Length == 0)
                return true;

            try
            {
                var temp = Convert.ToDouble(phonenumber);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
