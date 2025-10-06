using System.Globalization;

using IHFiction.SharedKernel.Infrastructure;

using MongoDB.Bson;

using static IHFiction.Data.Stories.Domain.StoryType;

namespace IHFiction.SharedWeb.Services;

public partial class StoryEditorService(
    AccountService accountService,
    BookService bookService,
    ChapterService chapterService,
    StoryService storyService,
    WorkService workService)
{
    public void CreateNewStory(string storyType)
    {
        CurrentStory = StoryEditorModel.Create(storyType);
    }

    // Book support for MultiBook stories

    public async Task<Result<BookEditorModel>> AddNewBookAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentStory?.Id is null) return Errors.NoStoryId;

        var newBook = new CreateBookBody { Title = $"New Book {CurrentStory.Books.Count + 1}", Description = string.Empty };
        var result = await bookService.CreateBookAsync(CurrentStory.Id.Value, newBook, null, cancellationToken);

        if (result.IsFailure) return result.DomainError;

        var apiResult = result.Value;

        var bookModel = BookEditorModel.Create(
            Ulid.Parse(apiResult.Id, CultureInfo.InvariantCulture),
            apiResult.Title,
            apiResult.Description,
            CurrentStory.Books.Count,
            false
        );

        // Add to CurrentStory.Books, raise events, etc.
        CurrentStory.Books.Add(bookModel);

        return bookModel;
    }

    public Task<Result> DeleteBookAsync(BookEditorModel book)
    {
        if (CurrentStory is null) return Task.FromResult(Result.Failure(Errors.NoStoryLoaded));
        // TODO: Implement actual API call and logic
        // Remove from CurrentStory.Books, raise events, etc.
        CurrentStory.Books.Remove(book);
        return Task.FromResult(Result.Success());
    }

    private async Task<Result<ChapterEditorModel>> AddNewStoryChapterAsync(CancellationToken cancellationToken)
    {
        if (CurrentStory?.Id is null) return Result.Failure<ChapterEditorModel>(Errors.NoStoryId);

        var newChapter = new AddChapterToStoryBody { Title = $"New Chapter {CurrentStory.Chapters.Count + 1}", Content = "# New Chapter" };
        var result = await chapterService.AddChapterToStoryAsync(CurrentStory.Id.Value, newChapter, null, cancellationToken);

        if (result.IsFailure) return result.DomainError;

        var apiResult = result.Value;

        var chapterModel = ChapterEditorModel.Create(
            Ulid.Parse(apiResult.ChapterId, CultureInfo.InvariantCulture),
            apiResult.ChapterTitle,
            apiResult.ChapterUpdatedAt.UtcDateTime,
            ObjectId.Parse(apiResult.ContentId),
            apiResult.Content,
            apiResult.Note1,
            apiResult.Note2,
            apiResult.ContentUpdatedAt.UtcDateTime,
            CurrentStory.Chapters.Count,
            apiResult.ChapterPublishedAt.HasValue
        );

        CurrentStory.Chapters.Add(chapterModel);

        return chapterModel;
    }

    private async Task<Result<ChapterEditorModel>> AddNewBookChapterAsync(CancellationToken cancellationToken)
    {
        if (CurrentBook?.Id is null) return Result.Failure<ChapterEditorModel>(Errors.NoBookId);

        var newChapter = new AddChapterToBookBody { Title = $"New Chapter {CurrentBook.Chapters.Count + 1}", Content = "# New Chapter" };
        var result = await bookService.AddChapterToBookAsync(CurrentBook.Id.Value, newChapter, null, cancellationToken);

        if (result.IsFailure) return result.DomainError;

        var apiResult = result.Value;

        var chapterModel = ChapterEditorModel.Create(
            Ulid.Parse(apiResult.ChapterId, CultureInfo.InvariantCulture),
            apiResult.ChapterTitle,
            apiResult.ChapterUpdatedAt.UtcDateTime,
            ObjectId.Parse(apiResult.ContentId),
            apiResult.Content,
            apiResult.Note1,
            apiResult.Note2,
            apiResult.ContentUpdatedAt.UtcDateTime,
            CurrentBook.Chapters.Count,
            apiResult.ChapterPublishedAt.HasValue
        );

        CurrentBook.Chapters.Add(chapterModel);

        return chapterModel;
    }

    public async Task<Result<ChapterEditorModel>> AddNewChapterAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentStory is null) return Result.Failure<ChapterEditorModel>(Errors.NoStoryLoaded);

        return CurrentStory.StoryType switch
        {
            MultiBook => await AddNewBookChapterAsync(cancellationToken),
            MultiChapter => await AddNewStoryChapterAsync(cancellationToken),
            _ => Errors.InvalidStoryType
        };
    }

    public async Task<Result> DeleteChapterAsync(ChapterEditorModel chapter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chapter);

        if (CurrentStory is null) return Result.Failure(Errors.NoStoryLoaded);
        if (chapter.Id is null) return Result.Failure<ChapterEditorModel>(Errors.NoStoryId);

        var result = await chapterService.DeleteChapterAsync(chapter.Id.Value, cancellationToken);

        if (result.IsFailure) return result;

        CurrentStory.Chapters.Remove(chapter);

        return Result.Success();
    }

    public async Task<Result<ChapterEditorModel>> LoadChapterAsync(Ulid chapterId, CancellationToken cancellationToken = default)
    {
        if (CurrentStory is null) return Result.Failure<ChapterEditorModel>(Errors.NoStoryLoaded);

        return CurrentStory.StoryType switch
        {
            MultiBook => await LoadBookChapterAsync(chapterId, cancellationToken),
            MultiChapter => await LoadStoryChapterAsync(chapterId, cancellationToken),
            _ => Errors.InvalidStoryType
        };
    }

    private async Task<Result<ChapterEditorModel>> LoadBookChapterAsync(Ulid chapterId, CancellationToken cancellationToken)
    {
        var result = await chapterService.GetCurrentAuthorChapterContentAsync(chapterId, null, cancellationToken);

        if (result.IsFailure) return result.DomainError; // Convert to generic Result<T>

        var apiResult = result.Value;

        CurrentChapter = ChapterEditorModel.Create(
            Ulid.Parse(apiResult.Id, CultureInfo.InvariantCulture),
            apiResult.Title,
            apiResult.UpdatedAt.UtcDateTime,
            ObjectId.Parse(apiResult.ContentId),
            apiResult.Content,
            apiResult.Note1,
            apiResult.Note2,
            apiResult.ContentUpdatedAt.UtcDateTime,
            apiResult.Order,
            apiResult.PublishedAt.HasValue
        );

        if (CurrentBook is null) return Errors.NoBookLoaded;

        var index = CurrentBook.Chapters.ToList().FindIndex(c => c.Id == CurrentChapter.Id);

        if (index == -1) return Errors.ChapterLoad;

        await using (CurrentBook.SuppressDirty())
        {
            CurrentBook.Chapters[index] = CurrentChapter;
        }

        return CurrentChapter;
    }

    private async Task<Result<ChapterEditorModel>> LoadStoryChapterAsync(Ulid chapterId, CancellationToken cancellationToken)
    {
        var result = await chapterService.GetCurrentAuthorChapterContentAsync(chapterId, null, cancellationToken);

        if (result.IsFailure) return result.DomainError; // Convert to generic Result<T>

        var apiResult = result.Value;

        CurrentChapter = ChapterEditorModel.Create(
            Ulid.Parse(apiResult.Id, CultureInfo.InvariantCulture),
            apiResult.Title,
            apiResult.UpdatedAt.UtcDateTime,
            ObjectId.Parse(apiResult.ContentId),
            apiResult.Content,
            apiResult.Note1,
            apiResult.Note2,
            apiResult.ContentUpdatedAt.UtcDateTime,
            apiResult.Order,
            apiResult.PublishedAt.HasValue
        );

        if (CurrentStory is null) return Errors.NoStoryLoaded;

        var index = CurrentStory.Chapters.ToList().FindIndex(c => c.Id == CurrentChapter.Id);

        if (index == -1) return Errors.ChapterLoad;

        await using (CurrentStory.SuppressDirty())
        {
            CurrentStory.Chapters[index] = CurrentChapter;
        }

        return CurrentChapter;
    }

    public async Task<Result<BookEditorModel>> LoadBookAsync(Ulid bookId, CancellationToken cancellationToken = default)
    {
        var result = await bookService.GetCurrentAuthorBookContentAsync(bookId, null, cancellationToken);

        if (result.IsFailure) return result.DomainError;

        var apiResult = result.Value;

        CurrentBook = BookEditorModel.Create(
            Ulid.Parse(apiResult.Id, CultureInfo.InvariantCulture),
            apiResult.Title,
            apiResult.Description,
            apiResult.Order,
            apiResult.PublishedAt.HasValue
        );

        await using (CurrentBook.SuppressDirty())
        {
            foreach (var chapSummary in apiResult.Chapters)
            {
                CurrentBook.Chapters.Add(ChapterEditorModel.Create(
                    Ulid.Parse(chapSummary.Id, CultureInfo.InvariantCulture),
                    chapSummary.Title,
                    chapSummary.ChapterUpdatedAt.UtcDateTime,
                    ObjectId.Parse(chapSummary.ContentId),
                    chapSummary.Content,
                    chapSummary.Note1,
                    chapSummary.Note2,
                    chapSummary.ContentUpdatedAt.UtcDateTime,
                    chapSummary.Order,
                    chapSummary.ChapterPublishedAt.HasValue
                ));
            }
        }

        if (CurrentStory is null) return Errors.NoStoryLoaded;

        var index = CurrentStory.Books.ToList().FindIndex(c => c.Id == CurrentBook.Id);

        if (index == -1) return Errors.BookLoad;

        await using (CurrentStory.SuppressDirty())
        {
            CurrentStory.Books[index] = CurrentBook;
        }

        CurrentChapter = null;

        return CurrentBook;
    }

    public async Task<Result<StoryEditorModel>> LoadStoryAsync(Ulid storyId, CancellationToken cancellationToken = default)
    {
        var result = await accountService.GetCurrentAuthorStoryContentAsync(storyId, null, cancellationToken);

        if (result.IsFailure) return result.DomainError; // Convert to generic Result<T>

        var apiResult = result.Value;

        string? storyType;

        if (apiResult.Books.Count > 0)
            storyType = MultiBook;

        else storyType = apiResult.Chapters.Count > 0 ? MultiChapter : SingleBody;

        var storyModel = StoryEditorModel.Create(
            storyType,
            apiResult.IsPublished,
            Ulid.Parse(apiResult.StoryId, CultureInfo.InvariantCulture),
            apiResult.StoryTitle,
            apiResult.StoryDescription,
            apiResult.StoryUpdatedAt.UtcDateTime,
            ObjectId.Parse(apiResult.ContentId),
            apiResult.Content,
            apiResult.Note1,
            apiResult.Note2,
            apiResult.ContentUpdatedAt?.UtcDateTime
        );

        if (apiResult.Books.Count > 0 || apiResult.Chapters.Count > 0)
        {
            await using (storyModel.SuppressDirty())
            {
                foreach (var bookSummary in apiResult.Books)
                {
                    storyModel.Books.Add(BookEditorModel.Create(
                        Ulid.Parse(bookSummary.Id, CultureInfo.InvariantCulture),
                        bookSummary.Title,
                        bookSummary.Description,
                        bookSummary.Order,
                        bookSummary.PublishedAt.HasValue
                    ));
                }

                foreach (var chapterSummary in apiResult.Chapters)
                {
                    storyModel.Chapters.Add(ChapterEditorModel.Create(
                        Ulid.Parse(chapterSummary.Id, CultureInfo.InvariantCulture),
                        chapterSummary.Title,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        chapterSummary.Order,
                        false
                    ));
                }
            }
        }

        CurrentStory = storyModel;
        CurrentBook = null;
        CurrentChapter = null;

        return storyModel;
    }

    public async Task<Result> SaveStoryAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentStory == null) return Result.Failure(Errors.NoStoryLoaded);

        if (!CurrentStory.Dirty) return Result.Success(); // No changes to save

        // Check if it's a new story
        if (CurrentStory.Id is null || CurrentStory.Id == Ulid.Empty)
        {
            var createBody = new CreateStoryBody
            {
                Title = CurrentStory.Title,
                Description = CurrentStory.Description,
                StoryType = CurrentStory.StoryType
            };

            var createResult = await storyService.CreateStoryAsync(createBody, null, cancellationToken);

            if (createResult.IsFailure) return createResult.DomainError;

            var apiResult = createResult.Value;

            await using (CurrentStory.SuppressDirty())
            {
                CurrentStory.Id = Ulid.Parse(apiResult.Id, CultureInfo.InvariantCulture);
                CurrentStory.StoryUpdatedAt = apiResult.UpdatedAt.UtcDateTime;
                CurrentStory.Description = apiResult.Description;
                CurrentStory.Title = apiResult.Title;
                CurrentStory.StoryUpdatedAt = apiResult.UpdatedAt.UtcDateTime;
            }
        }
        else
        {
            // Save Story Metadata (only if not a new story or if metadata changed after initial creation)
            var updateMetadataBody = new UpdateStoryMetadataBody() { Title = CurrentStory.Title, Description = CurrentStory.Description };
            var result = await storyService.UpdateStoryMetadataAsync(CurrentStory.Id.Value, updateMetadataBody, null, cancellationToken);

            if (result.IsFailure) return result.DomainError;

            var apiResult = result.Value;

            await using (CurrentStory.SuppressDirty())
            {
                CurrentStory.Id = Ulid.Parse(apiResult.Id, CultureInfo.InvariantCulture);
                CurrentStory.StoryUpdatedAt = apiResult.UpdatedAt.UtcDateTime;
                CurrentStory.Description = apiResult.Description;
                CurrentStory.Title = apiResult.Title;
                CurrentStory.StoryUpdatedAt = apiResult.UpdatedAt.UtcDateTime;
            }
        }

        // Save Story Content (if applicable)
        if (CurrentStory.StoryType == SingleBody)
        {
            var updateContentBody = new UpdateStoryContentBody() { Content = CurrentStory.Content, Note1 = CurrentStory.Note1, Note2 = CurrentStory.Note2 };
            var result = await storyService.UpdateStoryContentAsync(CurrentStory.Id.Value, updateContentBody, null, cancellationToken);

            if (result.IsFailure) return result.DomainError;

            var apiResult = result.Value;

            await using (CurrentStory.SuppressDirty())
            {
                CurrentStory.Id = Ulid.Parse(apiResult.StoryId, CultureInfo.InvariantCulture);
                CurrentStory.Title = apiResult.StoryTitle;
                CurrentStory.ContentId = ObjectId.Parse(apiResult.ContentId);
                CurrentStory.Content = apiResult.Content;
                CurrentStory.Note1 = apiResult.Note1;
                CurrentStory.Note2 = apiResult.Note2;
                CurrentStory.ContentUpdatedAt = apiResult.ContentUpdatedAt.UtcDateTime;
                CurrentStory.StoryUpdatedAt = apiResult.StoryUpdatedAt.UtcDateTime;
            }
        }

        // Save Chapters
        foreach (var chapter in CurrentStory.Chapters)
        {
            var saveChapterResult = await SaveChapterAsync(chapter, cancellationToken);
            if (saveChapterResult.IsFailure) return saveChapterResult;
        }

        // Save Books and their Chapters
        foreach (var book in CurrentStory.Books)
        {
            var saveBookResult = await SaveBookAsync(book, cancellationToken);
            if (saveBookResult.IsFailure) return saveBookResult;
        }

        CurrentStory.Dirty = false; // Re-evaluate dirty state after saving

        return Result.Success();
    }

    private async Task<Result> SaveChapterAsync(ChapterEditorModel chapter, CancellationToken cancellationToken)
    {
        if (!chapter.Dirty) return Result.Success();

        if (chapter.Id is null || chapter.Id == Ulid.Empty) return Errors.NoChapterId;

        // Save Chapter Metadata

        var updateMetadataBody = new UpdateChapterMetadataBody() { Title = chapter.Title };
        var metaResult = await chapterService.UpdateChapterMetadataAsync(chapter.Id.Value, updateMetadataBody, null, cancellationToken);

        if (metaResult.IsFailure) return metaResult.DomainError;

        await using (chapter.SuppressDirty())
        {
            var updateMetadataResponse = metaResult.Value;
            chapter.Title = updateMetadataResponse.ChapterTitle;

            // Save Chapter Content
            var updateContentBody = new UpdateChapterContentBody() { Content = chapter.Content, Note1 = chapter.Note1, Note2 = chapter.Note2 };
            var contentResult = await chapterService.UpdateChapterContentAsync(chapter.Id.Value, updateContentBody, null, cancellationToken);

            if (contentResult.IsFailure) return contentResult.DomainError;

            var updateContentResponse = contentResult.Value;

            chapter.Title = updateContentResponse.ChapterTitle;
            chapter.Content = updateContentResponse.Content;
            chapter.Note1 = updateContentResponse.Note1;
            chapter.Note2 = updateContentResponse.Note2;
        }

        chapter.Dirty = false; // Re-evaluate dirty state after saving

        return Result.Success();
    }

    private async Task<Result> SaveBookAsync(BookEditorModel book, CancellationToken cancellationToken)
    {
        if (!book.Dirty) return Result.Success();
        if (book.Id is null) return Errors.NoBookId;

        // Save Book Metadata if changed
        var updateBookBody = new UpdateBookMetadataBody { Title = book.Title, Description = book.Description };
        var result = await bookService.UpdateBookMetadataAsync(book.Id.Value, updateBookBody, cancellationToken);

        if (result.IsFailure) return result.DomainError;

        var updateBookResponse = result.Value;

        await using (book.SuppressDirty())
        {
            book.Title = updateBookResponse.Title;
            book.Description = updateBookResponse.Description;

            // Save Chapters within the book
            foreach (var chapter in book.Chapters)
            {
                var saveChapterResult = await SaveChapterAsync(chapter, cancellationToken);
                if (saveChapterResult.IsFailure) return saveChapterResult;
            }
        }

        book.Dirty = false; // Re-evaluate dirty state after saving

        return Result.Success();
    }

    public async Task<Result> ConvertStoryTypeAsync(string targetType, CancellationToken cancellationToken = default)
    {
        if (CurrentStory is null) return Errors.NoStoryLoaded;

        if (CurrentStory.Id is null) return Errors.NoStoryId;

        var body = new ConvertStoryTypeBody { TargetType = targetType };
        var result = await storyService.ConvertStoryTypeAsync(CurrentStory.Id.Value, body, cancellationToken);

        if (result.IsSuccess) await LoadStoryAsync(CurrentStory.Id.Value, cancellationToken);

        return result;
    }

    public async Task<Result> PublishStoryAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentStory is null) return Errors.NoStoryLoaded;

        if (CurrentStory.Id is null) return Errors.NoStoryId;

        if (!CurrentStory.IsPublished)
        {
            var result = await workService.PublishWorkAsync(CurrentStory.Id.Value, new(), null, cancellationToken);

            if (result.IsSuccess) CurrentStory.IsPublished = true;
            else return result.DomainError;
        }

        if (CurrentBook is not null && !CurrentBook.IsPublished)
        {
            if (CurrentBook.Id is null) return Errors.NoBookId;

            var result = await workService.PublishWorkAsync(CurrentBook.Id.Value, new(), null, cancellationToken);

            if (result.IsSuccess) CurrentBook.IsPublished = true;
            else return result.DomainError;
        }

        if (CurrentChapter is not null && !CurrentChapter.IsPublished)
        {
            if (CurrentChapter.Id is null) return Errors.NoChapterId;

            var result = await workService.PublishWorkAsync(CurrentChapter.Id.Value, new(), null, cancellationToken);

            if (result.IsSuccess) CurrentChapter.IsPublished = true;
            else return result.DomainError;
        }

        return Result.Success();
    }

    public void ClearChapter()
    {
        CurrentChapter = null;
    }

    public void ClearBook()
    {
        CurrentBook = null;
    }

    public void ClearStory()
    {
        CurrentStory = null;
    }

    internal void OnDirtyStateChanged(object? sender, DirtyStateChangedEventArgs args)
    {
        DirtyStateChanged?.Invoke(this, args);
    }

    public void Reset()
    {
        var wasDirty = CurrentStory?.Dirty;

        ClearChapter();
        ClearBook();
        ClearStory();

        if (wasDirty == true)
            DirtyStateChanged?.Invoke(this, new(false));
    }
}
