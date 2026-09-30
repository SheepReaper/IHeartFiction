using IHFiction.SharedKernel.AgentSkills;

namespace IHFiction.FictionApi.Extensions;

internal static class AgentSkillsExtensions
{
    private const string AgentSkillsIndexPath = "/.well-known/agent-skills/index.json";
    private const string AgentSkillsBasePath = "/.well-known/agent-skills";

    public static IEndpointRouteBuilder MapAgentSkills(this IEndpointRouteBuilder builder)
    {
        builder.MapMethods(AgentSkillsIndexPath, [HttpMethods.Get, HttpMethods.Head], () =>
            Results.Json(AgentSkillsCatalog.CreateDiscoveryDocument(), contentType: "application/json"))
            .ExcludeFromDescription();

        builder.MapMethods(
            $"{AgentSkillsBasePath}/{{skillName}}/SKILL.md",
            [HttpMethods.Get, HttpMethods.Head],
            (string skillName) =>
            {
                if (string.Equals(skillName, AgentSkillsCatalog.ReadContentSkillName, StringComparison.OrdinalIgnoreCase))
                {
                    return Results.Text(AgentSkillsCatalog.ReadContentSkillMarkdown, "text/markdown; charset=utf-8");
                }

                return Results.NotFound();
            })
            .ExcludeFromDescription();

        return builder;
    }
}
