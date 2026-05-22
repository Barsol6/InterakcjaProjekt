using IntegracjaProjekt.Models;
using Microsoft.EntityFrameworkCore;

namespace IntegracjaProjekt.Data;

public class AppDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<MilitaryExpenditure> Expenditures { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=eurostat.db");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasData(
            new User 
            { 
                Id = 1, 
                Username = "admin", 
                Password = "123", 
                Role = "Admin" 
            }
        );
    }
}