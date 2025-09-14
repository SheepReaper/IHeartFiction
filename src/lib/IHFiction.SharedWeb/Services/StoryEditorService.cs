using System.Collections.ObjectModel;
using System.Globalization;

using IHFiction.SharedKernel.Infrastructure;

using MongoDB.Bson;

namespace IHFiction.SharedWeb.Services;

// Internal models for the editor state
public class StoryEditorModel
{
    public Ulid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
    public ObjectId ContentId { get; set; }
    public string? Content { get; set; }
    public string? Note1 { get; set; }
    public string? Note2 { get; set; }
    public DateTime? ContentUpdatedAt { get; set; }
    public DateTime StoryUpdatedAt { get; set; }
    // Avoid nested namespace error by assigning default as string literal or move using to top if needed
    public string StoryType { get; set; } = Data.Stories.Domain.StoryType.SingleBody;

    public ObservableCollection<ChapterEditorModel> Chapters { get; } = [];
    public ObservableCollection<BookEditorModel> Books { get; } = [];

    // Track original values to determine dirty state
    internal string OriginalTitle { get; set; } = string.Empty;
    internal string OriginalDescription { get; set; } = string.Empty;
    internal string? OriginalContent { get; set; }
    internal string? OriginalNote1 { get; set; }
    internal string? OriginalNote2 { get; set; }

    public bool HasChaptersOrBooks => Chapters.Any() || Books.Any();

    public bool IsDirty(StoryEditorService service)
    {
        bool metadataDirty = Title != OriginalTitle || Description != OriginalDescription;
        bool contentDirty = Content != OriginalContent || Note1 != OriginalNote1 || Note2 != OriginalNote2;

        // If story has chapters/books, its direct content cannot be dirty
        if (HasChaptersOrBooks)
        {
            contentDirty = false;
        }

        bool chaptersDirty = Chapters.Any(c => c.IsDirty(service));
        bool booksDirty = Books.Any(b => b.IsDirty(service));

        return metadataDirty || contentDirty || chaptersDirty || booksDirty;
    }
}

public class BookEditorModel
{
    public Ulid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Note1 { get; set; }
    public string? Note2 { get; set; }
    public ObservableCollection<ChapterEditorModel> Chapters { get; } = [];

    internal string OriginalTitle { get; set; } = string.Empty;
    internal string? OriginalDescription { get; set; }
    internal string? OriginalNote1 { get; set; }
    internal string? OriginalNote2 { get; set; }

    public bool IsDirty(StoryEditorService service)
    {
        return Title != OriginalTitle
            || Description != OriginalDescription
            || Note1 != OriginalNote1
            || Note2 != OriginalNote2
            || Chapters.Any(c => c.IsDirty(service));
    }
}

public class ChapterEditorModel
{
    public Ulid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public ObjectId ContentId { get; set; }
    public string? Content { get; set; }
    public string? Note1 { get; set; }
    public string? Note2 { get; set; }
    public DateTime? ContentUpdatedAt { get; set; }
    public DateTime ChapterUpdatedAt { get; set; }

    internal string OriginalTitle { get; set; } = string.Empty;
    internal string? OriginalContent { get; set; }
    internal string? OriginalNote1 { get; set; }
    internal string? OriginalNote2 { get; set; }

    public bool IsDirty(StoryEditorService service)
    {
        return Title != OriginalTitle ||
               Content != OriginalContent ||
               Note1 != OriginalNote1 ||
               Note2 != OriginalNote2;
    }
}

public sealed class DirtyStateChangedEventArgs(bool value) : EventArgs
{
    public bool IsDirty => value;
}

public sealed class StoryChangedEventArgs(StoryEditorModel? value) : EventArgs
{
    public StoryEditorModel? NewStory => value;
}

public class StoryEditorService(StoryService storyService, ChapterService chapterService, AccountService accountService, BookService bookService)
{
    // Book support for MultiBook stories

    public Task<Result<BookEditorModel>> AddNewBook()
    {
        // TODO: Implement actual API call and logic
        var newBook = new BookEditorModel {
            Id = Ulid.NewUlid(),
            Title = "New Book",
            Description = string.Empty,
            Note1 = string.Empty,
            Note2 = string.Empty
        };
        // Add to CurrentStory.Books, raise events, etc.
        CurrentStory?.Books.Add(newBook);
        NotifyStateChanged();
        return Task.FromResult(Result.Success(newBook));
    }

    public Task<Result> DeleteBook(BookEditorModel book)
    {
        // TODO: Implement actual API call and logic
        // Remove from CurrentStory.Books, raise events, etc.
        CurrentStory?.Books.Remove(book);
        NotifyStateChanged();
        return Task.FromResult(Result.Success());
    }

    private StoryEditorModel? _currentStory;
    private bool _isDirty;

    public StoryEditorModel? CurrentStory
    {
        get => _currentStory;
        private set
        {
            _currentStory = value;
            IsDirty = false; // Reset dirty state when a new story is loaded
            OnStoryChanged?.Invoke(this, new(value));
        }
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (_isDirty != value)
            {
                _isDirty = value;
                OnDirtyStateChanged?.Invoke(this, new(_isDirty));
            }
        }
    }

    public event EventHandler<StoryChangedEventArgs>? OnStoryChanged;
    public event EventHandler<DirtyStateChangedEventArgs>? OnDirtyStateChanged;

    public void CreateNewStory(string storyType)
    {
        CurrentStory = new StoryEditorModel
        {
            StoryType = storyType
        };
        IsDirty = true;
    }

    public async Task<Result<StoryEditorModel>> LoadStoryAsync(Ulid storyId)
    {
        var result = await accountService.GetCurrentAuthorStoryContentAsync(storyId.ToString());

        if (result.IsFailure)
        {
            return result.DomainError; // Convert to generic Result<T>
        }

        var apiResponse = result.Value!;

        var storyModel = new StoryEditorModel
        {
            Id = Ulid.Parse(apiResponse.StoryId, CultureInfo.InvariantCulture),
            Title = apiResponse.StoryTitle,
            Description = apiResponse.StoryDescription,
            IsPublished = apiResponse.IsPublished,
            ContentId = ObjectId.Parse(apiResponse.ContentId),
            Content = apiResponse.Content,
            Note1 = apiResponse.Note1,
            Note2 = apiResponse.Note2,
            ContentUpdatedAt = apiResponse.ContentUpdatedAt?.UtcDateTime,
            StoryUpdatedAt = apiResponse.StoryUpdatedAt.UtcDateTime,

            OriginalTitle = apiResponse.StoryTitle,
            OriginalDescription = apiResponse.StoryDescription,
            OriginalContent = apiResponse.Content,
            OriginalNote1 = apiResponse.Note1,
            OriginalNote2 = apiResponse.Note2
        };

        foreach (var chapterSummary in apiResponse.Chapters)
        {
            storyModel.Chapters.Add(new ChapterEditorModel
            {
                Id = Ulid.Parse(chapterSummary.Id, CultureInfo.InvariantCulture),
                Title = chapterSummary.Title,
                OriginalTitle = chapterSummary.Title
                // Content and notes will be lazy loaded
            });
        }

        foreach (var bookSummary in apiResponse.Books)
        {
            var bookModel = new BookEditorModel
            {
                Id = Ulid.Parse(bookSummary.Id, CultureInfo.InvariantCulture),
                Title = bookSummary.Title,
                OriginalTitle = bookSummary.Title
            };
            // Chapters within books will be lazy loaded
            storyModel.Books.Add(bookModel);
        }

        CurrentStory = storyModel;
        return storyModel;
    }

    public async Task<Result> SaveStoryAsync()
    {
        if (CurrentStory == null)
        {
            return Result.Failure(new DomainError("StoryEditorService.SaveFailed", "No story loaded to save."));
        }

        if (!CurrentStory.IsDirty(this))
        {
            return Result.Success(); // No changes to save
        }

        // Check if it's a new story
        if (CurrentStory.Id == Ulid.Empty)
        {
            var createBody = new CreateStoryBody
            {
                Title = CurrentStory.Title,
                Description = CurrentStory.Description,
                StoryType = CurrentStory.StoryType
            };
            var createResult = await storyService.CreateStoryAsync(createBody);
            if (createResult.IsFailure)
            {
                return createResult.DomainError!;
            }
            CurrentStory.Id = Ulid.Parse(createResult.Value.Id, CultureInfo.InvariantCulture);
            CurrentStory.StoryUpdatedAt = createResult.Value.UpdatedAt.UtcDateTime;
            CurrentStory.OriginalTitle = createResult.Value.Title;
            CurrentStory.OriginalDescription = createResult.Value.Description;
        }

        // Save Story Metadata (only if not a new story or if metadata changed after initial creation)
        if (CurrentStory.Title != CurrentStory.OriginalTitle || CurrentStory.Description != CurrentStory.OriginalDescription)
        {
            var updateMetadataBody = new UpdateStoryMetadataBody() { Title = CurrentStory.Title, Description = CurrentStory.Description };
            var result = await storyService.UpdateStoryMetadataAsync(CurrentStory.Id.ToString(), updateMetadataBody);
            if (result.IsFailure)
            {
                return result.DomainError!;
            }
            var updateMetadataResponse = result.Value!;
            CurrentStory.OriginalTitle = updateMetadataResponse.Title;
            CurrentStory.OriginalDescription = updateMetadataResponse.Description;
            CurrentStory.StoryUpdatedAt = updateMetadataResponse.UpdatedAt.UtcDateTime;
        }

        // Save Story Content (if applicable)
        if (!CurrentStory.HasChaptersOrBooks &&
            (CurrentStory.Content != CurrentStory.OriginalContent ||
             CurrentStory.Note1 != CurrentStory.OriginalNote1 ||
             CurrentStory.Note2 != CurrentStory.OriginalNote2))
        {
            var updateContentBody = new UpdateStoryContentBody() { Content = CurrentStory.Content, Note1 = CurrentStory.Note1, Note2 = CurrentStory.Note2 };
            var result = await storyService.UpdateStoryContentAsync(CurrentStory.Id.ToString(), updateContentBody);
            if (result.IsFailure)
            {
                return result.DomainError!;
            }
            var updateContentResponse = result.Value!;
            CurrentStory.OriginalContent = updateContentResponse.Content;
            CurrentStory.OriginalNote1 = updateContentResponse.Note1;
            CurrentStory.OriginalNote2 = updateContentResponse.Note2;
            CurrentStory.ContentId = ObjectId.Parse(updateContentResponse.ContentId);
            CurrentStory.ContentUpdatedAt = updateContentResponse.ContentUpdatedAt.UtcDateTime;
            CurrentStory.StoryUpdatedAt = updateContentResponse.StoryUpdatedAt.UtcDateTime;
        }

        // Save Chapters
        foreach (var chapter in CurrentStory.Chapters)
        {
            var saveChapterResult = await SaveChapterAsync(chapter);
            if (saveChapterResult.IsFailure) return saveChapterResult;
        }

        // Save Books and their Chapters
        foreach (var book in CurrentStory.Books)
        {
            var saveBookResult = await SaveBookAsync(book);
            if (saveBookResult.IsFailure) return saveBookResult;
        }

        IsDirty = CurrentStory.IsDirty(this); // Re-evaluate dirty state after saving
        return Result.Success();
    }

    private async Task<Result> SaveChapterAsync(ChapterEditorModel chapter)
    {
        if (!chapter.IsDirty(this))
        {
            return Result.Success();
        }

        // Save Chapter Metadata
        if (chapter.Title != chapter.OriginalTitle)
        {
            var updateMetadataBody = new UpdateChapterMetadataBody() { Title = chapter.Title };
            var result = await chapterService.UpdateChapterMetadataAsync(chapter.Id.ToString(), updateMetadataBody);
            if (result.IsFailure)
            {
                return result.DomainError!;
            }
            var updateMetadataResponse = result.Value!;
            chapter.OriginalTitle = updateMetadataResponse.ChapterTitle;
            chapter.ChapterUpdatedAt = updateMetadataResponse.UpdatedAt.UtcDateTime;
        }

        // Save Chapter Content
        if (chapter.Content != chapter.OriginalContent ||
            chapter.Note1 != chapter.OriginalNote1 ||
            chapter.Note2 != chapter.OriginalNote2)
        {
            var updateContentBody = new UpdateChapterContentBody() { Content = chapter.Content, Note1 = chapter.Note1, Note2 = chapter.Note2 };
            var result = await chapterService.UpdateChapterContentAsync(chapter.Id.ToString(), updateContentBody);
            if (result.IsFailure)
            {
                return result.DomainError!;
            }
            var updateContentResponse = result.Value!;
            chapter.OriginalContent = updateContentResponse.Content;
            chapter.OriginalNote1 = updateContentResponse.Note1;
            chapter.OriginalNote2 = updateContentResponse.Note2;
            chapter.ContentId = ObjectId.Parse(updateContentResponse.ContentId);
            chapter.ContentUpdatedAt = updateContentResponse.ContentUpdatedAt.UtcDateTime;
            chapter.ChapterUpdatedAt = updateContentResponse.ChapterUpdatedAt.UtcDateTime;
        }

        return Result.Success();
    }

    private async Task<Result> SaveBookAsync(BookEditorModel book)
    {
        if (!book.IsDirty(this))
        {
            return Result.Success();
        }

        // Save Book Metadata if changed
        if (book.Title != book.OriginalTitle || book.Description != book.OriginalDescription)
        {
            var updateBookBody = new UpdateBookMetadataBody { Title = book.Title, Description = book.Description };
            var result = await bookService.UpdateBookMetadataAsync(book.Id.ToString(), updateBookBody);
            if (result.IsFailure)
            {
                return result.DomainError!;
            }
            var updateBookResponse = result.Value!;
            book.OriginalTitle = updateBookResponse.Title;
            book.OriginalDescription = updateBookResponse.Description;
        }

        // Save Chapters within the book
        foreach (var chapter in book.Chapters)
        {
            var saveChapterResult = await SaveChapterAsync(chapter);
            if (saveChapterResult.IsFailure) return saveChapterResult;
        }

        return Result.Success();
    }

    // Placeholder for lazy loading chapter content
    public async Task<Result<ChapterEditorModel>> LoadChapterContentAsync(ChapterEditorModel chapter)
    {
        ArgumentNullException.ThrowIfNull(chapter);

        var result = await chapterService.GetCurrentAuthorChapterContentAsync(chapter.Id.ToString());

        if (result.IsFailure)
        {
            return result.DomainError; // Convert to generic Result<T>
        }

        var apiResponse = result.Value!;

        chapter.Content = apiResponse.Content;
        chapter.Note1 = apiResponse.Note1;
        chapter.Note2 = apiResponse.Note2;
        chapter.ContentId = ObjectId.Parse(apiResponse.ContentId);
        chapter.ContentUpdatedAt = apiResponse.ContentUpdatedAt.UtcDateTime;

        chapter.OriginalContent = apiResponse.Content;
        chapter.OriginalNote1 = apiResponse.Note1;
        chapter.OriginalNote2 = apiResponse.Note2;

        return chapter;
    }

    public async Task<Result<ChapterEditorModel>> AddNewChapter()
    {
        if (CurrentStory is null) return Result.Failure<ChapterEditorModel>(new DomainError("StoryEditorService.NoStoryLoaded", "No story is loaded."));

        var newChapter = new AddChapterToStoryBody { Title = $"New Chapter {CurrentStory.Chapters.Count + 1}", Content = "# New Chapter" };
        var result = await chapterService.AddChapterToStoryAsync(CurrentStory.Id.ToString(), newChapter);

        if (result.IsFailure) return result.DomainError;

        var chapterModel = new ChapterEditorModel
        {
            Id = Ulid.Parse(result.Value.ChapterId, CultureInfo.InvariantCulture),
            Title = result.Value.ChapterTitle,
            OriginalTitle = result.Value.ChapterTitle,
            Content = newChapter.Content,
            OriginalContent = newChapter.Content,
        };

        CurrentStory.Chapters.Add(chapterModel);

        return chapterModel;
    }

    public async Task<Result> DeleteChapter(ChapterEditorModel chapter)
    {
        ArgumentNullException.ThrowIfNull(chapter);

        if (CurrentStory is null) return Result.Failure(new DomainError("StoryEditorService.NoStoryLoaded", "No story is loaded."));

        var result = await chapterService.DeleteChapterAsync(chapter.Id.ToString());

        if (result.IsFailure) return result;

        CurrentStory.Chapters.Remove(chapter);

        return Result.Success();
    }

    public async Task<Result<BookEditorModel>> LoadBookChaptersAsync(BookEditorModel book)
    {
        ArgumentNullException.ThrowIfNull(book);

        var result = await bookService.GetCurrentAuthorBookContentAsync(book.Id.ToString());

        if (result.IsFailure)
        {
            return result.DomainError;
        }

        var apiResponse = result.Value!;

        // Update book metadata if present in the API response
        if (!string.IsNullOrEmpty(apiResponse.Title))
        {
            book.Title = apiResponse.Title;
            book.OriginalTitle = apiResponse.Title;
        }
        if (!string.IsNullOrEmpty(apiResponse.Description))
        {
            book.Description = apiResponse.Description;
            book.OriginalDescription = apiResponse.Description;
        }

        book.Chapters.Clear();

        foreach (var chapterSummary in apiResponse.Chapters)
        {
            book.Chapters.Add(new ChapterEditorModel
            {
                Id = Ulid.Parse(chapterSummary.Id, CultureInfo.InvariantCulture),
                Title = chapterSummary.Title,
                OriginalTitle = chapterSummary.Title,
                Content = chapterSummary.Content,
                OriginalContent = chapterSummary.Content,
                Note1 = chapterSummary.Note1,
                OriginalNote1 = chapterSummary.Note1,
                Note2 = chapterSummary.Note2,
                OriginalNote2 = chapterSummary.Note2,
                ContentId = ObjectId.Parse(chapterSummary.ContentId),
                ContentUpdatedAt = chapterSummary.UpdatedAt.UtcDateTime,
                ChapterUpdatedAt = chapterSummary.UpdatedAt.UtcDateTime
            });
        }

        return book;
    }
    
    public async Task<Result> ConvertStoryTypeAsync(string targetType)
    {
        if (CurrentStory is null) return Result.Failure(new DomainError("StoryEditorService.NoStoryLoaded", "No story is loaded."));

        var body = new ConvertStoryTypeBody { TargetType = targetType };
        var result = await storyService.ConvertStoryTypeAsync(CurrentStory.Id.ToString(), body);

        if (result.IsSuccess)
        {
            await LoadStoryAsync(CurrentStory.Id);
        }

        return result;
    }

    public async Task<Result> PublishStoryAsync()
    {
        if (CurrentStory is null) return Result.Failure(new DomainError("StoryEditorService.NoStoryLoaded", "No story is loaded."));

        var result = await storyService.PublishStoryAsync(CurrentStory.Id.ToString());

        if (result.IsSuccess)
        {
            CurrentStory.IsPublished = true;
        }

        return result;
    }

    public void NotifyStateChanged()
    {
        if (CurrentStory is not null)
        {
            IsDirty = CurrentStory.IsDirty(this);
        }
    }
}
