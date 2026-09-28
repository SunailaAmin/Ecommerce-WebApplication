namespace ECommerce.Application.Interfaces;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a transaction. Nesting is not supported, because a second begin would
    /// silently take a different scope than the caller expects.
    /// </summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the pending changes and commits. Releasing the transaction is handled here
    /// so callers only deal with commit and rollback.
    /// </summary>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back and releases the transaction. Safe to call when none is active.
    /// </summary>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
