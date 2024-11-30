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

# Note

自`2.2.0`后不再兼容NET5/6/7,如需早期兼容请使用`1.0.0`!

