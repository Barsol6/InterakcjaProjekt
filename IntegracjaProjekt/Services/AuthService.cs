using IntegracjaProjekt.Data;
using IntegracjaProjekt.Models;

namespace IntegracjaProjekt.Services;

public class AuthService
{
    public static User? CurrentUser { get; private set; }

    public bool Login(string username, string password)
    {
        using var context = new AppDbContext();
        
        var user = context.Users.SingleOrDefault(u => u.Username == username && u.Password == password);
        
        if (user != null)
        {
            CurrentUser = user; 
            return true;
        }
        return false;
    }

    public void Logout()
    {
        CurrentUser = null;
    }

    public static bool IsAdmin()
    {
        return CurrentUser != null && CurrentUser.Role == "Admin";
    }
}