using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using UserManagementAPI.Data;
using UserManagementAPI.Middleware;
using UserManagementAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Only allowlisted application metadata reaches the log. Framework diagnostics
// can contain SQL parameters, token details, raw paths or exception messages.
builder.Services.AddSerilog((services, logger) => logger
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Fatal)
    .MinimumLevel.Override("System", LogEventLevel.Fatal)
    .Enrich.With<SafeLogEnricher>()
    .Filter.ByIncludingOnly(logEvent =>
        logEvent.Properties.TryGetValue("SourceContext", out var source) &&
        source is ScalarValue { Value: string name } &&
        name.StartsWith("UserManagementAPI.Middleware.", StringComparison.Ordinal))
    .ReadFrom.Services(services)
    .WriteTo.Console(new RenderedCompactJsonFormatter()), preserveStaticLogger: true);

builder.Services.AddControllers();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, context, cancellationToken) =>
{
    document.Components ??= new OpenApiComponents();
    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
    document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        Description = "A valid development JWT with an IT or HR role."
    };
    foreach (var path in document.Paths.Values)
        foreach (var operation in path.Operations?.Values.AsEnumerable() ?? Enumerable.Empty<OpenApiOperation>())
        {
            operation.Security = [new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            }];
            operation.Responses ??= new OpenApiResponses();
            operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Missing or invalid bearer token." });
            operation.Responses.TryAdd("403", new OpenApiResponse { Description = "IT or HR role required." });
        }
    return Task.CompletedTask;
}));
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// The framework reads issuer, audience and signing keys from
// Authentication:Schemes:Bearer. dotnet user-jwts supplies these in Development.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.IncludeErrorDetails = false;
        options.SaveToken = false;
        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidateAudience = true;
        options.TokenValidationParameters.ValidateLifetime = true;
        options.TokenValidationParameters.ValidateIssuerSigningKey = true;
        options.TokenValidationParameters.RequireSignedTokens = true;
        options.TokenValidationParameters.RequireExpirationTime = true;
        options.TokenValidationParameters.ValidAlgorithms = [SecurityAlgorithms.HmacSha256];
        options.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(30);
        options.TokenValidationParameters.RoleClaimType = "role";
        options.TokenValidationParameters.NameClaimType = "sub";
    });
builder.Services.AddAuthorization(options => options.AddPolicy("ManageUsers", policy =>
    policy.RequireAuthenticatedUser().RequireRole("IT", "HR")));

var app = builder.Build();

app.UseRouting();

// Wrap the exception handler so request logging sees the final response status.
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages(async context =>
{
    var http = context.HttpContext;
    await Results.Problem(statusCode: http.Response.StatusCode,
        extensions: new Dictionary<string, object?> { ["traceId"] = http.TraceIdentifier })
        .ExecuteAsync(http);
});
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapControllers();
app.Run();

public partial class Program;
