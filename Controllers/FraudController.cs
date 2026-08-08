using FraudProtection.Models;
using FraudProtection.Services.FraudAnalysisService;
using FraudProtection.Services.HistoryService;
using Microsoft.AspNetCore.Mvc;

namespace FraudProtection.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FraudController : ControllerBase
{
    private readonly IFraudAnalysisService _fraudAnalysisService;
    private readonly IHistoryService _historyService;
    private readonly ILogger<FraudController> _logger;

    public FraudController(
        IFraudAnalysisService fraudAnalysisService,
        IHistoryService historyService,
        ILogger<FraudController> logger)
    {
        _fraudAnalysisService = fraudAnalysisService;
        _historyService = historyService;
        _logger = logger;
    }

    [HttpPost("analyze")]
    public async Task<ActionResult<AnalyzeResponse>> Analyze([FromBody] AnalyzeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new { error = "Content is required." });
        }

        try
        {
            var result = await _fraudAnalysisService.AnalyzeAsync(request);
            return Ok(result);
        }
        catch (Exception ex) when (ex.InnerException is TimeoutException || ex is TimeoutException)
        {
            _logger.LogError(ex, "Fraud analysis timed out.");
            return StatusCode(504, new { error = "The AI provider took too long to respond. Please try again." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fraud analysis failed.");
            return StatusCode(500, new { error = "Analysis failed. Please try again." });
        }
    }

    [HttpGet("history")]
    public ActionResult<IReadOnlyList<AnalyzedCase>> GetHistory()
    {
        return Ok(_historyService.GetCases());
    }

    [HttpGet("history/{caseId}")]
    public ActionResult<AnalyzedCase> GetCase(string caseId)
    {
        var analyzedCase = _historyService.GetCase(caseId);
        return analyzedCase is null ? NotFound() : Ok(analyzedCase);
    }
}
