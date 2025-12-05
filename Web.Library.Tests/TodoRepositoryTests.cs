using Microsoft.DataApiBuilder.Rest.Options;
using Web.Library.Data;
using Web.Library.Models;
using Web.Library.Repositories;

namespace Web.Library.Tests;

public class TodoRepositoryTests
{
    [Fact]
    public async Task GetAsync_BuildsExpectedFilter()
    {
        var table = new FakeTodoTable { Items = { new Todo { Id = 1, Title = "Test" } } };
        var repository = new TodoRepository(table);

        var result = await repository.GetAsync(isCompleted: true, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("IsCompleted eq true", table.CapturedGetOptions?.Filter);
    }

    [Fact]
    public async Task AddAsync_ForwardsEntity()
    {
        var table = new FakeTodoTable();
        var repository = new TodoRepository(table);
        var todo = new Todo { Title = "Add" };

        await repository.AddAsync(todo, CancellationToken.None);

        Assert.Same(todo, table.AddedEntity);
    }

    [Fact]
    public async Task UpdateAsync_UsesPreconfiguredPatchOptions()
    {
        var table = new FakeTodoTable();
        var repository = new TodoRepository(table);
        var todo = new Todo { Id = 2, Title = "Update" };

        await repository.UpdateAsync(todo, CancellationToken.None);

        Assert.Same(todo, table.UpdatedEntity);
    Assert.NotNull(table.CapturedPatchOptions);
    var options = table.CapturedPatchOptions!;
    Assert.NotNull(options.ExcludeProperties);
    var excluded = options.ExcludeProperties!;
    Assert.Contains(nameof(Todo.Id), excluded);
    Assert.Contains(nameof(Todo.CategoryId), excluded);
    }

    [Fact]
    public async Task DeleteAsync_ForwardsEntity()
    {
        var table = new FakeTodoTable();
        var repository = new TodoRepository(table);
        var todo = new Todo { Id = 3 };

        await repository.DeleteAsync(todo, CancellationToken.None);

        Assert.Same(todo, table.DeletedEntity);
    }

    private sealed class FakeTodoTable : IDataApiTable<Todo>
    {
        public List<Todo> Items { get; } = new();
        public GetOptions? CapturedGetOptions { get; private set; }
        public Todo? AddedEntity { get; private set; }
        public Todo? UpdatedEntity { get; private set; }
        public Todo? DeletedEntity { get; private set; }
        public PatchOptions? CapturedPatchOptions { get; private set; }

        public Task AddAsync(Todo entity, CancellationToken cancellationToken)
        {
            AddedEntity = entity;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Todo entity, CancellationToken cancellationToken)
        {
            DeletedEntity = entity;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Todo>> GetAsync(GetOptions options, CancellationToken cancellationToken)
        {
            CapturedGetOptions = options;
            return Task.FromResult<IReadOnlyList<Todo>>(Items.ToArray());
        }

        public Task UpdateAsync(Todo entity, PatchOptions options, CancellationToken cancellationToken)
        {
            UpdatedEntity = entity;
            CapturedPatchOptions = options;
            return Task.CompletedTask;
        }
    }
}
