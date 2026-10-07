using System.Text.RegularExpressions;
using DnDCampaignManager.Api.DTOs;

namespace DnDCampaignManager.Api.Services.AI;

public static class MapLocationContext
{
    private static readonly (string Type, string Pattern)[] Types =
    [ ("town", @"\btowns?\b"), ("city", @"\b(?:city|cities)\b"), ("village", @"\bvillages?\b") ];

    public static string[] RequestedTypes(string question) => Types
        .Where(x => Matches(question, x.Pattern)).Select(x => x.Type).ToArray();

    public static string[] ExplicitTypes(MapLocation location) => Types
        .Where(x => Matches(location.Name + "\n" + location.Description, x.Pattern)).Select(x => x.Type).ToArray();

    public static bool Include(MapLocation location, IReadOnlyList<string> requestedTypes) =>
        requestedTypes.Count == 0 || ExplicitTypes(location).Intersect(requestedTypes).Any();

    private static bool Matches(string text, string pattern) => Regex.IsMatch(text, pattern,
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
}
