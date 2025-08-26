using MongoDB.Bson;

namespace IHFiction.Data.Infrastructure;

public interface IWorkBodyId
{
    ObjectId? WorkBodyId { get; set; }
}
