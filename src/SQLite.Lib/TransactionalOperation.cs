using System;
using SQLite.Lib.Contracts;

namespace SQLite.Lib
{
    /// <summary>
    /// Concrete implementation of a transactional operation
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    public class TransactionalOperation<T> : ITransactionalOperation<T> where T : class
    {
        public OperationType OperationType { get; }
        public T Entity { get; }
        public long? EntityId { get; }

        private TransactionalOperation(OperationType operationType, T entity = null, long? entityId = null)
        {
            OperationType = operationType;
            Entity = entity;
            EntityId = entityId;

            // Validate operation parameters
            switch (operationType)
            {
                case OperationType.Insert:
                case OperationType.Update:
                    if (entity == null)
                        throw new ArgumentNullException(nameof(entity), $"Entity cannot be null for {operationType} operation");
                    break;
                case OperationType.Delete:
                    if (!entityId.HasValue)
                        throw new ArgumentException("EntityId must have a value for Delete operation", nameof(entityId));
                    break;
            }
        }

        /// <summary>
        /// Creates an insert operation
        /// </summary>
        public static TransactionalOperation<T> CreateInsert(T entity)
        {
            return new TransactionalOperation<T>(OperationType.Insert, entity);
        }

        /// <summary>
        /// Creates an update operation
        /// </summary>
        public static TransactionalOperation<T> CreateUpdate(T entity)
        {
            return new TransactionalOperation<T>(OperationType.Update, entity);
        }

        /// <summary>
        /// Creates a delete operation
        /// </summary>
        public static TransactionalOperation<T> CreateDelete(long entityId)
        {
            return new TransactionalOperation<T>(OperationType.Delete, entityId: entityId);
        }

        public void Commit(IPersistenceProvider<,> provider)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            switch (OperationType)
            {
                case OperationType.Insert:
                    provider.Insert(Entity);
                    break;
                case OperationType.Update:
                    provider.Update(Entity);
                    break;
                case OperationType.Delete:
                    provider.Delete(EntityId.Value);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown operation type: {OperationType}");
            }
        }

        public void Rollback()
        {
            // In SQLite, rollback is handled at the transaction level
            // Individual operations don't need to do anything special
        }
    }
}