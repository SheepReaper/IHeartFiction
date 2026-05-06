using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Components.Forms;

using IHFiction.SharedKernel.Infrastructure;
using IHFiction.SharedKernel.Stories;

namespace IHFiction.SharedWeb.Services;

public partial class StoryEditorService
{
    internal sealed class DirtyStateChangedEventArgs(bool value) : EventArgs
    {
        public bool IsDirty => value;
    }

    internal sealed class StoryChangedEventArgs(StoryEditorModel? oldStory, StoryEditorModel? newStory) : EventArgs
    {
        public StoryEditorModel? OldStory => oldStory;
        public StoryEditorModel? NewStory => newStory;
    }

    internal sealed class BookChangedEventArgs(BookEditorModel? oldBook, BookEditorModel? newBook) : EventArgs
    {
        public BookEditorModel? OldBook => oldBook;
        public BookEditorModel? NewBook => newBook;
    }

    internal sealed class ChapterChangedEventArgs(ChapterEditorModel? oldChapter, ChapterEditorModel? newChapter) : EventArgs
    {
        public ChapterEditorModel? OldChapter => oldChapter;
        public ChapterEditorModel? NewChapter => newChapter;
    }

    internal event EventHandler<StoryChangedEventArgs>? StoryChanged;
    internal event EventHandler<BookChangedEventArgs>? BookChanged;
    internal event EventHandler<ChapterChangedEventArgs>? ChapterChanged;
    internal event EventHandler<DirtyStateChangedEventArgs>? DirtyStateChanged;

    internal static class Errors
    {
        public static readonly DomainError NoStoryLoaded = new("StoryEditorService.NoStoryLoaded", "No story is loaded.");
        public static readonly DomainError NoBookLoaded = new("StoryEditorService.NoBookLoaded", "No book is loaded.");
        public static readonly DomainError NoStoryId = new("StoryEditorService.NoStoryId", "Story must be saved before using this method.");
        public static readonly DomainError NoChapterId = new("StoryEditorService.NoChapterId", "Chapter must be saved before using this method.");
        public static readonly DomainError NoBookId = new("StoryEditorService.NoBookId", "Book must be saved before using this method.");
        public static readonly DomainError ChapterLoad = new("StoryEditorService.ChapterLoad", "Failed to load chapter.");
        public static readonly DomainError BookLoad = new("StoryEditorService.BookLoad", "Failed to load book.");
        public static readonly DomainError InvalidStoryType = new("StoryEditorService.InvalidStoryType", "The current story type does not support this operation.");
        public static readonly DomainError InvalidCoverType = new("StoryEditorService.InvalidCoverType", "Cover images must be JPG, PNG, or WebP.");
        public static readonly DomainError CoverTooLarge = new("StoryEditorService.CoverTooLarge", $"Cover images must be {StoryCoverRules.MaxFileSizeBytes / (1024 * 1024)} MB or smaller.");
    }

    private byte[]? PendingCoverContent { get; set; }
    private string? PendingCoverFileName { get; set; }
    private string? PendingCoverContentType { get; set; }
    private string? PendingCoverPreviewUrl { get; set; }
    private bool RemoveCoverOnSave { get; set; }

    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Blazor image bindings consume relative URLs and data URLs as strings.")]
    public string? CurrentCoverPreviewUrl => PendingCoverPreviewUrl ?? CurrentStory?.CoverImageUrl;
    public bool HasVisibleCover => !string.IsNullOrWhiteSpace(CurrentCoverPreviewUrl);

    public StoryEditorModel? CurrentStory
    {
        get;
        private set
        {
            if (field == value) return;

            var oldStory = field;
            field = value;

            ClearPendingCoverState();

            if (oldStory is not null)
                oldStory.DirtyStateChanged -= OnDirtyStateChanged;

            if (value is not null)
                value.DirtyStateChanged += OnDirtyStateChanged;

            StoryChanged?.Invoke(this, new(oldStory, value));
        }
    }

    public BookEditorModel? CurrentBook
    {
        get;
        private set
        {
            if (field == value) return;

            var oldBook = field;
            field = value;

            if (oldBook is not null)
                oldBook.DirtyStateChanged -= OnDirtyStateChanged;

            if (value is not null)
                value.DirtyStateChanged += OnDirtyStateChanged;

            BookChanged?.Invoke(this, new(oldBook, value));
        }
    }

    public ChapterEditorModel? CurrentChapter
    {
        get;
        private set
        {
            if (field == value) return;

            var oldChapter = field;
            field = value;

            if (oldChapter is not null)
                oldChapter.DirtyStateChanged -= OnDirtyStateChanged;
                
            if (value is not null)
                value.DirtyStateChanged += OnDirtyStateChanged;

            ChapterChanged?.Invoke(this, new(oldChapter, value));
        }
    }

    public async Task<Result> SetPendingCoverAsync(IBrowserFile file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (CurrentStory is null)
        {
            return Errors.NoStoryLoaded;
        }

        if (file.Size > StoryCoverRules.MaxFileSizeBytes)
        {
            return Errors.CoverTooLarge;
        }

        if (!StoryCoverRules.IsAllowedContentType(file.ContentType)
            || !StoryCoverRules.IsAllowedFileExtension(file.Name))
        {
            return Errors.InvalidCoverType;
        }

        await using var stream = file.OpenReadStream(StoryCoverRules.MaxFileSizeBytes, cancellationToken);
        await using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken);

        PendingCoverContent = memoryStream.ToArray();
        PendingCoverFileName = file.Name;
        PendingCoverContentType = file.ContentType;
        PendingCoverPreviewUrl = $"data:{file.ContentType};base64,{Convert.ToBase64String(PendingCoverContent)}";
        RemoveCoverOnSave = false;
        CurrentStory.Dirty = true;

        return Result.Success();
    }

    public void MarkCoverForRemoval()
    {
        if (CurrentStory is null)
        {
            return;
        }

        PendingCoverContent = null;
        PendingCoverFileName = null;
        PendingCoverContentType = null;
        PendingCoverPreviewUrl = null;
        RemoveCoverOnSave = true;
        CurrentStory.HasCoverImage = false;
        CurrentStory.Dirty = true;
    }

    private void ClearPendingCoverState()
    {
        PendingCoverContent = null;
        PendingCoverFileName = null;
        PendingCoverContentType = null;
        PendingCoverPreviewUrl = null;
        RemoveCoverOnSave = false;
    }
}

