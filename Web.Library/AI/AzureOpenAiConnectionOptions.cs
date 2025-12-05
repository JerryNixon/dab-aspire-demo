using System.Data.Common;
using System.Globalization;

namespace Web.Library.AI;

public sealed record AzureOpenAiConnectionOptions(string Endpoint, string Key, string Deployment, string? ApiVersion)
{
    public static AzureOpenAiConnectionOptions FromConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));
        }

        var builder = new DbConnectionStringBuilder
        {
            ConnectionString = connectionString
        };

        var endpoint = GetRequiredValue(builder, "Endpoint");
        var key = GetRequiredValue(builder, "Key");
        var deployment = GetRequiredValue(builder, "Deployment");
        var apiVersion = GetOptionalValue(builder, "ApiVersion");

        return new AzureOpenAiConnectionOptions(endpoint, key, deployment, apiVersion);
    }

    private static string GetRequiredValue(DbConnectionStringBuilder builder, string key)
    {
        if (!builder.TryGetValue(key, out var value) || value is null)
        {
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Missing connection string value for '{0}'.", key));
        }

        var stringValue = Convert.ToString(value, CultureInfo.InvariantCulture);

        if (string.IsNullOrWhiteSpace(stringValue))
        {
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Connection string value for '{0}' cannot be empty.", key));
        }

        return stringValue;
    }

    private static string? GetOptionalValue(DbConnectionStringBuilder builder, string key)
    {
        if (!builder.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }
}
