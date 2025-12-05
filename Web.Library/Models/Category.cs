using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Web.Library.Models;

public sealed record Category
{
    [Key]
    [JsonPropertyName("Id")]
    public int Id { get; init; }

    [JsonPropertyName("Name")]
    public string Name { get; init; } = string.Empty;
}
