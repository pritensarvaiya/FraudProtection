using System.Net;
using System.Net.Sockets;
using System.Text.Json.Serialization;
using FraudProtection.AiModel;
using FraudProtection.Services.FraudAnalysisService;
using FraudProtection.Services.HistoryService;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

var builder = WebApplication.CreateBuilder(args);

// Railway (and similar PaaS hosts) assign the listen port via the PORT env var at runtime.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new() { Title = "ScamShield AI API", Version = "v1" }));

var allowedOrigins = builder.Configuration["Cors:AllowedOrigins"]
    ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? [];

builder.Services.AddCors(options =>
    options.AddPolicy("AllowFrontend", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            // No Cors:AllowedOrigins configured (e.g. local dev) - fall back to permissive CORS.
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
    }));

builder.Services.AddSingleton<IHistoryService, HistoryService>();

// On this network, outbound IPv6 to Google's endpoints is silently blackholed, and .NET's
// Happy-Eyeballs fallback to IPv4 does not kick in fast enough — the connection just hangs
// until HttpClient.Timeout elapses (curl, which tries IPv4 in parallel, is unaffected). The
// ConnectCallback below forces every connection through this handler to resolve and dial IPv4
// only, which is the actual fix; do not remove it without re-testing on this network.
builder.Services.AddHttpClient("Gemini")
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        ConnectCallback = async (context, cancellationToken) =>
        {
            var entry = await Dns.GetHostEntryAsync(context.DnsEndPoint.Host, AddressFamily.InterNetwork, cancellationToken);
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(entry.AddressList[0], context.DnsEndPoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
    });

builder.Services.AddSingleton<IChatCompletionService>(sp =>
{
    var apiKey = builder.Configuration["Gemini:ApiKey"]
        ?? throw new InvalidOperationException("Gemini:ApiKey is not configured. Set it via appsettings.Development.json or user-secrets.");
    var model = builder.Configuration["Gemini:Model"] ?? "gemini-2.0-flash";
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("Gemini");
    httpClient.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
    return new GeminiChatCompletionService(apiKey, model, httpClient);
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

// In Development the SPA calls the API over plain HTTP; redirecting to the
// self-signed HTTPS port makes those fetch() calls fail on the certificate.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAuthorization();
app.MapControllers();

app.Run();
