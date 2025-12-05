using Microsoft.DataApiBuilder.Rest.Options;
using Web.Library.Data;
using Web.Library.Models;

namespace Web.Library.Repositories;

public sealed class CategoryRepository : ICategoryRepository
{
    private readonly IDataApiTable<Category> _table;

    public CategoryRepository(IDataApiTable<Category> table)
    {
        _table = table;
    }

    public Task<IReadOnlyList<Category>> GetAsync(CancellationToken cancellationToken)
    {
        var options = new GetOptions();
        return _table.GetAsync(options, cancellationToken);
    }
}
