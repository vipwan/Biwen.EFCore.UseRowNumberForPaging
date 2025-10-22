using Microsoft.EntityFrameworkCore;
namespace TestProject1
{
    public class UnitTest1
    {



        [Fact]
        public void SimpleTest()
        {
            //SELECT[t].[Id], [t].[Age], [t].[Email], [t].[Name]
            //FROM(
            //    SELECT[u].[Id], [u].[Age], [u].[Email], [u].[Name], ROW_NUMBER() OVER(ORDER BY(SELECT 1)) AS[row]
            //    FROM[Users] AS[u]
            //    WHERE[u].[Id] > 1
            //) AS[t]
            //WHERE[t].[row] > @__p_0 AND[t].[row] <= @__p_0 + @__p_1

            using var dbContext = new MyDbContext();

#if NET9_0_OR_GREATER
            var rawSql = dbContext.Users.Where(x => x.Email != null && x.Email.StartsWith('v')).OrderBy(x => x.Id).Skip(10).ToQueryString();
            Assert.Contains("ROW_NUMBER", rawSql);
#endif
            var rawSql2 = dbContext.Users.OrderBy(x => x.Id).Skip(10).Take(20).ToQueryString();
            Assert.Contains("ROW_NUMBER", rawSql2);

            //makesure include must be using AsSplitQuery()
            var rawSql3 = dbContext.Users.Include(x => x.Hobbies).OrderBy(x => x.Id).Skip(10).Take(20).ToQueryString();
            Assert.Contains("ROW_NUMBER", rawSql3);

        }

        [Fact]
        public void IncludeTest()
        {
            using var dbContext = new MyDbContext();

            var rawSql = dbContext.Users.Include(x => x.Hobbies).OrderByDescending(x => x.Id)
                .Skip(10).Take(20).ToQueryString();

            Assert.Contains("ROW_NUMBER", rawSql);


            var rawSql2 = dbContext.Hobbies.Include(x => x.User)
                .Where(x => x.User.CreatedDate > DateTime.Now.AddDays(-10))
                .OrderBy(x => x.Id)
                .ThenByDescending(x => x.User.Id).Skip(10).Take(20).ToQueryString();

            Assert.Contains("ROW_NUMBER", rawSql2);


        }


        [Fact]
        public void AliasTest()
        {
            using var dbContext = new MyDbContext();

            var rawSql = dbContext.Users
                .Select(x => new { x.Id, Alias = x.Email }) //alias
                .OrderByDescending(x => x.Id)
                .Skip(10)
                .Take(20)
                .ToQueryString();

            //_context.Set<Post>().OrderBy(e => e.CreatedOn).Skip(10).Take(10).Select(e => new { Date = e.CreatedOn }).ToListAsync();

            Assert.Contains("ROW_NUMBER", rawSql);
        }

        [Fact]
        public void DateOnlyTranslateTest()
        {
            using var dbContext = new MyDbContext();

            var rawSql = dbContext.Users
                .Select(x => new { x.Id, Alias = x.Email, Date = DateOnly.FromDateTime(x.CreatedDate) }) //alias
                .OrderByDescending(x => x.Id)
                .Skip(10)
                .Take(20)
                .ToQueryString();

            //_context.Set<Post>().OrderBy(e => e.CreatedOn).Skip(10).Take(10).Select(e => new { Date = e.CreatedOn }).ToListAsync();

            Assert.Contains("ROW_NUMBER", rawSql);
        }


        [Fact]
        public void GroupingTest()
        {

            //var iQuery = from e in this._context.Set()
            //             where e.Exmblref.CompareTo("2503") > 0
            //             group e by e.Exmblref into gg
            //             select new
            //             {
            //                 refno = gg.Key,
            //                 count = gg.Count()
            //             };
            //var iQuery2 = iQuery.Skip(0).Take(2);

            using var dbContext = new MyDbContext();

            var iquery = from e in dbContext.Users
                         where e.CreatedDate < DateTime.Now
                         group e by e.Email into gg
                         select new
                         {
                             Email = gg.Key,
                             Count = gg.Count()
                         };
            //having
            iquery = iquery.Where(x => x.Count > 1);

            var rawSql = iquery.OrderByDescending(x => x.Count).Skip(10).Take(20).ToQueryString();

            Assert.Contains("ROW_NUMBER", rawSql);
        }
    }
}
