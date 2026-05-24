using System.Data;
using IntegracjaProjekt.Data;
using IntegracjaProjekt.Models;
using Microsoft.EntityFrameworkCore;

namespace IntegracjaProjekt.Services;

public class DatabaseService
{

    public async Task SaveExpendituresAsync(List<MilitaryExpenditure> newRecords, string source)
    {
        using var context = new AppDbContext();
        using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        
        try
        {
            var existingData = await context.Expenditures.Where(e => e.DataSource == source).ToListAsync();
            if (existingData.Any())
            {
                context.Expenditures.RemoveRange(existingData);
                await context.SaveChangesAsync();
            }

            await context.Expenditures.AddRangeAsync(newRecords);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw; 
        }
    }

    public async Task<List<MilitaryExpenditure>> GetExpendituresAsync()
    {
        using var context = new AppDbContext();
        
        return await context.Expenditures
                            .OrderByDescending(e => e.Year)
                            .ThenBy(e => e.CountryCode)
                            .ToListAsync();
    }
}