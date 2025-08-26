using IHFiction.Data.Infrastructure;

using MongoDB.Bson;

namespace IHFiction.Data.Stories.Domain;

public sealed class Chapter : Work, IWorkBodyId
{
    public ObjectId? WorkBodyId { get; set; }

    public int Order { get; set; }

    public Book? Book { get; set; }
    public Ulid? BookId { get; set; }
    public Story? Story { get; set; }
    public Ulid? StoryId { get; set; }
}
