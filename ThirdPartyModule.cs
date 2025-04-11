using Kzone.ViewDto;
using KzoneSyncService.Application.Interfaces.Services;
using KzoneSyncService.Infrastructure.Extensions;
using Nancy;

namespace KzoneSyncService.Presentation.WebApi
{
    public class ThirdPartyModule : NancyModule
    {
        private readonly ILicenseManagerService _licenseManager;
        public ThirdPartyModule(ILicenseManagerService licenseManager)
        {
            _licenseManager = licenseManager;
            Get["/api/thirdparty/license"] = parameters =>
            {
                var license = _licenseManager.GetViewLicenseDto();
                return Response.AsJson(license.WithHmac());
            };
        }
    }
}
