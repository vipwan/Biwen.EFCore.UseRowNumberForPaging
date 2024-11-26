using Biwen.EFCore.UseRowNumberForPaging;
using Microsoft.EntityFrameworkCore;

namespace TestProject1;

public class MyDbContext : DbContext
{
    public DbSet<User> Users { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer(
            @"Server=(localdb)\mssqllocaldb;Database=Users;Integrated Security=True",
            o =>
            {
                o.UseRowNumberForPaging();
            });
    }
}
