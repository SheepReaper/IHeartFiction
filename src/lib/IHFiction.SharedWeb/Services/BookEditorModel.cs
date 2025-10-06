using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

using static IHFiction.SharedWeb.Services.StoryEditorService;

namespace IHFiction.SharedWeb.Services;

public class BookEditorModel : INotifyPropertyChanged
{
    internal event EventHandler<DirtyStateChangedEventArgs>? DirtyStateChanged;
    public event PropertyChangedEventHandler? PropertyChanged;
    private bool _suppressDirty = true;

    public Ulid? Id { get; set; }
    public int Order { get; set; }
    public ObservableCollection<ChapterEditorModel> Chapters { get; } = [];
    public bool IsPublished { get; set; }

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

    public bool Dirty
    {
        get;
        set
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

    private void ChaptersPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        OnPropertyChanged();
    }

    private BookEditorModel(Ulid? id, string? title, string? description, int order, bool isPublished)
    {
        Id = id;
        Order = order;
        Title = title;
        Description = description;
        IsPublished = isPublished;

        PropertyChanged += (sender, args) =>
        {
            if (_suppressDirty) return;
            if (!Dirty) Dirty = true;
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

    public static BookEditorModel Create(
        Ulid id,
        string? title,
        string? description,
        int order = 0,
        bool isPublished = false) => new(id, title, description, order, isPublished) { _suppressDirty = false };

    public static BookEditorModel Create() => new(null, null, null, 0, false) { _suppressDirty = false };

    public IAsyncDisposable SuppressDirty() => new SuppressDirtyContext(this);

    private sealed class SuppressDirtyContext : IAsyncDisposable
    {
        private readonly BookEditorModel _model;
        private readonly bool _prevInitState;

        public SuppressDirtyContext(BookEditorModel model)
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
