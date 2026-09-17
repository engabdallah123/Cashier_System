using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.CloudAPI.Database;
using POS.CloudAPI.DTOs;
using POS.CloudAPI.Services;
using System.Security.Claims;

namespace POS.CloudAPI.Controllers
{
    [ApiController]
    [Route("api/cloud/auth")]
    public class CloudAuthController : ControllerBase
    {
        private readonly CloudDbContext _db;
        private readonly IJwtTokenService _jwt;

        public CloudAuthController(CloudDbContext db, IJwtTokenService jwt)
        {
            _db = db;
            _jwt = jwt;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { message = "اسم المستخدم وكلمة المرور مطلوبان." });

            // Authenticate user directly by username, and load their tenant
            Entities.CloudUser? user;
            if (!string.IsNullOrWhiteSpace(req.ShopCode))
            {
                var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Code == req.ShopCode.Trim() && t.IsActive);
                if (tenant == null)
                    return BadRequest(new { message = "كود المتجر / الفرع غير صحيح." });

                user = await _db.Users.FirstOrDefaultAsync(u => u.TenantId == tenant.Id && u.Username == req.Username.Trim() && u.IsActive);
            }
            else
            {
                user = await _db.Users.FirstOrDefaultAsync(u => u.Username == req.Username.Trim() && u.IsActive);
            }

            if (user == null || !CloudDbContext.VerifyPassword(req.Password, user.PasswordHash))
            {
                return Unauthorized(new { message = "اسم المستخدم أو كلمة المرور غير صحيحة." });
            }

            var userTenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == user.TenantId && t.IsActive);
            if (userTenant == null)
            {
                return Unauthorized(new { message = "المتجر التابع له هذا الحساب غير نشط أو غير موجود." });
            }

            var token = _jwt.GenerateToken(user, userTenant);

            var response = new AuthResponseDto(
                Token: token,
                UserId: user.Id,
                FullName: user.FullName,
                Role: user.Role,
                TenantId: userTenant.Id,
                TenantName: userTenant.Name,
                TenantCode: userTenant.Code,
                ExpiresAt: DateTime.UtcNow.AddDays(30)
            );

            return Ok(response);
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetProfile()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized();

            var user = await _db.Users.FindAsync(userId);
            if (user == null) return NotFound();

            var tenant = await _db.Tenants.FindAsync(user.TenantId);

            return Ok(new
            {
                user.Id,
                user.Username,
                user.FullName,
                user.Role,
                user.Phone,
                Tenant = new
                {
                    tenant?.Id,
                    tenant?.Name,
                    tenant?.Code
                }
            });
        }
    }
}
