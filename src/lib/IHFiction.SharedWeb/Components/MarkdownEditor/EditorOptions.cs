using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Components;

namespace IHFiction.SharedWeb.Components.MarkdownEditor;

public class ViewerOptions
{
    [JsonPropertyName("el")]
    public ElementReference Element { get; set; }
    [JsonPropertyName("initialValue")]
    public string? InitialValue { get; set; } = string.Empty;
    [JsonPropertyName("events")]
    public object? Events { get; set; }
    [JsonPropertyName("plugins")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "DTO")]
    public object[]? Plugins { get; set; }
    [JsonPropertyName("extendedAutolinks")]
    public object? ExtendedAutolinks { get; set; }
    [JsonPropertyName("linkAttributes")]
    public object? LinkAttributes { get; set; }
    [JsonPropertyName("customHTMLRenderer")]
    public object? CustomHTMLRenderer { get; set; }
    [JsonPropertyName("referenceDefinition")]
    public bool? ReferenceDefinition { get; set; }
    [JsonPropertyName("customHTMLSanitizer")]
    public object? CustomHTMLSanitizer { get; set; }
    [JsonPropertyName("frontMatter")]
    public bool? FrontMatter { get; set; }
    [JsonPropertyName("usageStatistics")]
    public bool? UsageStatistics { get; set; }
    [JsonPropertyName("theme")]
    public string? Theme { get; set; } = "dark";
}

public class EditorOptions : ViewerOptions
{
    [JsonPropertyName("height")]
    public string? Height { get; set; } = "100%";
    [JsonPropertyName("minHeight")]
    public string? MinHeight { get; set; }
    [JsonPropertyName("previewStyle")]
    public string? PreviewStyle { get; set; } = "tab";
    [JsonPropertyName("initialEditType")]
    public string? InitialEditType { get; set; } = "wysiwyg";
    [JsonPropertyName("hooks")]
    public object? Hooks { get; set; }
    [JsonPropertyName("language")]
    public string? Language { get; set; } = "en-US";
    [JsonPropertyName("useCommandShortcut")]
    public bool? UseCommandShortcut { get; set; } = false;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "DTO")]
    [JsonPropertyName("toolbarItems")]
    public string[][]? ToolbarItems { get; } = [
        ["heading", "bold", "italic", "strike"],
        ["hr", "quote"],
        ["ul", "ol", "task", "indent", "outdent"],
        ["table", "image", "link"],
        ["code", "codeblock"],
        ["scrollSync"]
    ];
    [JsonPropertyName("hideModeSwitch")]
    public bool? HideModeSwitch { get; set; }
    [JsonPropertyName("placeholder")]
    public string? Placeholder { get; set; }
    [JsonPropertyName("customMarkdownRenderer")]
    public object? CustomMarkdownRenderer { get; set; }
    [JsonPropertyName("previewHighlight")]
    public bool? PreviewHighlight { get; set; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "DTO")]
    [JsonPropertyName("widgetRules")]
    public object[] WidgetRules { get; set; } = [];
    [JsonPropertyName("autofocus")]
    public bool? Autofocus { get; set; }
    [JsonPropertyName("viewer")]
    public bool? Viewer { get; set; }
}