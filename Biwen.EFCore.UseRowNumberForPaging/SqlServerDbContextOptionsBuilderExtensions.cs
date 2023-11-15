namespace EntityFrameworkCore.UseRowNumberForPaging
{
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
}
