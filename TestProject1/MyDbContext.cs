using Biwen.EFCore.UseRowNumberForPaging;
using Microsoft.EntityFrameworkCore;

namespace TestProject1;

public class MyDbContext : DbContext
{

    public DbSet<User> Users { get; set; } = null!;


    public DbSet<Hobby> Hobbies { get; set; } = null!;



    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // debug sql
        optionsBuilder.LogTo(Console.WriteLine);

        optionsBuilder.UseSqlServer(
            @"Server=(localdb)\mssqllocaldb;Database=Users;Integrated Security=True",
            o =>
            {
                o.UseRowNumberForPaging();
            });
    }
}
