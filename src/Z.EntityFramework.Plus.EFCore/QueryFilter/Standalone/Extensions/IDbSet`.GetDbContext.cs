﻿// Description: Entity Framework Bulk Operations & Utilities (EF Bulk SaveChanges, Insert, Update, Delete, Merge | LINQ Query Cache, Deferred, Filter, IncludeFilter, IncludeOptimize | Audit)
// Website & Documentation: https://github.com/zzzprojects/Entity-Framework-Plus
// Forum & Issues: https://github.com/zzzprojects/EntityFramework-Plus/issues
// License: https://github.com/zzzprojects/EntityFramework-Plus/blob/master/LICENSE
// More projects: http://www.zzzprojects.com/
// Copyright © ZZZ Projects Inc. 2014 - 2016. All rights reserved.


#if STANDALONE && (EF5 || EF6)
using System.Data.Entity;
using System.Reflection;

namespace Z.EntityFramework.Plus
{
    public static partial class QueryFilterExtensions
    {
        /// <summary>An IDbSet&lt;TEntity&gt; extension method that gets the DbContext from the DbSet.</summary>
        /// <typeparam name="T">The type of elements of the query.</typeparam>
        /// <param name="dbSet">The dbSet to act on.</param>
        /// <returns>The DbContext from the DbSet.</returns>
        internal static DbContext GetDbContext<T>(this IDbSet<T> dbSet) where T : class
        {
            var internalSet = dbSet.GetType().GetField("_internalSet", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(dbSet);
            var internalContext = internalSet.GetType().BaseType.GetField("_internalContext", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(internalSet);
            return (DbContext) internalContext.GetType().GetProperty("Owner", BindingFlags.Instance | BindingFlags.Public).GetValue(internalContext, null);
        }
    }
}

#endif