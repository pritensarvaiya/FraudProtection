using FraudProtection.Models;

namespace FraudProtection.Services.HistoryService;

public interface IHistoryService
{
    void AddCase(AnalyzedCase analyzedCase);
    IReadOnlyList<AnalyzedCase> GetCases();
    AnalyzedCase? GetCase(string caseId);
}
