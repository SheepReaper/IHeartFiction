using System.ComponentModel;
using System.Runtime.CompilerServices;

using MongoDB.Bson;

using static IHFiction.SharedWeb.Services.StoryEditorService;

namespace IHFiction.SharedWeb.Services;

public class ChapterEditorModel : INotifyPropertyChanged
{
    internal event EventHandler<DirtyStateChangedEventArgs>? DirtyStateChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _suppressDirty = true;

    public Ulid? Id { get; init; }
    public ObjectId? ContentId { get; init; }
    public DateTime? ContentUpdatedAt { get; init; }
    public DateTime? ChapterUpdatedAt { get; init; }
    public bool IsPublished { get; set; }

    public string? Title
    {
        get; set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public string? Content
    {
        get; set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public string? Note1
    {
        get; set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public string? Note2
    {
        get; set
        {
            if (value == field) return;
            field = value;
            OnPropertyChanged();
        }
    }

    public bool Dirty
    {
        get; set
        {
            if (value == field) return;

            field = value;

            if (_suppressDirty) return;

            DirtyStateChanged?.Invoke(this, new(field));
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new(propertyName));
    }

    private ChapterEditorModel(
        Ulid? chapterId,
        string? title,
        DateTime? chapterUpdatedAt,
        ObjectId? contentId,
        string? content,
        string? note1,
        string? note2,
        DateTime? contentUpdatedAt,
        bool isPublished = false
    )
    {
        Id = chapterId;
        Title = title;
        ChapterUpdatedAt = chapterUpdatedAt;
        ContentId = contentId;
        Content = content;
        Note1 = note1;
        Note2 = note2;
        ContentUpdatedAt = contentUpdatedAt;
        IsPublished = isPublished;

        PropertyChanged += (sender, args) =>
        {
            if (_suppressDirty) return;
            if (!Dirty) Dirty = true;
        };
    }

    public static ChapterEditorModel Create(
        Ulid chapterId,
        string? title,
        DateTime? chapterUpdatedAt,
        ObjectId? contentId,
        string? content,
        string? note1,
        string? note2,
        DateTime? contentUpdatedAt,
        bool isPublished = false
    ) => new(
        chapterId,
        title,
        chapterUpdatedAt,
        contentId,
        content,
        note1,
        note2,
        contentUpdatedAt,
        isPublished
    )
    {
        _suppressDirty = false
    };

    public static ChapterEditorModel Create() => new(null, null, null, null, null, null, null, null, false)
    {
        _suppressDirty = false
    };
    
    public IAsyncDisposable SuppressDirty() => new SuppressDirtyContext(this);

    private sealed class SuppressDirtyContext : IAsyncDisposable
    {
        private readonly ChapterEditorModel _model;
        private readonly bool _prevInitState;

        public SuppressDirtyContext(ChapterEditorModel model)
        {
            _model = model;
            _prevInitState = model._suppressDirty;
            model._suppressDirty = true;
        }

        public ValueTask DisposeAsync()
        {
            _model._suppressDirty = _prevInitState;

            return ValueTask.CompletedTask;
        }
    }
}
