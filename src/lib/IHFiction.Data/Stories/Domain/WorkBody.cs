using System.ComponentModel.DataAnnotations;

using IHFiction.SharedKernel.Entities;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace IHFiction.Data.Stories.Domain;

/// <summary>
/// Represents the content body of a work (story, chapter, etc.) stored as markdown
/// </summary>
public sealed class WorkBody : IUpdatedAt
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.GenerateNewId();

    /// <summary>
    /// Optional note field that can contain markdown content
    /// </summary>
    public string? Note1 { get; set; }

    /// <summary>
    /// Optional note field that can contain markdown content
    /// </summary>
    public string? Note2 { get; set; }

    /// <summary>
    /// The main content of the work stored as markdown.
    /// Supports full markdown syntax including images, links, formatting, etc.
    /// </summary>
    public required string Content { get; set; }

    [ConcurrencyCheck]
    public DateTime UpdatedAt { get; set; }
    
    [Timestamp]
    public long Version => UpdatedAt.ToBinary();
}
