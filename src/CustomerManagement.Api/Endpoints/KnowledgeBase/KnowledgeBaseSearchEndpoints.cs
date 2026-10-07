using CustomerManagement.Api.Contracts.KnowledgeBase;
using CustomerManagement.Api.Infrastructure.Auth;
using CustomerManagement.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CustomerManagement.Api.Endpoints.KnowledgeBase;

public static class KnowledgeBaseSearchEndpoints
{
    private const int MaxTerms = 10;
    private const int MaxResults = 50;
    private const int SnippetLength = 200;

    public static IEndpointRouteBuilder MapKnowledgeBaseSearchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/knowledge-base").WithTags("KnowledgeBaseSearch");

        group.MapGet("/search", SearchAsync)
            .RequireAuthorization("Permission:" + Permissions.KnowledgeBaseSearch)
            .WithName("SearchKnowledgeBase")
            .WithSummary("Search FAQs, help articles, and guides together. Only the first 10 search terms and the top 50 results are used.");

        return app;
    }

    private static async Task<IResult> SearchAsync(
        [FromQuery] string? q,
        CustomerManagementDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var rawQuery = q ?? string.Empty;
        var terms = rawQuery
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxTerms)
            .ToList();

        if (terms.Count == 0)
        {
            return Results.Ok(new KnowledgeBaseSearchResponse(rawQuery, []));
        }

        var faqEntries = await dbContext.FaqEntries
            .AsNoTracking()
            .Where(entry => entry.IsActive)
            .ToListAsync(cancellationToken);

        var helpArticles = await dbContext.HelpArticles
            .AsNoTracking()
            .Where(entry => entry.IsActive)
            .ToListAsync(cancellationToken);

        var guides = await dbContext.Guides
            .Include(entry => entry.Steps)
            .AsNoTracking()
            .Where(entry => entry.IsActive)
            .ToListAsync(cancellationToken);

        var ranked = new List<(int Score, KnowledgeBaseSearchResultResponse Result)>();

        foreach (var entry in faqEntries)
        {
            var score = ScoreMatch(terms, entry.Question, entry.Answer);
            if (score <= 0)
            {
                continue;
            }

            ranked.Add((score, new KnowledgeBaseSearchResultResponse(
                KnowledgeBaseContentType.Faq.ToString(), entry.Id, entry.Question, Truncate(entry.Answer))));
        }

        foreach (var entry in helpArticles)
        {
            var score = ScoreMatch(terms, entry.Title, entry.Body);
            if (score <= 0)
            {
                continue;
            }

            ranked.Add((score, new KnowledgeBaseSearchResultResponse(
                KnowledgeBaseContentType.HelpArticle.ToString(), entry.Id, entry.Title, Truncate(entry.Body))));
        }

        foreach (var entry in guides)
        {
            var orderedSteps = entry.Steps.OrderBy(step => step.StepNumber).ToList();
            var score = (CountOccurrences(entry.Title, terms) * 3)
                + orderedSteps.Sum(step => CountOccurrences(step.Instruction, terms));
            if (score <= 0)
            {
                continue;
            }

            var matchingStep = orderedSteps.FirstOrDefault(step => ContainsAnyTerm(step.Instruction, terms));
            var snippetSource = matchingStep?.Instruction ?? orderedSteps.FirstOrDefault()?.Instruction ?? string.Empty;
            ranked.Add((score, new KnowledgeBaseSearchResultResponse(
                KnowledgeBaseContentType.Guide.ToString(), entry.Id, entry.Title, Truncate(snippetSource))));
        }

        var results = ranked
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Result.Title, StringComparer.OrdinalIgnoreCase)
            .Take(MaxResults)
            .Select(item => item.Result)
            .ToList();

        return Results.Ok(new KnowledgeBaseSearchResponse(rawQuery, results));
    }

    private static int ScoreMatch(IReadOnlyList<string> terms, string titleText, string bodyText)
    {
        return (CountOccurrences(titleText, terms) * 3) + CountOccurrences(bodyText, terms);
    }

    private static int CountOccurrences(string text, IReadOnlyList<string> terms)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var total = 0;
        foreach (var term in terms)
        {
            var index = 0;
            while (true)
            {
                var found = text.IndexOf(term, index, StringComparison.OrdinalIgnoreCase);
                if (found < 0)
                {
                    break;
                }

                total++;
                index = found + term.Length;
            }
        }

        return total;
    }

    private static bool ContainsAnyTerm(string text, IReadOnlyList<string> terms)
    {
        return terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static string Truncate(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= SnippetLength)
        {
            return text;
        }

        return text[..SnippetLength] + "…";
    }
}
