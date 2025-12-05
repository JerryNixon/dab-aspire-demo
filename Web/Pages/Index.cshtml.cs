using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Web.Library.Models;
using Web.Library.Repositories;

namespace Web.Pages;

public class IndexModel : PageModel
{
    private readonly ITodoRepository _todoRepository;
    private readonly ICategoryRepository _categoryRepository;

    public IndexModel(ITodoRepository todoRepository, ICategoryRepository categoryRepository)
    {
        _todoRepository = todoRepository;
        _categoryRepository = categoryRepository;
    }

    public Todo[] PendingTodos { get; private set; } = [];

    public Todo[] CompletedTodos { get; private set; } = [];

    public Category[] Categories { get; private set; } = [];

    public IReadOnlyDictionary<int, string> CategoryLookup { get; private set; } = new Dictionary<int, string>();

    [TempData]
    public string? ErrorMessage { get; set; }

    [TempData]
    public int? EditingId { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            var cancellationToken = HttpContext.RequestAborted;

            PendingTodos = (await _todoRepository.GetAsync(false, cancellationToken)).ToArray();

            Categories = (await _categoryRepository.GetAsync(cancellationToken)).ToArray();
            CategoryLookup = Categories
                .GroupBy(c => c.Id)
                .ToDictionary(g => g.Key, g => g.First().Name);

            try
            {
                CompletedTodos = (await _todoRepository.GetAsync(true, cancellationToken)).ToArray();
            }
            catch (Exception completedEx)
            {
                ErrorMessage = $"Warning: Could not load completed todos: {completedEx.Message}";
                CompletedTodos = [];
            }
        }
        catch (Exception ex)
        {
            if (string.IsNullOrEmpty(ErrorMessage))
            {
                var innerMessage = ex.InnerException?.Message ?? "";
                ErrorMessage = $"Error fetching todos: {ex.Message}{(string.IsNullOrEmpty(innerMessage) ? "" : $" ({innerMessage})")}";
            }
        }
    }

    public async Task<IActionResult> OnPostAsync(string action, int? id, string? title)
    {
        try
        {
            var cancellationToken = HttpContext.RequestAborted;

            bool postedIsCompleted = false;
            if (Request.HasFormContentType && Request.Form.TryGetValue("isCompleted", out var val))
            {
                bool.TryParse(val.FirstOrDefault(), out postedIsCompleted);
            }

            int postedCategoryId = 0;
            if (Request.HasFormContentType && Request.Form.TryGetValue("categoryId", out var categoryValues))
            {
                int.TryParse(categoryValues.FirstOrDefault(), out postedCategoryId);
            }

            var todo = id.HasValue
                ? new Todo
                {
                    Id = id.Value,
                    Title = title ?? Request.Form["title"].FirstOrDefault() ?? string.Empty,
                    IsCompleted = postedIsCompleted,
                    CategoryId = postedCategoryId
                }
                : null;

            switch (action)
            {
                case "Create":
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        throw new ArgumentNullException(nameof(title), "Title cannot be empty.");
                    }

                    if (postedCategoryId <= 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(postedCategoryId), "Category is required.");
                    }

                    await _todoRepository.AddAsync(new Todo { Title = title!, CategoryId = postedCategoryId }, cancellationToken);
                    break;

                case "Edit":
                    if (!id.HasValue)
                    {
                        throw new ArgumentNullException(nameof(id));
                    }

                    EditingId = id.Value;
                    break;

                case "Update":
                    if (!id.HasValue || string.IsNullOrWhiteSpace(title))
                    {
                        throw new ArgumentNullException("ID and title are required.");
                    }

                    if (postedCategoryId <= 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(postedCategoryId), "Category is required.");
                    }

                    await _todoRepository.UpdateAsync(todo! with
                    {
                        Title = title!,
                        CategoryId = postedCategoryId,
                        IsCompleted = todo.IsCompleted
                    }, cancellationToken);

                    EditingId = null;
                    break;

                case "Toggle":
                    if (!id.HasValue)
                    {
                        throw new ArgumentNullException(nameof(id));
                    }

                    await _todoRepository.UpdateAsync(todo! with { IsCompleted = !todo.IsCompleted }, cancellationToken);
                    break;

                case "Delete":
                    if (!id.HasValue)
                    {
                        throw new ArgumentNullException(nameof(id));
                    }

                    await _todoRepository.DeleteAsync(todo!, cancellationToken);
                    break;

                case "CancelEdit":
                    EditingId = null;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(action), "Invalid action specified.");
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
        }

        return RedirectToPage();
    }
}