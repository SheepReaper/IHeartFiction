using System.Net;
using Microsoft.AspNetCore.Components;

namespace IHFiction.WebClient.Infrastructure;

internal sealed class VisitorContext
{
    // InteractiveServer persistence is encrypted/authenticated by ASP.NET Core Data Protection.
    [PersistentState]
    public string? ClientIp { get; set; }

    public static string? Normalize(IPAddress? address) =>
        (address?.IsIPv4MappedToIPv6 == true ? address.MapToIPv4() : address)?.ToString();
}
