using System.ComponentModel.DataAnnotations;

namespace IHFiction.WebClient.Infrastructure;

internal sealed class WebRateLimitOptions
{
    [Range(1, 100000)] public int General { get; set; } = 300;
    [Range(1, 100000)] public int Authentication { get; set; } = 10;
    [Range(1, 100000)] public int Callbacks { get; set; } = 30;
    [Range(1, 100000)] public int Connections { get; set; } = 30;
    [Range(1, 100000)] public int Reports { get; set; } = 60;
}
