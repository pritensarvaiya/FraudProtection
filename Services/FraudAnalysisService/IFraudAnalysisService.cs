using FraudProtection.Models;

namespace FraudProtection.Services.FraudAnalysisService;

public interface IFraudAnalysisService
{
    Task<AnalyzeResponse> AnalyzeAsync(AnalyzeRequest request);
}
