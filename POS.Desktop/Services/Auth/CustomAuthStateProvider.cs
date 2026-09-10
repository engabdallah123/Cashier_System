using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using System.Text.Json;

namespace POS.Desktop.Services.Auth
{
    public class CustomAuthStateProvider : AuthenticationStateProvider
    {
        private string? _token;
        private string? _userFullName;
        private string? _userRole;
        private Guid _userId;

        public string? Token => _token;
        public string? UserFullName => _userFullName;
        public string? UserRole => _userRole;
        public Guid UserId => _userId;

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            if (string.IsNullOrEmpty(_token))
            {
                var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
                return Task.FromResult(new AuthenticationState(anonymous));
            }

            var claims = ParseClaimsFromJwt(_token);
            var identity = new ClaimsIdentity(claims, "jwt", ClaimTypes.Name, ClaimTypes.Role);
            var user = new ClaimsPrincipal(identity);
            return Task.FromResult(new AuthenticationState(user));
        }

        public void MarkUserAsAuthenticated(string token, string fullName, string role, Guid userId)
        {
            _token = token;
            _userFullName = fullName;
            _userRole = role;
            _userId = userId;

            var claims = ParseClaimsFromJwt(token);
            var identity = new ClaimsIdentity(claims, "jwt", ClaimTypes.Name, ClaimTypes.Role);
            var user = new ClaimsPrincipal(identity);

            // Extract role from claims if passed role is null or empty
            var roleClaim = claims.FirstOrDefault(c => c.Type == ClaimTypes.Role || c.Type == "role");
            if (roleClaim != null && !string.IsNullOrEmpty(roleClaim.Value))
            {
                _userRole = roleClaim.Value;
            }

            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
        }

        public void MarkUserAsLoggedOut()
        {
            _token = null;
            _userFullName = null;
            _userRole = null;
            _userId = Guid.Empty;

            var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(anonymous)));
        }

        private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
        {
            var claims = new List<Claim>();
            try
            {
                var parts = jwt.Split('.');
                if (parts.Length < 2) return claims;

                var payload = parts[1];
                var jsonBytes = ParseBase64WithoutPadding(payload);
                var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);

                if (keyValuePairs is null) return claims;
                

                foreach (var kvp in keyValuePairs)
                {
                    var claimType = kvp.Key;
                    if (claimType.Equals("role", StringComparison.OrdinalIgnoreCase) || claimType.Equals("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", StringComparison.OrdinalIgnoreCase))
                    {
                        claimType = ClaimTypes.Role;
                    }
                    else if (claimType.Equals("name", StringComparison.OrdinalIgnoreCase) || claimType.Equals("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name", StringComparison.OrdinalIgnoreCase))
                    {
                        claimType = ClaimTypes.Name;
                    }
                    else if (claimType.Equals("sub", StringComparison.OrdinalIgnoreCase) || claimType.Equals("nameid", StringComparison.OrdinalIgnoreCase) || claimType.Equals("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", StringComparison.OrdinalIgnoreCase))
                    {
                        claimType = ClaimTypes.NameIdentifier;
                    }

                    if (kvp.Value is JsonElement element && element.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in element.EnumerateArray())
                        {
                            claims.Add(new Claim(claimType, item.ToString()));
                        }
                    }
                    else
                    {
                        claims.Add(new Claim(claimType, kvp.Value?.ToString() ?? string.Empty));
                    }
                }
            }
            catch
            {
                // Fallback gracefully without breaking authentication
            }

            return claims;
        }

        private static byte[] ParseBase64WithoutPadding(string base64)
        {
            base64 = base64.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            return Convert.FromBase64String(base64);
        }
    }
}
