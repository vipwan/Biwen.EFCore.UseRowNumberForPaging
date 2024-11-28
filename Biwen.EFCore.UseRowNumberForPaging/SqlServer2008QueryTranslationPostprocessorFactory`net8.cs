// Licensed to the Biwen.EFCore.UseRowNumberForPaging under one or more agreements.
// The Biwen.EFCore.UseRowNumberForPaging licenses this file to you under the MIT license. 
// See the LICENSE file in the project root for more information.
// Biwen.EFCore.UseRowNumberForPaging Author: 万雅虎, Github: https://github.com/vipwan
// Bring back support for UseRowNumberForPaging in EntityFrameworkCore 9.0/8.0 Use a ROW_NUMBER() in queries instead of OFFSET/FETCH. This method is backwards-compatible to SQL Server 2005.
// Modify Date: 2024-11-15 14:42:01 SqlServer2008QueryTranslationPostprocessorFactory.cs

#if NET8_0

namespace Biwen.EFCore.UseRowNumberForPaging;

using Microsoft.EntityFrameworkCore.Query;
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
        RelationalQueryTranslationPostprocessor(dependencies, relationalDependencies, queryCompilationContext)
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

            private static readonly MethodInfo GenerateOuterColumnAccessor;
            private static readonly Type TableReferenceExpressionType;

            private readonly Expression root = root;
            private readonly ISqlExpressionFactory sqlExpressionFactory = sqlExpressionFactory;
            static Offset2RowNumberConvertVisitor()
            {
                var method = typeof(SelectExpression).GetMethod("GenerateOuterColumn", BindingFlags.NonPublic | BindingFlags.Instance);

                if (!typeof(ColumnExpression).IsAssignableFrom(method?.ReturnType))
                    throw new InvalidOperationException("SelectExpression.GenerateOuterColumn() is not found.");

                var @params = method.GetParameters();
                TableReferenceExpressionType = @params.First().ParameterType;
                GenerateOuterColumnAccessor = method;
            }

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

                // 更新表达式
                selectExpression = selectExpression.Update([.. selectExpression.Projection],
                                                           [.. selectExpression.Tables],
                                                           selectExpression.Predicate,
                                                           [.. selectExpression.GroupBy],
                                                           selectExpression.Having,
                                                           orderings: newOrderings,
                                                           limit: null,
                                                           offset: null);


                selectExpression.PushdownIntoSubquery();

                var rowOrderings = oldOrderings.Any()
                    ? oldOrderings
                    : [new OrderingExpression(new SqlFragmentExpression("(SELECT 1)"), true)];

                var subQuery = selectExpression.Tables[0];
                var projection = new RowNumberExpression([], rowOrderings, oldOffset.TypeMapping);
                var left = GenerateOuterColumnAccessor.Invoke(subQuery
                    ,
                    [
                        Activator.CreateInstance(TableReferenceExpressionType, [subQuery,subQuery.Alias!])!,
                        projection,
                        "row",
                        true
                    ]) as ColumnExpression;

                selectExpression.ApplyPredicate(sqlExpressionFactory.GreaterThan(left!, oldOffset));

                if (oldLimit != null)
                {
                    if (oldOrderings.Count == 0)
                    {
                        selectExpression.ApplyPredicate(sqlExpressionFactory.LessThanOrEqual(left, sqlExpressionFactory.Add(oldOffset, oldLimit)));
                    }
                    else
                    {
                        // 这里不支持子查询的 OrderBy 操作
                        selectExpression.ApplyLimit(oldLimit);
                    }
                }
                return selectExpression;

            }
        }
    }
}

#endif