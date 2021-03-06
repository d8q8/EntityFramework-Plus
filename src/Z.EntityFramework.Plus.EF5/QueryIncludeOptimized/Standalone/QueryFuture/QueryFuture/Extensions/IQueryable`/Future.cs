﻿// Description: Entity Framework Bulk Operations & Utilities (EF Bulk SaveChanges, Insert, Update, Delete, Merge | LINQ Query Cache, Deferred, Filter, IncludeFilter, IncludeOptimize | Audit)
// Website & Documentation: https://github.com/zzzprojects/Entity-Framework-Plus
// Forum & Issues: https://github.com/zzzprojects/EntityFramework-Plus/issues
// License: https://github.com/zzzprojects/EntityFramework-Plus/blob/master/LICENSE
// More projects: http://www.zzzprojects.com/
// Copyright © ZZZ Projects Inc. 2014 - 2016. All rights reserved.


#if STANDALONE
using System.Linq;

namespace Z.EntityFramework.Plus
{
    internal static partial class QueryFutureExtensions
    {
        /// <summary>
        ///     Defer the execution of the <paramref name="query" /> and batch the query command with other
        ///     future queries. The batch is executed when a future query requires a database round trip.
        /// </summary>
        /// <typeparam name="T">The type of elements of the query.</typeparam>
        /// <param name="query">
        ///     The query to defer the execution of and to add in the batch of future
        ///     queries.
        /// </param>
        /// <returns>
        ///     The QueryFutureEnumerable&lt;TEntity&gt; added to the batch of futures queries.
        /// </returns>
        internal static QueryFutureEnumerable<T> Future<T>(this IQueryable<T> query)
        {
#if EF5 || EF6
            var objectQuery = query.GetObjectQuery();
            var futureBatch = QueryFutureManager.AddOrGetBatch(objectQuery.Context);
            var futureQuery = new QueryFutureEnumerable<T>(futureBatch, objectQuery);
#elif EF7
            var context = query.GetDbContext();
            var futureBatch = QueryFutureManager.AddOrGetBatch(context);
            var futureQuery = new QueryFutureEnumerable<T>(futureBatch, query);
#endif
            futureBatch.Queries.Add(futureQuery);

            return futureQuery;
        }
    }
}
#endif