using System;
using System.Text.Json.Serialization;

namespace LoxNet;

/// <summary>
/// Metadata for cached structure files.
/// </summary>
internal record StructureCacheMetadata(
    [property: JsonPropertyName("lastModified")] string LastModified,
    [property: JsonPropertyName("host")] string Host,
    [property: JsonPropertyName("serial")] string Serial,
    [property: JsonPropertyName("fetchedAt")] DateTime FetchedAt,
    [property: JsonPropertyName("sizeBytes")] long SizeBytes);
