using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

using MongoDB.Bson;

using static IHFiction.Data.Stories.Domain.StoryType;
using static IHFiction.SharedWeb.Services.StoryEditorService;

namespace IHFiction.SharedWeb.Services;

public class StoryEditorModel : INotifyPropertyChanged
{
    internal event EventHandler<DirtyStateChangedEventArgs>? DirtyStateChanged;
    public event PropertyChangedEventHandler? PropertyChanged;
    private bool _initializing = true;

    public Ulid? Id { get; set; }
    public bool IsPublished { get; set; }
    public ObjectId? ContentId { get; set; }
    public DateTime? ContentUpdatedAt { get; set; }
    public DateTime? StoryUpdatedAt { get; set; }
    public string StoryType { get; set; }

    public ObservableCollection<ChapterEditorModel> Chapters { get; } = [];
    public ObservableCollection<BookEditorModel> Books { get; } = [];

    public bool HasChapters => Chapters.Any();
    public bool HasBooks => Books.Any();
    public bool HasChaptersOrBooks => HasChapters || HasBooks;

    public string? Title
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public string? Description
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public string? Content
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public string? Note1
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public string? Note2
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public bool Dirty
    {
        get;
        set
        {
            if (value == field) return;
            field = value;
            if (_initializing) return;
            DirtyStateChanged?.Invoke(this, new(field));
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new(propertyName));
    }

    private void BookPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        OnPropertyChanged();
    }

    private void ChaptersPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        OnPropertyChanged();
    }

    private StoryEditorModel(
        string storyType,
        bool isPublished,
        Ulid? storyId,
        string? title,
        string? description,
        DateTime? storyUpdatedAt,
        ObjectId? contentId,
        string? content,
        string? note1,
        string? note2,
        DateTime? contentUpdatedAt
    )
    {
        Id = storyId;
        IsPublished = isPublished;
        ContentId = contentId;
        ContentUpdatedAt = contentUpdatedAt;
        StoryUpdatedAt = storyUpdatedAt;
        StoryType = storyType;
        Title = title;
        Description = description;
        Content = content;
        Note1 = note1;
        Note2 = note2;

        PropertyChanged += (sender, args) =>
        {
            Console.WriteLine($"Property changed: {args.PropertyName}, sender: {sender?.GetType().Name}, suppressing: {_initializing}, previously dirty: {Dirty}");
            if (_initializing) return;
            if (!Dirty) Dirty = true;
        };

        Books.CollectionChanged += (sender, args) =>
        {
            // Handle book removal
            if ((args.Action == NotifyCollectionChangedAction.Remove || args.Action == NotifyCollectionChangedAction.Replace || args.Action == NotifyCollectionChangedAction.Reset) && args.OldItems is not null)
                foreach (var oldItem in args.OldItems.Cast<BookEditorModel>()) oldItem.PropertyChanged -= BookPropertyChanged;

            // Handle new book addition
            if ((args.Action == NotifyCollectionChangedAction.Add || args.Action == NotifyCollectionChangedAction.Replace) && args.NewItems is not null)
                foreach (var newItem in args.NewItems.Cast<BookEditorModel>()) newItem.PropertyChanged += BookPropertyChanged;

            OnPropertyChanged(nameof(Books));
        };

        Chapters.CollectionChanged += (sender, args) =>
        {
            // Handle chapter removal
            if ((args.Action == NotifyCollectionChangedAction.Remove || args.Action == NotifyCollectionChangedAction.Replace || args.Action == NotifyCollectionChangedAction.Reset) && args.OldItems is not null)
                foreach (var oldItem in args.OldItems.Cast<ChapterEditorModel>()) oldItem.PropertyChanged -= ChaptersPropertyChanged;

            // Handle chapter addition
            if ((args.Action == NotifyCollectionChangedAction.Add || args.Action == NotifyCollectionChangedAction.Replace) && args.NewItems is not null)
                foreach (var newItem in args.NewItems.Cast<ChapterEditorModel>()) newItem.PropertyChanged += ChaptersPropertyChanged;

            OnPropertyChanged(nameof(Chapters));
        };
    }

    public static StoryEditorModel Create(
        string storyType,
        bool isPublished,
        Ulid storyId,
        string? title,
        string? description,
        DateTime storyUpdatedAt,
        ObjectId? contentId,
        string? content,
        string? note1,
        string? note2,
        DateTime? contentUpdatedAt
    ) => new(
        storyType,
        isPublished,
        storyId,
        title,
        description,
        storyUpdatedAt,
        contentId,
        content,
        note1,
        note2,
        contentUpdatedAt
    )
    { _initializing = false };

    public static StoryEditorModel Create(string storyType) => new(storyType, false, null, null, null, null, null, null, null, null, null) { _initializing = false };
    public static StoryEditorModel CreateSingleBody() => Create(SingleBody);
    public static StoryEditorModel CreateMultiBook() => Create(MultiBook);
    public static StoryEditorModel CreateMultiChapter() => Create(MultiChapter);

    public IAsyncDisposable SuppressDirty() => new SuppressDirtyContext(this);

    private sealed class SuppressDirtyContext : IAsyncDisposable
    {
        private readonly StoryEditorModel _model;
        private readonly bool _prevInitState;

        public SuppressDirtyContext(StoryEditorModel model)
        {
            _model = model;
            _prevInitState = model._initializing;
            model._initializing = true;
        }

        public ValueTask DisposeAsync()
        {
            _model._initializing = _prevInitState;

            return ValueTask.CompletedTask;
        }
    }
}
