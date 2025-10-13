using System.Text.Json;
using System.Text.Json.Serialization;

using IHFiction.FictionApi.Account;
using IHFiction.FictionApi.Authors;
using IHFiction.FictionApi.Common;
using IHFiction.FictionApi.Stories;
using IHFiction.FictionApi.Tags;
using IHFiction.SharedKernel.Linking;
using IHFiction.SharedKernel.Pagination;

namespace IHFiction.FictionApi.Infrastructure;


// Keycloak
[JsonSerializable(typeof(ClientRepresentation[]))]
[JsonSerializable(typeof(RoleRepresentation[]))]


// Common
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(Ulid))]


// Account
[JsonSerializable(typeof(GetOwnAuthorProfile.GetOwnAuthorProfileQuery))]
[JsonSerializable(typeof(Linked<GetOwnAuthorProfile.GetOwnAuthorProfileResponse>))]

[JsonSerializable(typeof(GetOwnBookContent.GetOwnBookContentQuery))]
[JsonSerializable(typeof(Linked<GetOwnBookContent.GetOwnBookContentResponse>))]

[JsonSerializable(typeof(GetOwnChapterContent.GetOwnChapterContentQuery))]
[JsonSerializable(typeof(Linked<GetOwnChapterContent.GetOwnChapterContentResponse>))]

[JsonSerializable(typeof(GetOwnStories.GetOwnStoriesBody))]
[JsonSerializable(typeof(GetOwnStories.GetOwnStoriesQuery))]
[JsonSerializable(typeof(LinkedPagedCollection<GetOwnStories.AuthorStoryItem>))]

[JsonSerializable(typeof(GetOwnStoryContent.GetOwnStoryContentQuery))]
[JsonSerializable(typeof(Linked<GetOwnStoryContent.GetOwnStoryContentResponse>))]

[JsonSerializable(typeof(RegisterAsAuthor.RegisterAsAuthorQuery))]
[JsonSerializable(typeof(RegisterAsAuthor.RegisterAsAuthorBody))]
[JsonSerializable(typeof(Linked<RegisterAsAuthor.RegisterAsAuthorResponse>))]

[JsonSerializable(typeof(UpdateOwnAuthorProfile.UpdateOwnAuthorProfileQuery))]
[JsonSerializable(typeof(UpdateOwnAuthorProfile.UpdateOwnAuthorProfileBody))]
[JsonSerializable(typeof(Linked<UpdateOwnAuthorProfile.UpdateOwnAuthorProfileResponse>))]


// Authors
[JsonSerializable(typeof(GetAuthor.GetAuthorQuery))]
[JsonSerializable(typeof(Linked<GetAuthor.GetAuthorResponse>))]

[JsonSerializable(typeof(ListAuthors.ListAuthorsQuery))]
[JsonSerializable(typeof(LinkedPagedCollection<ListAuthors.ListAuthorsItem>))]


// Stories
[JsonSerializable(typeof(AddTagsToStory.AddTagsToStoryQuery))]
[JsonSerializable(typeof(AddTagsToStory.AddTagsToStoryBody))]
[JsonSerializable(typeof(Linked<AddTagsToStory.AddTagsToStoryResponse>))]

[JsonSerializable(typeof(ConvertStoryType.ConvertStoryTypeBody))]

[JsonSerializable(typeof(CreateBook.CreateBookQuery))]
[JsonSerializable(typeof(CreateBook.CreateBookBody))]
[JsonSerializable(typeof(Linked<CreateBook.CreateBookResponse>))]

[JsonSerializable(typeof(CreateBookChapter.CreateBookChapterQuery))]
[JsonSerializable(typeof(CreateBookChapter.CreateBookChapterBody))]
[JsonSerializable(typeof(Linked<CreateBookChapter.CreateBookChapterResponse>))]

[JsonSerializable(typeof(CreateStory.CreateStoryQuery))]
[JsonSerializable(typeof(CreateStory.CreateStoryBody))]
[JsonSerializable(typeof(Linked<CreateStory.CreateStoryResponse>))]

[JsonSerializable(typeof(CreateStoryChapter.CreateStoryChapterQuery))]
[JsonSerializable(typeof(CreateStoryChapter.CreateStoryChapterBody))]
[JsonSerializable(typeof(Linked<CreateStoryChapter.CreateStoryChapterResponse>))]

[JsonSerializable(typeof(GetPublishedChapterContent.GetPublishedChapterContentQuery))]
[JsonSerializable(typeof(Linked<GetPublishedChapterContent.GetPublishedChapterContentResponse>))]

[JsonSerializable(typeof(GetPublishedStory.GetPublishedStoryQuery))]
[JsonSerializable(typeof(Linked<GetPublishedStory.GetPublishedStoryResponse>))]

[JsonSerializable(typeof(GetPublishedStoryContent.GetPublishedStoryContentQuery))]
[JsonSerializable(typeof(Linked<GetPublishedStoryContent.GetPublishedStoryContentResponse>))]

[JsonSerializable(typeof(ListPublishedStories.ListPublishedStoriesQuery))]
[JsonSerializable(typeof(ListPublishedStories.ListPublishedStoriesBody))]
[JsonSerializable(typeof(LinkedPagedCollection<ListPublishedStories.ListPublishedStoriesItem>))]

[JsonSerializable(typeof(ListPublishedStoryChapters.ListPublishedStoryChaptersQuery))]
[JsonSerializable(typeof(LinkedPagedCollection<ListPublishedStoryChapters.ListPublishedStoryChaptersItem>))]

[JsonSerializable(typeof(PublishStory.PublishStoryQuery))]
[JsonSerializable(typeof(Linked<PublishStory.PublishStoryResponse>))]

[JsonSerializable(typeof(PublishWork.PublishWorkQuery))]
[JsonSerializable(typeof(PublishWork.PublishWorkBody))]
[JsonSerializable(typeof(Linked<PublishWork.PublishWorkResponse>))]

[JsonSerializable(typeof(UnpublishStory.UnpublishStoryQuery))]
[JsonSerializable(typeof(Linked<UnpublishStory.UnpublishStoryResponse>))]

[JsonSerializable(typeof(UpdateBookMetadata.UpdateBookMetadataQuery))]
[JsonSerializable(typeof(UpdateBookMetadata.UpdateBookMetadataBody))]
[JsonSerializable(typeof(Linked<UpdateBookMetadata.UpdateBookMetadataResponse>))]

[JsonSerializable(typeof(UpdateChapterContent.UpdateChapterContentQuery))]
[JsonSerializable(typeof(UpdateChapterContent.UpdateChapterContentBody))]
[JsonSerializable(typeof(Linked<UpdateChapterContent.UpdateChapterContentResponse>))]

[JsonSerializable(typeof(UpdateChapterMetadata.UpdateChapterMetadataQuery))]
[JsonSerializable(typeof(UpdateChapterMetadata.UpdateChapterMetadataBody))]
[JsonSerializable(typeof(Linked<UpdateChapterMetadata.UpdateChapterMetadataResponse>))]

[JsonSerializable(typeof(UpdateStoryContent.UpdateStoryContentQuery))]
[JsonSerializable(typeof(UpdateStoryContent.UpdateStoryContentBody))]
[JsonSerializable(typeof(Linked<UpdateStoryContent.UpdateStoryContentResponse>))]

[JsonSerializable(typeof(UpdateStoryMetadata.UpdateStoryMetadataQuery))]
[JsonSerializable(typeof(UpdateStoryMetadata.UpdateStoryMetadataBody))]
[JsonSerializable(typeof(Linked<UpdateStoryMetadata.UpdateStoryMetadataResponse>))]


// Tags
[JsonSerializable(typeof(ListTags.ListTagsQuery))]
[JsonSerializable(typeof(ListTags.ListTagsBody))]
[JsonSerializable(typeof(LinkedPagedCollection<ListTags.ListTagsItem>))]
internal partial class FictionApiJsonSerializerContext : JsonSerializerContext;