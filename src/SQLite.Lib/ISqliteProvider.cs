using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace SQLite.Lib.Abstractions
{
    /// <summary>
    /// Defines the contract for SQLite data access operations
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    public interface ISqliteProvider<T> where T : class
    {
        /// <summary>
        /// Creates the table for the entity type if it doesn't exist
        /// </summary>
        void CreateTable();

        /// <summary>
        /// Inserts a new entity
        /// </summary>
        /// <param name="entity">Entity to insert</param>
        /// <returns>The inserted entity with generated ID</returns>
        T Insert(T entity);

        /// <summary>
        /// Inserts multiple entities in a transaction
        /// </summary>
        /// <param name="entities">Entities to insert</param>
        /// <returns>Number of inserted records</returns>
        int InsertBatch(IEnumerable<T> entities);

        /// <summary>
        /// Updates an existing entity
        /// </summary>
        /// <param name="entity">Entity to update</param>
        /// <returns>True if updated successfully</returns>
        bool Update(T entity);

        /// <summary>
        /// Deletes an entity by ID
        /// </summary>
        /// <param name="id">Entity ID</param>
        /// <returns>True if deleted successfully</returns>
        bool Delete(long id);

        /// <summary>
        /// Gets an entity by ID
        /// </summary>
        /// <param name="id">Entity ID</param>
        /// <returns>The entity or null if not found</returns>
        T GetById(long id);

        /// <summary>
        /// Gets all entities
        /// </summary>
        /// <returns>All entities</returns>
        IEnumerable<T> GetAll();

        /// <summary>
        /// Gets entities matching a predicate
        /// </summary>
        /// <param name="predicate">Filter predicate</param>
        /// <returns>Matching entities</returns>
        IEnumerable<T> Find(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// Gets the count of all entities
        /// </summary>
        /// <returns>Total count</returns>
        long Count();

        /// <summary>
        /// Gets the count of entities matching a predicate
        /// </summary>
        /// <param name="predicate">Filter predicate</param>
        /// <returns>Matching count</returns>
        long Count(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// Executes a raw SQL query
        /// </summary>
        /// <param name="sql">SQL query</param>
        /// <param name="parameters">Query parameters</param>
        /// <returns>Query results</returns>
        IEnumerable<T> ExecuteQuery(string sql, params object[] parameters);

        /// <summary>
        /// Executes a raw SQL command
        /// </summary>
        /// <param name="sql">SQL command</param>
        /// <param name="parameters">Command parameters</param>
        /// <returns>Number of affected rows</returns>
        int ExecuteCommand(string sql, params object[] parameters);

        /// <summary>
        /// Begins a new transaction
        /// </summary>
        /// <returns>Transaction context</returns>
        IDisposable BeginTransaction();

        /// <summary>
        /// Vacuum the database to reclaim space
        /// </summary>
        void Vacuum();

        /// <summary>
        /// Analyze the database for query optimization
        /// </summary>
        void Analyze();
    }
}
