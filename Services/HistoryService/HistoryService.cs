using System.Collections.Concurrent;
using FraudProtection.Models;

namespace FraudProtection.Services.HistoryService;

/// <summary>
/// In-memory store of analyzed cases for the demo. Not persisted across restarts by design.
/// </summary>
public class HistoryService : IHistoryService
{
    private readonly ConcurrentDictionary<string, AnalyzedCase> _cases = new();

    public void AddCase(AnalyzedCase analyzedCase)
    {
        _cases[analyzedCase.CaseId] = analyzedCase;
    }

    public IReadOnlyList<AnalyzedCase> GetCases()
    {
        return _cases.Values
            .OrderByDescending(c => c.AnalyzedAtUtc)
            .ToList();
    }

    public AnalyzedCase? GetCase(string caseId)
    {
        return _cases.GetValueOrDefault(caseId);
    }
}
