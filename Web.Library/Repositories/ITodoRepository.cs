using Web.Library.Models;

namespace Web.Library.Repositories;

public interface ITodoRepository
{
    Task<IReadOnlyList<Todo>> GetAsync(bool isCompleted, CancellationToken cancellationToken);
    Task AddAsync(Todo todo, CancellationToken cancellationToken);
    Task UpdateAsync(Todo todo, CancellationToken cancellationToken);
    Task DeleteAsync(Todo todo, CancellationToken cancellationToken);
}
