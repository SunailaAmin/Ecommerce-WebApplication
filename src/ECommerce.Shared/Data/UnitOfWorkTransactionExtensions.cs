namespace ECommerce.Shared.Data;

public static class UnitOfWorkTransactionExtensions
{
    /// <summary>
    /// Commits the tracked changes in a transaction and rolls back if the save fails.
    /// The caller mutates tracked entities first, because the commit performs the save.
    /// </summary>
    public static async Task CommitChangesInTransactionAsync(
        this IUnitOfWork unitOfWork,
        CancellationToken cancellationToken = default)
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            await unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
