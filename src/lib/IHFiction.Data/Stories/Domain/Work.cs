using IHFiction.Data.Authors.Domain;
using IHFiction.Data.Searching.Domain;
using IHFiction.SharedKernel.Entities;

namespace IHFiction.Data.Stories.Domain;

public abstract class Work : DomainUlidEntityWithTimestamp, ISoftDeletable
{
    public required string Title { get; set; }

    public DateTime? PublishedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public bool IsPublished => PublishedAt.HasValue;

    private ICollection<Author>? _authors;
    public ICollection<Author> Authors => _authors ??= [];

    private ICollection<Tag>? _tags;
    public ICollection<Tag> Tags => _tags ??= [];

    public Ulid OwnerId { get; set; }
    public required Author Owner { get; set; }
}
