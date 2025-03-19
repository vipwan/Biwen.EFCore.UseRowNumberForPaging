// Licensed to the Biwen.EFCore.UseRowNumberForPaging under one or more agreements.
// The Biwen.EFCore.UseRowNumberForPaging licenses this file to you under the MIT license. 
// See the LICENSE file in the project root for more information.
// Biwen.EFCore.UseRowNumberForPaging Author: 万雅虎, Github: https://github.com/vipwan
// Bring back support for UseRowNumberForPaging in EntityFrameworkCore 9.0/8.0 Use a ROW_NUMBER() in queries instead of OFFSET/FETCH. This method is backwards-compatible to SQL Server 2005.
// Modify Date: 2024-11-25 18:12:51 SqlServer2008QueryTranslationPostprocessorFactory`net9.cs

#if NET9_0

#pragma warning disable EF1001 // Internal EF Core API usage.

namespace Biwen.EFCore.UseRowNumberForPaging;

using Microsoft.EntityFrameworkCore.Query;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public class SqlServer2008QueryTranslationPostprocessorFactory(
    QueryTranslationPostprocessorDependencies dependencies,
    RelationalQueryTranslationPostprocessorDependencies relationalDependencies) :
    IQueryTranslationPostprocessorFactory
{
    private readonly QueryTranslationPostprocessorDependencies _dependencies = dependencies;
    private readonly RelationalQueryTranslationPostprocessorDependencies _relationalDependencies = relationalDependencies;

    public virtual QueryTranslationPostprocessor Create(QueryCompilationContext queryCompilationContext) => new SqlServer2008QueryTranslationPostprocessor(
            _dependencies,
            _relationalDependencies,
            queryCompilationContext);

    internal class SqlServer2008QueryTranslationPostprocessor(
        QueryTranslationPostprocessorDependencies dependencies,
        RelationalQueryTranslationPostprocessorDependencies relationalDependencies,
        QueryCompilationContext queryCompilationContext) :
        RelationalQueryTranslationPostprocessor(dependencies, relationalDependencies, (RelationalQueryCompilationContext)queryCompilationContext)
    {
        public override Expression Process(Expression query)
        {
            query = base.Process(query);
            query = new Offset2RowNumberConvertVisitor(RelationalDependencies.SqlExpressionFactory).Visit(query);
            return query;
        }

        /// <summary>
        /// 将 Offset 转换为 RowNumber
        /// </summary>
        /// <param name="sqlExpressionFactory"></param>
        internal class Offset2RowNumberConvertVisitor(ISqlExpressionFactory sqlExpressionFactory) : ExpressionVisitor
        {
            private readonly ISqlExpressionFactory sqlExpressionFactory = sqlExpressionFactory;
            private const string SubTableName = "t";
            private const string RowColumnName = "_Row_";//下标避免数据表存在字段

            private static readonly FieldInfo _clientProjections = typeof(SelectExpression).GetField("_clientProjections", BindingFlags.NonPublic | BindingFlags.Instance);
            private static readonly FieldInfo _projectionMapping = typeof(SelectExpression).GetField("_projectionMapping", BindingFlags.NonPublic | BindingFlags.Instance);

            protected override Expression VisitExtension(Expression node) => node switch
            {
                ShapedQueryExpression shapedQueryExpression => shapedQueryExpression.Update(Visit(shapedQueryExpression.QueryExpression), shapedQueryExpression.ShaperExpression),
                SelectExpression se => VisitSelect(se),
                _ => base.VisitExtension(node)
            };

            private SelectExpression VisitSelect(SelectExpression selectExpression)
            {
                var oldOffset = selectExpression.Offset;
                if (oldOffset == null)
                    return selectExpression;

                var oldLimit = selectExpression.Limit;
                var oldOrderings = selectExpression.Orderings;

                var rowOrderings = oldOrderings.Any() switch
                {
                    true => oldOrderings,
                    false => [new OrderingExpression(new SqlFragmentExpression("(SELECT 1)"), true)]
                };

                var oldSelect = selectExpression;

                var rowNumberExpression = new RowNumberExpression([], rowOrderings, oldOffset.TypeMapping);
                // 创建子查询
                IList<ProjectionExpression> projection = [new ProjectionExpression(rowNumberExpression, RowColumnName),];

                var subQuery = new SelectExpression(
                    SubTableName,
                    oldSelect.Tables,
                    oldSelect.Predicate,
                    oldSelect.GroupBy,
                    oldSelect.Having,
                    [.. oldSelect.Projection, .. projection],
                    oldSelect.IsDistinct,
                    [],//排序已经在rowNumber中了
                    null,
                    null,
                    null,
                    null);

                //构造新的条件:
                //存在 Limit 时，条件为 Row > Offset AND Row <= Offset + Limit
                //不存在 Limit 时，条件为 Row > Offset
                var newPredicate = oldLimit is not null
                    ? sqlExpressionFactory.AndAlso(
                        sqlExpressionFactory.GreaterThan(
                            new ColumnExpression(RowColumnName, SubTableName, typeof(int), null, true),
                            oldOffset),
                        sqlExpressionFactory.LessThanOrEqual(
                            new ColumnExpression(RowColumnName, SubTableName, typeof(int), null, true),
                            sqlExpressionFactory.Add(oldOffset, oldLimit)))
                    : sqlExpressionFactory.GreaterThan(
                        new ColumnExpression(RowColumnName, SubTableName, typeof(int), null, true),
                        oldOffset);

                //新的Projection:
                var newProjection = oldSelect.Projection.Select(exp =>
                {
                    //将任意表达式的来源替换为子查询的字段:
                    var newCol = new ColumnExpression(exp.Alias, SubTableName, exp.Expression.Type, exp.Expression.TypeMapping, true);
                    return new ProjectionExpression(newCol, exp.Alias);
                });

                // 创建新的 SelectExpression，将子查询作为来源
                var newSelect = new SelectExpression(
                    oldSelect.Alias,
                    [subQuery],//子查询
                    newPredicate,//新的条件
                    groupBy: [],
                    having: null,
                    [.. newProjection],//新的Projection
                    oldSelect.IsDistinct,
                    [],
                    null,
                    null,
                    null,
                    null);

                // replace ProjectionMapping & ClientProjections
                _projectionMapping.SetValue(newSelect, _projectionMapping.GetValue(oldSelect));
                _clientProjections.SetValue(newSelect, _clientProjections.GetValue(oldSelect));

                return newSelect;
            }
        }
    }
}

#pragma warning restore EF1001 // Internal EF Core API usage.

#endif