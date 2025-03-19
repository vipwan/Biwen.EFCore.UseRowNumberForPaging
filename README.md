# Biwen.EFCore.UseRowNumberForPaging

Bring back support for [UseRowNumberForPaging](https://docs.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.infrastructure.sqlserverdbcontextoptionsbuilder.userownumberforpaging?view=efcore-3.0) in EntityFrameworkCore 9.0/8.0/7.0/6.0

- for EFCore 8.0/9.0 using `2.0.0`+
- for EFCore 6.0/7.0/8.0 using `1.0.0`

# Usage

The same as original UseRowNumberForPaging method
```c#
// using
using Biwen.EFCore.UseRowNumberForPaging;

// UseRowNumberForPaging()
optionsBuilder.UseSqlServer("connection string", o => o.UseRowNumberForPaging());

```

# Sample

```c#
//select & group by
var iquery = from e in dbContext.Users
                where e.CreatedDate < DateTime.Now
                group e by e.Email into gg
                select new
                {
                    Email = gg.Key,
                    Count = gg.Count()
                };
//having
iquery = iquery.Where(x => x.Count > 1);
var list = iquery.OrderByDescending(x => x.Count).Skip(10).Take(20);
```

Generated SQL

```sql
DECLARE @__p_0 int = 10;
DECLARE @__p_1 int = 20;

SELECT [t].[Email], [t].[Count]
FROM (
    SELECT [u].[Email], COUNT(*) AS [Count], ROW_NUMBER() OVER(ORDER BY COUNT(*) DESC) AS [_Row_]
    FROM [Users] AS [u]
    WHERE [u].[CreatedDate] < GETDATE()
    GROUP BY [u].[Email]
    HAVING COUNT(*) > 1
) AS [t]
WHERE [t].[_Row_] > @__p_0 AND [t].[_Row_] <= @__p_0 + @__p_1

```



# Note

自`2.2.0`后不再兼容NET5/6/7,如需早期兼容请使用`1.0.0`!

