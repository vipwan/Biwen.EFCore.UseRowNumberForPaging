# Biwen.EFCore.UseRowNumberForPaging

Bring back support for [UseRowNumberForPaging](https://docs.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.infrastructure.sqlserverdbcontextoptionsbuilder.userownumberforpaging?view=efcore-3.0) in EntityFrameworkCore 9.0/8.0/7.0/6.0

# Usage

The same as original UseRowNumberForPaging method
```c#
// using
using Biwen.EFCore.UseRowNumberForPaging;

// UseRowNumberForPaging()
optionsBuilder.UseSqlServer("connection string", i => i.UseRowNumberForPaging());

```