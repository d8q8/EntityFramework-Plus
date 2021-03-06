﻿// Description: Entity Framework Bulk Operations & Utilities (EF Bulk SaveChanges, Insert, Update, Delete, Merge | LINQ Query Cache, Deferred, Filter, IncludeFilter, IncludeOptimize | Audit)
// Website & Documentation: https://github.com/zzzprojects/Entity-Framework-Plus
// Forum & Issues: https://github.com/zzzprojects/EntityFramework-Plus/issues
// License: https://github.com/zzzprojects/EntityFramework-Plus/blob/master/LICENSE
// More projects: http://www.zzzprojects.com/
// Copyright © ZZZ Projects Inc. 2014 - 2016. All rights reserved.

#if EF7
using System.Reflection;
using Microsoft.Data.Entity.ChangeTracking;
using Microsoft.Data.Entity.ChangeTracking.Internal;

namespace Z.EntityFramework.Plus
{
    public static partial class Extensions
    {
        internal static StateManager GetStateManager(this ChangeTracker changeTracker)
        {
            var _stateManagerField = changeTracker.GetType().GetField("_stateManager", BindingFlags.NonPublic | BindingFlags.Instance);
            return (StateManager) _stateManagerField.GetValue(changeTracker);
        }
    }
}

#endif