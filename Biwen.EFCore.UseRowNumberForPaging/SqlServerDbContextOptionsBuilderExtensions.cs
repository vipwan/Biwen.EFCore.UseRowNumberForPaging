// Licensed to the Biwen.EFCore.UseRowNumberForPaging under one or more agreements.
// The Biwen.EFCore.UseRowNumberForPaging licenses this file to you under the MIT license. 
// See the LICENSE file in the project root for more information.
// Biwen.EFCore.UseRowNumberForPaging Author: 万雅虎, Github: https://github.com/vipwan
// Bring back support for UseRowNumberForPaging in EntityFrameworkCore 9.0/8.0 Use a ROW_NUMBER() in queries instead of OFFSET/FETCH. This method is backwards-compatible to SQL Server 2005.
// Modify Date: 2024-11-15 14:42:12 SqlServerDbContextOptionsBuilderExtensions.cs

using Microsoft.EntityFrameworkCore;

namespace Biwen.EFCore.UseRowNumberForPaging;

public static class SqlServerDbContextOptionsBuilderExtensions
{
    /// <summary>
    /// UseRowNumberForPaging
    /// </summary>
    /// <param name="optionsBuilder"></param>
    /// <returns></returns>
    public static SqlServerDbContextOptionsBuilder UseRowNumberForPaging(this SqlServerDbContextOptionsBuilder optionsBuilder)
    {
        //fix Include() query
        // UseRowNumberForPaging must be used with UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
        optionsBuilder.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);

        ((IRelationalDbContextOptionsBuilderInfrastructure)optionsBuilder).OptionsBuilder
            .ReplaceService<IQueryTranslationPostprocessorFactory, SqlServer2008QueryTranslationPostprocessorFactory>();

        return optionsBuilder;
    }
}
