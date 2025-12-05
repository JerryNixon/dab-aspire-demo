using Microsoft.DataApiBuilder.Rest.Options;

namespace Web.Library.Data;

public interface IDataApiTable<T>
    where T : class
{
    Task<IReadOnlyList<T>> GetAsync(GetOptions options, CancellationToken cancellationToken);
    Task AddAsync(T entity, CancellationToken cancellationToken);
    Task UpdateAsync(T entity, PatchOptions options, CancellationToken cancellationToken);
    Task DeleteAsync(T entity, CancellationToken cancellationToken);
}
