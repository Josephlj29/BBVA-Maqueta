using System.Text.Json.Serialization;
using Bbva.Reconciliation.Api.Endpoints;
using Bbva.Reconciliation.Api.OpenApi;
using Bbva.Reconciliation.Application.Movements;
using Bbva.Reconciliation.Infrastructure.DependencyInjection;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

const string AngularCorsPolicy = "angular-dev-server";

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;

    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddProblemDetails();

builder.Services.AddBbvaIntegration(builder.Configuration);
builder.Services.AddSandboxTestPlan(builder.Configuration, builder.Environment.ContentRootPath);

builder.Services.AddScoped<SearchMovementsHandler>();
builder.Services.AddScoped<GetMovementHandler>();

builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BbvaOpenApiDocumentTransformer>());

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:4200", "https://localhost:4200"];

builder.Services.AddCors(options => options.AddPolicy(
    AngularCorsPolicy,
    policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(AngularCorsPolicy);

app.MapMovementEndpoints();
app.MapDiagnosticsEndpoints(includeSignaturePreview: !app.Environment.IsProduction());

if (!app.Environment.IsProduction())
{
    app.MapSandboxEndpoints();
    app.MapPlaygroundEndpoints();

    app.MapOpenApi();

    app.MapScalarApiReference(options => options
        .WithTitle("BBVA Business Reconciliation")
        .WithTheme(ScalarTheme.BluePlanet)
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient));

    app.MapGet("/", () => Results.Redirect("/scalar/v1")).ExcludeFromDescription();
}

await app.RunAsync();

public partial class Program;
