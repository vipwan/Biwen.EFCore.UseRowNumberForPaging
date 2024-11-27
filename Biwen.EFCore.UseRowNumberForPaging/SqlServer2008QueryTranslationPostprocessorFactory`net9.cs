// Licensed to the Biwen.EFCore.UseRowNumberForPaging under one or more agreements.
// The Biwen.EFCore.UseRowNumberForPaging licenses this file to you under the MIT license. 
// See the LICENSE file in the project root for more information.
// Biwen.EFCore.UseRowNumberForPaging Author: 万雅虎, Github: https://github.com/vipwan
// Bring back support for UseRowNumberForPaging in EntityFrameworkCore 9.0/8.0/7.0/6.0 Use a ROW_NUMBER() in queries instead of OFFSET/FETCH. This method is backwards-compatible to SQL Server 2005.
// Modify Date: 2024-11-25 18:12:51 SqlServer2008QueryTranslationPostprocessorFactory`net9.cs

#if NET9_0

#pragma warning disable EF1001 // Internal EF Core API usage.

namespace Biwen.EFCore.UseRowNumberForPaging;

using Microsoft.EntityFrameworkCore.Query;
using System.Collections.Generic;

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
            query = new Offset2RowNumberConvertVisitor(query, RelationalDependencies.SqlExpressionFactory).Visit(query);
            return query;
        }

        /// <summary>
        /// 将 Offset 转换为 RowNumber
        /// </summary>
        /// <param name="root"></param>
        /// <param name="sqlExpressionFactory"></param>
        internal class Offset2RowNumberConvertVisitor(
            Expression root,
            ISqlExpressionFactory sqlExpressionFactory) : ExpressionVisitor
        {
            private readonly Expression root = root;
            private readonly ISqlExpressionFactory sqlExpressionFactory = sqlExpressionFactory;
            private const string SubTableName = "subTbl";
            private const string RowColumnName = "_Row_";//下标避免数据表存在字段

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
                IList<ProjectionExpression> projections = [new ProjectionExpression(rowNumberExpression, RowColumnName),];

                var subquery = new SelectExpression(
                    SubTableName,
                    oldSelect.Tables,
                    oldSelect.Predicate,
                    oldSelect.GroupBy,
                    oldSelect.Having,
                    [.. oldSelect.Projection, .. projections],
                    oldSelect.IsDistinct,
                    [],//排序已经在rowNumber中了
                    null,
                    null,
                    null,
                    null);

                //构造新的条件:
                var andLeft = sqlExpressionFactory.GreaterThan(
                    new ColumnExpression(RowColumnName, SubTableName, typeof(int), null, true),
                    oldOffset);
                var andRight = sqlExpressionFactory.LessThanOrEqual(
                    new ColumnExpression(RowColumnName, SubTableName, typeof(int), null, true),
                    sqlExpressionFactory.Add(oldOffset, oldLimit));

                var newPredicate = sqlExpressionFactory.AndAlso(andLeft, andRight);

                //新的Projection:
                var newProjections = oldSelect.Projection.Select(e =>
                {
                    if (e is { Expression: ColumnExpression col })
                    {
                        // 替换为子查询的别名
                        var newCol = new ColumnExpression(col.Name, SubTableName, col.Type, col.TypeMapping, col.IsNullable);
                        return new ProjectionExpression(newCol, e.Alias);
                    }
                    return e;
                });

                // 创建新的 SelectExpression，将子查询作为来源
                var newSelect = new SelectExpression(
                    oldSelect.Alias,
                    [subquery],//子查询
                    newPredicate,//新的条件
                    oldSelect.GroupBy,
                    oldSelect.Having,
                    [.. newProjections],//新的Projection
                    oldSelect.IsDistinct,
                    [],
                    null,
                    null,
                    null,
                    null);

                // replace ProjectionMapping 
                var pm = new ProjectionMember();
                var projectionMapping = new Dictionary<ProjectionMember, Expression>
                {
                    [pm] = oldSelect.GetProjection(new ProjectionBindingExpression(null, pm, null))
                };
                newSelect.ReplaceProjection(projectionMapping);

                return newSelect;
            }
        }
    }
}

#pragma warning restore EF1001 // Internal EF Core API usage.

#endif