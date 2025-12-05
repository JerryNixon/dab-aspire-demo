namespace Web.Library.Data;

public sealed class DataApiOptions
{
    public DataApiOptions(string baseAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseAddress);

        if (!Uri.TryCreate(AppendTrailingSlash(baseAddress), UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("The Data API base address must be an absolute URI.", nameof(baseAddress));
        }

        BaseAddress = uri;
    }

    public Uri BaseAddress { get; }

    private static string AppendTrailingSlash(string value) => value.EndsWith('/') ? value : value + "/";
}
