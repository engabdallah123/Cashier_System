using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.CloudAPI.Database;
using POS.CloudAPI.DTOs;
using System.Security.Claims;

namespace POS.CloudAPI.Controllers
{
    [ApiController]
    [Route("api/cloud/store-settings")]
    [Authorize]
    public class CloudSettingsController : ControllerBase
    {
        private readonly CloudDbContext _db;

        public CloudSettingsController(CloudDbContext db)
        {
            _db = db;
        }

        private Guid GetTenantId()
        {
            var tenantClaim = User.FindFirstValue("TenantId");
            return Guid.TryParse(tenantClaim, out var tenantId) ? tenantId : Guid.Empty;
        }

        [HttpGet]
        public async Task<IActionResult> GetSettings()
        {
            var tenantId = GetTenantId();
            if (tenantId == Guid.Empty) return Unauthorized();

            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
            var settings = await _db.StoreSettings.FirstOrDefaultAsync(s => s.TenantId == tenantId);

            if (settings != null)
            {
                return Ok(new CloudStoreSettingsDto(
                    settings.StoreName,
                    settings.Address,
                    settings.Phone,
                    settings.TaxRate,
                    settings.IsTaxIncluded,
                    settings.Currency,
                    settings.InvoiceFooterMessage,
                    settings.AllowNegativeStock,
                    settings.LogoUrl
                ));
            }

            // Fallback if desktop hasn't pushed settings yet
            return Ok(new CloudStoreSettingsDto(
                tenant?.Name ?? "المتجر الرئيسي",
                null,
                null,
                0,
                false,
                "ج.م",
                null,
                false,
                null
            ));
        }
    }
}
