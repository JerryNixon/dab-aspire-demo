using Web.Library.Models;

namespace Web.Library.Repositories;

public interface ICategoryRepository
{
    Task<IReadOnlyList<Category>> GetAsync(CancellationToken cancellationToken);
}
