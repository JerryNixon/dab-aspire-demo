using Microsoft.DataApiBuilder.Rest.Options;
using Web.Library.Data;
using Web.Library.Models;

namespace Web.Library.Repositories;

public sealed class TodoRepository : ITodoRepository
{
    private static readonly PatchOptions PatchOptions = new()
    {
        ExcludeProperties =
        [
            nameof(Todo.Id),
            nameof(Todo.CategoryId)
        ]
    };

    private readonly IDataApiTable<Todo> _table;

    public TodoRepository(IDataApiTable<Todo> table)
    {
        _table = table;
    }

    public Task AddAsync(Todo todo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(todo);
        return _table.AddAsync(todo, cancellationToken);
    }

    public Task DeleteAsync(Todo todo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(todo);
        return _table.DeleteAsync(todo, cancellationToken);
    }

    public async Task<IReadOnlyList<Todo>> GetAsync(bool isCompleted, CancellationToken cancellationToken)
    {
        var options = new GetOptions
        {
            Filter = $"{nameof(Todo.IsCompleted)} eq {isCompleted.ToString().ToLowerInvariant()}"
        };

        return await _table.GetAsync(options, cancellationToken);
    }

    public Task UpdateAsync(Todo todo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(todo);
        return _table.UpdateAsync(todo, PatchOptions, cancellationToken);
    }
}
