// Licensed to the Biwen.EFCore.UseRowNumberForPaging under one or more agreements.
// The Biwen.EFCore.UseRowNumberForPaging licenses this file to you under the MIT license. 
// See the LICENSE file in the project root for more information.
// Biwen.EFCore.UseRowNumberForPaging Author: 万雅虎, Github: https://github.com/vipwan
// Bring back support for UseRowNumberForPaging in EntityFrameworkCore 9.0/8.0/7.0/6.0 Use a ROW_NUMBER() in queries instead of OFFSET/FETCH. This method is backwards-compatible to SQL Server 2005.
// Modify Date: 2024-11-25 18:12:51 SqlServer2008QueryTranslationPostprocessorFactory`net9.cs

#if NET9_0_OR_GREATER

#pragma warning disable EF1001 // Internal EF Core API usage.

namespace Biwen.EFCore.UseRowNumberForPaging;

using Microsoft.EntityFrameworkCore.Query;
using System.Collections.Generic;
using System.Reflection;

public class SqlServer2008QueryTranslationPostprocessorFactory(
    QueryTranslationPostprocessorDependencies dependencies,
    RelationalQueryTranslationPostprocessorDependencies relationalDependencies) : IQueryTranslationPostprocessorFactory
{
    private readonly QueryTranslationPostprocessorDependencies _dependencies = dependencies;
    private readonly RelationalQueryTranslationPostprocessorDependencies _relationalDependencies = relationalDependencies;

    public virtual QueryTranslationPostprocessor Create(QueryCompilationContext queryCompilationContext)
        => new SqlServer2008QueryTranslationPostprocessor(
            _dependencies,
            _relationalDependencies,
            queryCompilationContext);

    public class SqlServer2008QueryTranslationPostprocessor(QueryTranslationPostprocessorDependencies dependencies, RelationalQueryTranslationPostprocessorDependencies relationalDependencies, QueryCompilationContext queryCompilationContext) :
        RelationalQueryTranslationPostprocessor(dependencies, relationalDependencies, (RelationalQueryCompilationContext)queryCompilationContext)
    {
        public override Expression Process(Expression query)
        {
            query = base.Process(query);
            query = new Offset2RowNumberConvertVisitor(query, RelationalDependencies.SqlExpressionFactory).Visit(query);
            return query;
        }
        internal class Offset2RowNumberConvertVisitor(
            Expression root,
            ISqlExpressionFactory sqlExpressionFactory) : ExpressionVisitor
        {
            private readonly Expression root = root;
            private readonly ISqlExpressionFactory sqlExpressionFactory = sqlExpressionFactory;
            private const string SubTableName = "subTbl";
            private const string RowColumnName = "Row";

            private const string _projectionMappingProp = "_projectionMapping";

            protected override Expression VisitExtension(Expression node)
            {
                if (node is ShapedQueryExpression shapedQueryExpression)
                {
                    return shapedQueryExpression.Update(Visit(shapedQueryExpression.QueryExpression), shapedQueryExpression.ShaperExpression);
                }
                if (node is SelectExpression se)
                {
                    node = VisitSelect(se);
                    return node;
                }
                return base.VisitExtension(node);
            }

            private SelectExpression VisitSelect(SelectExpression selectExpression)
            {
                var oldOffset = selectExpression.Offset;
                if (oldOffset == null)
                    return selectExpression;

                var oldLimit = selectExpression.Limit;
                var oldOrderings = selectExpression.Orderings;

                // 在子查询中 OrderBy 必须写 Top 数量
                var newOrderings = oldOrderings.Count > 0 && (oldLimit != null || selectExpression == root)
                    ? oldOrderings.ToList()
                    : [];

                var rowOrderings = oldOrderings.Any()
                    ? oldOrderings
                    : [new OrderingExpression(new SqlFragmentExpression("(SELECT 1)"), true)];

                var oldSelect = selectExpression;

                var rowNumberExpression = new RowNumberExpression([], rowOrderings, oldOffset.TypeMapping);
                // 创建子查询
                IReadOnlyList<ProjectionExpression> projections =
                    [
                    new ProjectionExpression(rowNumberExpression, RowColumnName),];

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

                //新的条件:
                //var newPredicate = sqlExpressionFactory.Fragment($"({SubTableName}.[{RowColumnName}] > @__p_0) AND ({SubTableName}.[{RowColumnName}] <= @__p_0 + @__p_1)");

                var and1 = sqlExpressionFactory.GreaterThan(
                    new ColumnExpression(RowColumnName, SubTableName, typeof(int), null, true),
                    oldOffset);
                var and2 = sqlExpressionFactory.LessThanOrEqual(
                    new ColumnExpression(RowColumnName, SubTableName, typeof(int), null, true),
                    sqlExpressionFactory.Add(oldOffset, oldLimit));

                var newPredicate = sqlExpressionFactory.AndAlso(and1, and2);


                //新的Projection:
                var newProjections = oldSelect.Projection.Select(e =>
                {
                    var retn = e;
                    if (e != null && e.Expression is ColumnExpression col)
                    {
                        var newCol = new ColumnExpression(col.Name, SubTableName, col.Type, col.TypeMapping, col.IsNullable);
                        return new ProjectionExpression(newCol, e.Alias);
                    }
                    return e;
                }).ToList();

                // 创建新的 SelectExpression，将子查询作为来源
                var newSelect = new SelectExpression(
                    oldSelect.Alias,
                   [subquery],
                   newPredicate,//条件为offset.limit
                   oldSelect.GroupBy,
                    oldSelect.Having,
                newProjections, //oldSelect.Projection,
                  oldSelect.IsDistinct,
                 [],
                   null,//参数需要
                    null,
                     null,
                null);

                //使用反射替换_projectionMapping变量:
                var _projectionMapping = typeof(SelectExpression).GetField(_projectionMappingProp, BindingFlags.NonPublic | BindingFlags.Instance);
                _projectionMapping.SetValue(newSelect, _projectionMapping.GetValue(oldSelect));


                return newSelect;
            }
        }
    }
}

#pragma warning restore EF1001 // Internal EF Core API usage.

#endif