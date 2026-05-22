using IntegracjaProjekt.Data;
using IntegracjaProjekt.UI;

namespace IntegracjaProjekt;

class Program
{
    static async Task Main(string[] args)
    {
        using (var context = new AppDbContext())
        {
            context.Database.EnsureCreated();
        }

        var uiManager = new UiManager();
        await uiManager.StartAsync();
    }
}