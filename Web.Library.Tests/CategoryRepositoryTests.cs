using Microsoft.DataApiBuilder.Rest.Options;
using Web.Library.Data;
using Web.Library.Models;
using Web.Library.Repositories;

namespace Web.Library.Tests;

public class CategoryRepositoryTests
{
    [Fact]
    public async Task GetAsync_ReturnsCategories()
    {
        var table = new FakeCategoryTable
        {
            Items =
            {
                new Category { Id = 1, Name = "Home" },
                new Category { Id = 2, Name = "Work" }
            }
        };
        var repository = new CategoryRepository(table);

        var result = await repository.GetAsync(CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.NotNull(table.CapturedGetOptions);
    }

    private sealed class FakeCategoryTable : IDataApiTable<Category>
    {
        public List<Category> Items { get; } = new();
        public GetOptions? CapturedGetOptions { get; private set; }

        public Task AddAsync(Category entity, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteAsync(Category entity, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<Category>> GetAsync(GetOptions options, CancellationToken cancellationToken)
        {
            CapturedGetOptions = options;
            return Task.FromResult<IReadOnlyList<Category>>(Items.ToArray());
        }

        public Task UpdateAsync(Category entity, PatchOptions options, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
