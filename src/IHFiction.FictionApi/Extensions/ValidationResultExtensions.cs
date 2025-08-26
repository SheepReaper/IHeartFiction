using System.ComponentModel.DataAnnotations;

namespace IHFiction.FictionApi.Extensions;

internal static class ValidationResultExtensions
{
    public static Dictionary<string, string?[]> ToDictionary(this ValidationResult result)
    {
        return result.MemberNames.ToDictionary(member => member, member => new string?[] { result.ErrorMessage });
    }

    public static Dictionary<string, string[]> ToDictionary(this IEnumerable<ValidationResult> results)
    {
        return results.SelectMany(result => result.MemberNames.Select(member => (member, result.ErrorMessage ?? string.Empty)))
            .GroupBy(pair => pair.member, pair => pair.Item2)
            .ToDictionary(group => group.Key, group => group.ToArray());
    }
}
