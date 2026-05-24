using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IntegracjaProjekt.Data;
using Microsoft.IdentityModel.Tokens;

namespace IntegracjaProjekt.Services;

public class AuthService
{
    private const string SecretKey = "dwstgfkijop;78y7yuracvqtrgwdrsqw3es2aeq2!";

    public string? LoginAndGetToken(string username, string password)
    {
        using var context = new AppDbContext();
        var user = context.Users.SingleOrDefault(u => u.Username == username && u.Password == password);
        
        if (user == null) return null;

        return GenerateJwtToken(user.Username, user.Role);
    }

    private string GenerateJwtToken(string username, string role)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, username),
            new Claim(ClaimTypes.Role, role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(1), 
            SigningCredentials = creds
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }



    public bool IsAdminFromToken(string token)
    {
        return GetRoleFromToken(token) == "Admin";
    }

    public string GetUsernameFromToken(string token)
    {
        return GetClaimValue(token, "nameid") 
               ?? GetClaimValue(token, ClaimTypes.NameIdentifier) 
               ?? "Nieznany";
    }
    
    public string GetRoleFromToken(string token)
    {
        return GetClaimValue(token, "role") 
               ?? GetClaimValue(token, ClaimTypes.Role) 
               ?? "User";
    }

    private string? GetClaimValue(string token, string claimType)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            if (!tokenHandler.CanReadToken(token)) return null;
            
            var jwtToken = tokenHandler.ReadJwtToken(token);
            return jwtToken.Claims.FirstOrDefault(c => c.Type == claimType)?.Value;
        }
        catch
        {
            return null;
        }
    }
}