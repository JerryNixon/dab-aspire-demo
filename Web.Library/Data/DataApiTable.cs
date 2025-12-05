using System.Diagnostics;
using System.Linq;
using Microsoft.DataApiBuilder.Rest;
using Microsoft.DataApiBuilder.Rest.Options;
using Microsoft.Extensions.Logging;
using Web.Library.Diagnostics;

namespace Web.Library.Data;

internal sealed class DataApiTable<T> : IDataApiTable<T>
    where T : class
{
    private readonly TableRepository<T> _repository;
    private readonly ILogger<DataApiTable<T>> _logger;

    public DataApiTable(TableRepository<T> repository, ILogger<DataApiTable<T>> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<T>> GetAsync(GetOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        using var activity = Telemetry.StartActivity("dataapi.get", ActivityKind.Client);
        activity?.SetTag("dataapi.entity", typeof(T).Name);

        var response = await _repository.GetAsync(options, cancellationToken: cancellationToken);
        if (response.Success && response.Result is { } results)
        {
            var resultCount = results is ICollection<T> collection ? collection.Count : results.Count();
            activity?.SetTag("dataapi.result.count", resultCount);
            return results;
        }

        activity?.SetStatus(ActivityStatusCode.Error, response.Error?.Message ?? "Unknown error");
        throw CreateFailure("retrieve", response.Error?.Message);
    }

    public async Task AddAsync(T entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);

        using var activity = Telemetry.StartActivity("dataapi.create", ActivityKind.Client);
        activity?.SetTag("dataapi.entity", typeof(T).Name);

        var response = await _repository.PostAsync(entity, cancellationToken: cancellationToken);
        if (response.Success)
        {
            return;
        }

        activity?.SetStatus(ActivityStatusCode.Error, response.Error?.Message ?? "Unknown error");
        throw CreateFailure("create", response.Error?.Message);
    }

    public async Task UpdateAsync(T entity, PatchOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(options);

        using var activity = Telemetry.StartActivity("dataapi.update", ActivityKind.Client);
        activity?.SetTag("dataapi.entity", typeof(T).Name);

        var response = await _repository.PatchAsync(entity, options, cancellationToken: cancellationToken);
        if (response.Success)
        {
            return;
        }

        activity?.SetStatus(ActivityStatusCode.Error, response.Error?.Message ?? "Unknown error");
        throw CreateFailure("update", response.Error?.Message);
    }

    public async Task DeleteAsync(T entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);

        using var activity = Telemetry.StartActivity("dataapi.delete", ActivityKind.Client);
        activity?.SetTag("dataapi.entity", typeof(T).Name);

        var response = await _repository.DeleteAsync(entity, cancellationToken: cancellationToken);
        if (response.Success)
        {
            return;
        }

        activity?.SetStatus(ActivityStatusCode.Error, response.Error?.Message ?? "Unknown error");
        throw CreateFailure("delete", response.Error?.Message);
    }

    private InvalidOperationException CreateFailure(string operation, string? message)
    {
        var error = string.IsNullOrWhiteSpace(message) ? "Unknown error" : message;
        _logger.LogError("Failed to {Operation} {EntityName}: {Error}", operation, typeof(T).Name, error);
        return new InvalidOperationException($"Failed to {operation} {typeof(T).Name}: {error}");
    }
}
