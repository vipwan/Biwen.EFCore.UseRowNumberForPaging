using Azure;
using Microsoft.EntityFrameworkCore;

namespace TestProject1
{
    public class UnitTest1
    {
        [Fact]
        public void Test1()
        {
            //SELECT[t].[Id], [t].[Age], [t].[Email], [t].[Name]
            //FROM(
            //    SELECT[u].[Id], [u].[Age], [u].[Email], [u].[Name], ROW_NUMBER() OVER(ORDER BY(SELECT 1)) AS[row]
            //    FROM[Users] AS[u]
            //    WHERE[u].[Id] > 1
            //) AS[t]
            //WHERE[t].[row] > @__p_0 AND[t].[row] <= @__p_0 + @__p_1

            using var dbContext = new MyDbContext();
            var rawSql = dbContext.Users.OrderBy(x => x.Id).Skip(10).Take(20).ToQueryString();
            Assert.Contains("ROW_NUMBER", rawSql);

            //var list = dbContext.Users.OrderBy(x => x.Id).Skip(10).Take(20).ToList();
            //Assert.NotNull(list);


        }


    }
}
