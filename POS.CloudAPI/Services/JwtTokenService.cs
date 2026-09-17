using Microsoft.IdentityModel.Tokens;
using POS.CloudAPI.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace POS.CloudAPI.Services
{
    public interface IJwtTokenService
    {
        string GenerateToken(CloudUser user, Tenant tenant);
    }

    public class JwtTokenService : IJwtTokenService
    {
        private readonly IConfiguration _configuration;

        public JwtTokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateToken(CloudUser user, Tenant tenant)
        {
            var secretKey = _configuration["JWT:SecretKey"] ?? "Super-Secret-Online-Cloud-Key-For-POS-Mobile-2026-Supermarket";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim("FullName", user.FullName),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("TenantId", tenant.Id.ToString()),
                new Claim("TenantName", tenant.Name),
                new Claim("TenantCode", tenant.Code),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["JWT:Issuer"] ?? "POS.CloudAPI",
                audience: _configuration["JWT:Audience"] ?? "POS.MobileApp",
                claims: claims,
                expires: DateTime.UtcNow.AddDays(30), // 30 days for mobile owner convenience
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
