using System.Text.Json.Serialization;
using FraudProtection.AiModel;
using FraudProtection.Services.FraudAnalysisService;
using FraudProtection.Services.HistoryService;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new() { Title = "ScamShield AI API", Version = "v1" }));

builder.Services.AddCors(options =>
    options.AddPolicy("AllowFrontend", policy => policy
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.AddSingleton<IHistoryService, HistoryService>();

builder.Services.AddSingleton<IChatCompletionService>(_ =>
{
    var apiKey = builder.Configuration["Gemini:ApiKey"]
        ?? throw new InvalidOperationException("Gemini:ApiKey is not configured. Set it via appsettings.Development.json or user-secrets.");
    var model = builder.Configuration["Gemini:Model"] ?? "gemini-2.0-flash";
    return new GeminiChatCompletionService(apiKey, model);
});

builder.Services.AddScoped<FraudProtection.Plugins.FraudAnalysisPlugin.FraudAnalysisPlugin>();

builder.Services.AddScoped<Kernel>(sp =>
{
    var kernelBuilder = Kernel.CreateBuilder();
    kernelBuilder.Services.AddSingleton(sp.GetRequiredService<IChatCompletionService>());

    var fraudPlugin = sp.GetRequiredService<FraudProtection.Plugins.FraudAnalysisPlugin.FraudAnalysisPlugin>();
    kernelBuilder.Plugins.AddFromObject(fraudPlugin, "FraudAnalysis");

    return kernelBuilder.Build();
});

builder.Services.AddScoped<IFraudAnalysisService, FraudAnalysisService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ScamShield AI API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("AllowFrontend");
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
