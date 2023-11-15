namespace EntityFrameworkCore.UseRowNumberForPaging
{
    using System.Reflection;
    public class SqlServer2008QueryTranslationPostprocessorFactory : IQueryTranslationPostprocessorFactory
    {
        private readonly QueryTranslationPostprocessorDependencies _dependencies;
        private readonly RelationalQueryTranslationPostprocessorDependencies _relationalDependencies;
        public SqlServer2008QueryTranslationPostprocessorFactory(QueryTranslationPostprocessorDependencies dependencies, RelationalQueryTranslationPostprocessorDependencies relationalDependencies)
        {
            _dependencies = dependencies;
            _relationalDependencies = relationalDependencies;
        }

        public virtual QueryTranslationPostprocessor Create(QueryCompilationContext queryCompilationContext)
            => new SqlServer2008QueryTranslationPostprocessor(
                _dependencies,
                _relationalDependencies,
                queryCompilationContext);
        public class SqlServer2008QueryTranslationPostprocessor : RelationalQueryTranslationPostprocessor
        {
            public SqlServer2008QueryTranslationPostprocessor(QueryTranslationPostprocessorDependencies dependencies, RelationalQueryTranslationPostprocessorDependencies relationalDependencies, QueryCompilationContext queryCompilationContext)
                : base(dependencies, relationalDependencies, queryCompilationContext)
            {
            }
            public override Expression Process(Expression query)
            {
                query = base.Process(query);
                query = new Offset2RowNumberConvertVisitor(query, RelationalDependencies.SqlExpressionFactory).Visit(query);
                return query;
            }
            internal class Offset2RowNumberConvertVisitor : ExpressionVisitor
            {

                private static readonly MethodInfo GenerateOuterColumnAccessor;
                private static readonly Type TableReferenceExpressionType;

                private readonly Expression root;
                private readonly ISqlExpressionFactory sqlExpressionFactory;
                static Offset2RowNumberConvertVisitor()
                {
                    var method = typeof(SelectExpression).GetMethod("GenerateOuterColumn", BindingFlags.NonPublic | BindingFlags.Instance);

                    if (!typeof(ColumnExpression).IsAssignableFrom(method?.ReturnType))
                        throw new InvalidOperationException("SelectExpression.GenerateOuterColumn() is not found.");

                    TableReferenceExpressionType = method.GetParameters().First().ParameterType;
                    GenerateOuterColumnAccessor = method;
                }
                public Offset2RowNumberConvertVisitor(Expression root, ISqlExpressionFactory sqlExpressionFactory)
                {
                    this.root = root;
                    this.sqlExpressionFactory = sqlExpressionFactory;
                }
                protected override Expression VisitExtension(Expression node)
                {
                    if (node is ShapedQueryExpression shapedQueryExpression)
                    {
                        return shapedQueryExpression.Update(Visit(shapedQueryExpression.QueryExpression), shapedQueryExpression.ShaperExpression);
                    }
                    if (node is SelectExpression se)
                        node = VisitSelect(se);
                    return base.VisitExtension(node);
                }
                private Expression VisitSelect(SelectExpression selectExpression)
                {
                    var oldOffset = selectExpression.Offset;
                    if (oldOffset == null)
                        return selectExpression;

                    var oldLimit = selectExpression.Limit;
                    var oldOrderings = selectExpression.Orderings;

                    // 在子查询中 OrderBy 必须写 Top 数量
                    var newOrderings = oldOrderings.Count > 0 && (oldLimit != null || selectExpression == root)
                        ? oldOrderings.ToList()
                        : new List<OrderingExpression>();
                    // 更新表达式
                    selectExpression = selectExpression.Update(selectExpression.Projection.ToList(),
                                                               selectExpression.Tables.ToList(),
                                                               selectExpression.Predicate,
                                                               selectExpression.GroupBy.ToList(),
                                                               selectExpression.Having,
                                                               orderings: newOrderings,
                                                               limit: null,
                                                               offset: null);
                    var rowOrderings = oldOrderings.Count != 0 ? oldOrderings
                        : new[] { new OrderingExpression(new SqlFragmentExpression("(SELECT 1)"), true) };

                    selectExpression.PushdownIntoSubquery();

                    var subQuery = (SelectExpression)selectExpression.Tables[0];
                    var projection = new RowNumberExpression(Array.Empty<SqlExpression>(), rowOrderings, oldOffset.TypeMapping);
                    var left = GenerateOuterColumnAccessor.Invoke(subQuery
                        , new object[]
                        {
                            Activator.CreateInstance(TableReferenceExpressionType, new object[] { subQuery,subQuery.Alias! })!,
                            projection,
                            "row",
                            true
                        }) as ColumnExpression;

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
}
