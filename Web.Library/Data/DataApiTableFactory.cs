using Microsoft.DataApiBuilder.Rest;
using Microsoft.Extensions.Logging;

namespace Web.Library.Data;

public sealed class DataApiTableFactory : IDataApiTableFactory
{
    private readonly DataApiOptions _options;
    private readonly ILoggerFactory _loggerFactory;

    public DataApiTableFactory(DataApiOptions options, ILoggerFactory loggerFactory)
    {
        _options = options;
        _loggerFactory = loggerFactory;
    }

    public IDataApiTable<T> Create<T>() where T : class => Create<T>(typeof(T).Name);

    public IDataApiTable<T> Create<T>(string entityName) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);

        var entityUri = new Uri(_options.BaseAddress, $"api/{entityName}");
        var table = new TableRepository<T>(entityUri);
        var logger = _loggerFactory.CreateLogger<DataApiTable<T>>();
        return new DataApiTable<T>(table, logger);
    }
}
