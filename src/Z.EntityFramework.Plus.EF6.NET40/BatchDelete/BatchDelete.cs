﻿// Description: Entity Framework Bulk Operations & Utilities (EF Bulk SaveChanges, Insert, Update, Delete, Merge | LINQ Query Cache, Deferred, Filter, IncludeFilter, IncludeOptimize | Audit)
// Website & Documentation: https://github.com/zzzprojects/Entity-Framework-Plus
// Forum & Issues: https://github.com/zzzprojects/EntityFramework-Plus/issues
// License: https://github.com/zzzprojects/EntityFramework-Plus/blob/master/LICENSE
// More projects: http://www.zzzprojects.com/
// Copyright © ZZZ Projects Inc. 2014 - 2016. All rights reserved.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
#if EF5
using System.Data.Objects;
using Z.EntityFramework.Plus.Internal.Core.SchemaObjectModel;

#elif EF6
using System.Data.Entity.Core.Objects;
using Z.EntityFramework.Plus.Internal.Core.SchemaObjectModel;

#elif EFCORE
using System.Reflection;
using Microsoft.Data.Entity;
using Microsoft.Data.Entity.Metadata;

#endif

namespace Z.EntityFramework.Plus
{
    /// <summary>Class to batch delete.</summary>
    public class BatchDelete
    {
        /// <summary>The command text template.</summary>
        internal const string CommandTextTemplate = @"
DELETE
FROM    A
FROM    {TableName} AS A
        INNER JOIN ( {Select}
                    ) AS B ON {PrimaryKeys}

SELECT @@ROWCOUNT
";

        /// <summary>The command text template with WHILE loop.</summary>
        internal const string CommandTextWhileTemplate = @"
DECLARE @rowAffected INT
DECLARE @totalRowAffected INT

SET @totalRowAffected = 0

WHILE @rowAffected IS NULL
    OR @rowAffected > 0
    BEGIN
        DELETE TOP ({Top})
        FROM    A
        FROM    {TableName} AS A
                INNER JOIN ( {Select}
                           ) AS B ON {PrimaryKeys}

        SET @rowAffected = @@ROWCOUNT
        SET @totalRowAffected = @totalRowAffected + @rowAffected
    END

SELECT  @totalRowAffected
";

        /// <summary>The command text template with DELAY and WHILE loop</summary>
        internal const string CommandTextWhileDelayTemplate = @"
DECLARE @rowAffected INT
DECLARE @totalRowAffected INT

SET @totalRowAffected = 0

WHILE @rowAffected IS NULL
    OR @rowAffected > 0
    BEGIN
        IF @rowAffected IS NOT NULL
            BEGIN
                WAITFOR DELAY '{Delay}'
            END

        DELETE TOP ({Top})
        FROM    A
        FROM    {TableName} AS A
                INNER JOIN ( {Select}
                           ) AS B ON {PrimaryKeys}

        SET @rowAffected = @@ROWCOUNT
        SET @totalRowAffected = @totalRowAffected + @rowAffected
    END

SELECT  @totalRowAffected
";

        /// <summary>Default constructor.</summary>
        public BatchDelete()
        {
            BatchSize = 4000;
        }

        /// <summary>Gets or sets the size of the batch.</summary>
        /// <value>The size of the batch.</value>
        public int BatchSize { get; set; }

        /// <summary>Gets or sets the batch delay interval in milliseconds (The wait time between batch).</summary>
        /// <value>The batch delay interval in milliseconds (The wait time between batch).</value>
        public int BatchDelayInterval { get; set; }

        /// <summary>Gets or sets the DbCommand before being executed.</summary>
        /// <value>The DbCommand before being executed.</value>
        public Action<DbCommand> Executing { get; set; }

        /// <summary>Executes the batch delete operation.</summary>
        /// <typeparam name="T">The type of elements of the query.</typeparam>
        /// <param name="query">The query used to execute the batch operation.</param>
        /// <returns>The number of rows affected.</returns>
        public int Execute<T>(IQueryable<T> query) where T : class
        {
            // GET model and info
#if EF5 || EF6
            var model = query.GetDbContext().GetModel();
            var entity = model.Entity<T>();
            var keys = entity.Info.Key.PropertyRefs;

            // SELECT keys names
            var queryKeys = query.SelectByName(keys.Select(x => x.Name).ToList());
            var innerObjectQuery = queryKeys.GetObjectQuery();

            // CREATE command
            var command = CreateCommand(innerObjectQuery, entity);

            // EXECUTE
            var ownConnection = false;

            try
            {
                if (innerObjectQuery.Context.Connection.State != ConnectionState.Open)
                {
                    ownConnection = true;
                    innerObjectQuery.Context.Connection.Open();
                }

                if (Executing != null)
                {
                    Executing(command);
                }

                var rowAffecteds = (int) command.ExecuteScalar();
                return rowAffecteds;
            }
            finally
            {
                if (ownConnection && innerObjectQuery.Context.Connection.State != ConnectionState.Closed)
                {
                    innerObjectQuery.Context.Connection.Close();
                }
            }
#elif EFCORE
            var dbContext = query.GetDbContext();
            var entity = dbContext.Model.FindEntityType(typeof (T));
            var keys = entity.GetKeys().ToList()[0].Properties;

            var queryKeys = query.SelectByName(keys.Select(x => x.Name).ToList());

            // CREATE command
            var command = CreateCommand(queryKeys, entity);

            // EXECUTE
            var ownConnection = false;

            try
            {
                if (dbContext.Database.GetDbConnection().State != ConnectionState.Open)
                {
                    ownConnection = true;
                    dbContext.Database.OpenConnection();
                }

                if (Executing != null)
                {
                    Executing(command);
                }

                var rowAffecteds = (int) command.ExecuteScalar();
                return rowAffecteds;
            }
            finally
            {
                if (ownConnection && dbContext.Database.GetDbConnection().State != ConnectionState.Closed)
                {
                    dbContext.Database.CloseConnection();
                }
            }
#endif
        }

#if EF5 || EF6
        /// <summary>Creates a command to execute the batch operation.</summary>
        /// <param name="query">The query.</param>
        /// <param name="entity">The schema entity.</param>
        /// <returns>The new command to execute the batch operation.</returns>
        internal DbCommand CreateCommand<T>(ObjectQuery query, SchemaEntityType<T> entity)
        {
            // GET mapping
            var mapping = entity.Info.EntityTypeMapping.MappingFragment;
            var store = mapping.StoreEntitySet;
            var tableName = string.IsNullOrEmpty(store.Schema) ?
                string.Concat("[", store.Table, "]") :
                string.Concat("[", store.Schema, "].[", store.Table, "]");

            // GET keys mappings
            var columnKeys = new List<string>();
            foreach (var propertyKey in entity.Info.Key.PropertyRefs)
            {
                var mappingProperty = mapping.ScalarProperties.Find(x => x.Name == propertyKey.Name);

                if (mappingProperty == null)
                {
                    throw new Exception(string.Format(ExceptionMessage.BatchOperations_PropertyNotFound, propertyKey.Name));
                }

                columnKeys.Add(mappingProperty.ColumnName);
            }

            // GET command text template
            var commandTextTemplate = BatchSize > 0 ?
                BatchDelayInterval > 0 ?
                    CommandTextWhileDelayTemplate :
                    CommandTextWhileTemplate :
                CommandTextTemplate;

            // GET inner query
            var querySelect = query.ToTraceString();

            // GET primary key join
            var primaryKeys = string.Join(Environment.NewLine + "AND ", columnKeys.Select(x => string.Concat("A.[", x, "] = B.[", x, "]")));

            // REPLACE template
            commandTextTemplate = commandTextTemplate.Replace("{TableName}", tableName)
                .Replace("{Select}", querySelect)
                .Replace("{PrimaryKeys}", primaryKeys)
                .Replace("{Top}", BatchSize.ToString())
                .Replace("{Delay}", TimeSpan.FromMilliseconds(BatchDelayInterval).ToString(@"hh\:mm\:ss\:fff"));

            // CREATE command
            var command = query.Context.CreateStoreCommand();
            command.CommandText = commandTextTemplate;

            // ADD Parameter
            var parameterCollection = query.Parameters;
            foreach (var parameter in parameterCollection)
            {
                var param = command.CreateParameter();
                param.ParameterName = parameter.Name;
                param.Value = parameter.Value;

                command.Parameters.Add(param);
            }

            return command;
        }
#elif EFCORE
        public DbCommand CreateCommand(IQueryable query, IEntityType entity)
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.FullName == "EntityFramework.MicrosoftSqlServer, Version=7.0.0.0, Culture=neutral, PublicKeyToken=adb9793829ddae60");

            if (assembly != null)
            {
                var type = assembly.GetType("Microsoft.Data.Entity.SqlServerMetadataExtensions");
                var sqlServerEntityTypeMethod = type.GetMethod("SqlServer", BindingFlags.Public | BindingFlags.Static, null, new[] {typeof (IEntityType)}, null);
                var sqlServerPropertyMethod = type.GetMethod("SqlServer", BindingFlags.Public | BindingFlags.Static, null, new[] {typeof (IProperty)}, null);
                var sqlServer = (IRelationalEntityTypeAnnotations) sqlServerEntityTypeMethod.Invoke(null, new[] {entity});

                // GET mapping
                var tableName = string.IsNullOrEmpty(sqlServer.Schema) ?
                    string.Concat("[", sqlServer.TableName, "]") :
                    string.Concat("[", sqlServer.Schema, "].[", sqlServer.TableName, "]");

                // GET keys mappings
                var columnKeys = new List<string>();
                foreach (var propertyKey in entity.GetKeys().ToList()[0].Properties)
                {
                    var mappingProperty = sqlServerPropertyMethod.Invoke(null, new[] {propertyKey});

                    var columnNameProperty = mappingProperty.GetType().GetProperty("ColumnName", BindingFlags.Public | BindingFlags.Instance);
                    columnKeys.Add((string) columnNameProperty.GetValue(mappingProperty));
                }

                // GET command text template
                var commandTextTemplate = BatchSize > 0 ?
                    BatchDelayInterval > 0 ?
                        CommandTextWhileDelayTemplate :
                        CommandTextWhileTemplate :
                    CommandTextTemplate;

                // GET inner query
                var relationalCommand = query.CreateCommand();
                var querySelect = relationalCommand.CommandText;

                // GET primary key join
                var primaryKeys = string.Join(Environment.NewLine + "AND ", columnKeys.Select(x => string.Concat("A.[", x, "] = B.[", x, "]")));

                // REPLACE template
                commandTextTemplate = commandTextTemplate.Replace("{TableName}", tableName)
                    .Replace("{Select}", querySelect)
                    .Replace("{PrimaryKeys}", primaryKeys)
                    .Replace("{Top}", BatchSize.ToString())
                    .Replace("{Delay}", TimeSpan.FromMilliseconds(BatchDelayInterval).ToString(@"hh\:mm\:ss\:fff"));

                // CREATE command
                var command = query.GetDbContext().CreateStoreCommand();
                command.CommandText = commandTextTemplate;

                // ADD Parameter
                var parameterCollection = relationalCommand.Parameters;
                foreach (var parameter in parameterCollection)
                {
                    var param = command.CreateParameter();
                    param.ParameterName = parameter.Name;
                    param.Value = parameter.Value;

                    command.Parameters.Add(param);
                }

                return command;
            }
            return null;
        }
#endif
    }
}