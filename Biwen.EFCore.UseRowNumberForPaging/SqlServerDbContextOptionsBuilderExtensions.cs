// Licensed to the Biwen.EFCore.UseRowNumberForPaging under one or more agreements.
// The Biwen.EFCore.UseRowNumberForPaging licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
        ((IRelationalDbContextOptionsBuilderInfrastructure)optionsBuilder).OptionsBuilder
            .ReplaceService<IQueryTranslationPostprocessorFactory, SqlServer2008QueryTranslationPostprocessorFactory>();
        return optionsBuilder;
    }
}
