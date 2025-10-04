using Microsoft.AspNetCore.Http;

using FluentAssertions;

using IHFiction.FictionApi.Authors;
using IHFiction.SharedKernel.DataShaping;
using IHFiction.SharedKernel.Linking;
using IHFiction.SharedKernel.Pagination;

namespace IHFiction.UnitTests.Authors;

public class ListAuthorsShapingTests
{
    [Fact]
    public void WithLinks_And_ShapeData_Flattens_LinkedPagedCollection()
    {
        // Arrange - create a couple of ListAuthorsItem instances
        var item1 = new ListAuthors.ListAuthorsItem(Ulid.NewUlid(), "Alice", "bio1", DateTime.UtcNow, DateTime.UtcNow, 2, 1);
        var item2 = new ListAuthors.ListAuthorsItem(Ulid.NewUlid(), "Bob", "bio2", DateTime.UtcNow, DateTime.UtcNow, 3, 2);

        // Build a simple IQueryable over the items
        var items = new[] { item1, item2 }.AsQueryable();

        // Create a paged collection wrapper (TotalCount = 2, page=1, pageSize=10)
        var paged = new PagedCollection<ListAuthors.ListAuthorsItem>(items, 2, 1, 10);

        // Construct Linked<item> for each item and a collection-level links list
        var itemLinksQueryable = items.Select(e => new Linked<ListAuthors.ListAuthorsItem>(
            e,
            new[] { new LinkItem($"/authors/{e.Id}", "self", HttpMethods.Get) }
        ));

        var collectionLinks = new[] { new LinkItem("/authors", "self", HttpMethods.Get) };

        var linked = new LinkedPagedCollection<ListAuthors.ListAuthorsItem>(
            itemLinksQueryable,
            paged.TotalCount,
            paged.CurrentPage,
            paged.PageSize,
            collectionLinks);

        // Act - shape the linked paged collection. Use the object-based overload so root-level LinkedPagedCollection is shaped as an object
        var shaped = DataShapingService.ShapeData(linked, null);
        var dict = (IDictionary<string, object?>)shaped;

        // Assert - collection-level properties exist
        dict.Should().ContainKey("Data");
        dict.Should().ContainKey("TotalCount");
        dict.Should().ContainKey("CurrentPage");
        dict.Should().ContainKey("PageSize");
        // collection-level links may be named "links" (flattening) or "Links" (collection property)
        (dict.ContainsKey("links") || dict.ContainsKey("Links")).Should().BeTrue();

        // Assert - Data is an enumerable of shaped items
        var data = dict["Data"] as IEnumerable<object>;
        data.Should().NotBeNull();
        var dataList = data!.ToList();
        dataList.Should().HaveCount(2);

        // Each item should be an ExpandoObject with item properties and links
        foreach (var obj in dataList)
        {
            var itemDict = (IDictionary<string, object?>)obj!;
            itemDict.Should().ContainKey("Id");
            itemDict.Should().ContainKey("Name");
            itemDict.Should().ContainKey("links");
            var itemLinks = (IEnumerable<LinkItem>)itemDict["links"]!;
            itemLinks.Should().ContainSingle();
        }

        // verify collection-level links content
        var collectionLinksObj = dict.ContainsKey("links") ? dict["links"] : dict["Links"];
        collectionLinksObj.Should().BeAssignableTo<IEnumerable<LinkItem>>();
        var collLinks = ((IEnumerable<LinkItem>)collectionLinksObj!).ToList();
        collLinks.Should().ContainSingle();
    }
}
