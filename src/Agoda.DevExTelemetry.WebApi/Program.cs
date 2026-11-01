using System.Runtime.CompilerServices;
using Agoda.DevExTelemetry.Core.Configs;
using Agoda.DevExTelemetry.Core.Data;
using Agoda.DevExTelemetry.Core.Insights;
using Agoda.DevExTelemetry.WebApi.Mcp;
using Agoda.IoC.NetCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;
using ModelContextProtocol.Protocol;
using Serilog;

[assembly: InternalsVisibleTo("Agoda.DevExTelemetry.IntegrationTests")]
[assembly: InternalsVisibleTo("Agoda.DevExTelemetry.McpTests")]

var builder = WebApplication.CreateBuilder(args);

var assemName = typeof(Program).Assembly.GetName();
var env = builder.Environment.EnvironmentName;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddJsonFile($"appsettings.{env}.json", true)
    .AddEnvironmentVariables()
    .Build();

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(configuration)
    .Enrich.WithProperty("MachineName", System.Environment.MachineName)
    .Enrich.WithProperty("Version", assemName.Version?.ToString() ?? "0.0.0")
    .Enrich.WithProperty("AssemblyName", assemName.Name ?? "DevExTelemetry")
    .Enrich.WithProperty("EnvironmentName", env)
    .Enrich.FromLogContext());

var pgConnectionString = System.Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");
var usePostgres = !string.IsNullOrEmpty(pgConnectionString);

if (usePostgres)
{
    builder.Services.AddDbContext<TelemetryDbContext>(options =>
        options.UseNpgsql(pgConnectionString));
    builder.Services.AddScoped<ITelemetryRepository, PostgresTelemetryRepository>();
}
else
{
    builder.Services.AddDbContext<TelemetryDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
                          ?? "Data Source=devex-telemetry.db"));
    builder.Services.AddScoped<ITelemetryRepository, SqliteTelemetryRepository>();
}

builder.Services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 524_288_000; // 500 MB
});

builder.Services.AddRequestDecompression();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.AddLogging();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Agoda.DevExTelemetry", Version = "v1" });
    c.UseInlineDefinitionsForEnums();
    c.SupportNonNullableReferenceTypes();
    c.NonNullableReferenceTypesAsRequired();
    c.CustomOperationIds(e =>
        $"{e.ActionDescriptor.RouteValues["controller"]}_{e.ActionDescriptor.RouteValues["action"]}");
    c.EnableAnnotations();
});

builder.Services.TryAddSingleton(System.TimeProvider.System);

// MCP server for coding agents (issue #27): read-only insights tools over Streamable HTTP at /mcp.
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new Implementation
        {
            Name = "dx-telemetry",
            Title = "DX Telemetry",
            Version = assemName.Version?.ToString() ?? "0.0.0"
        };
        options.ServerInstructions = ServerInstructions.TEXT;
    })
    .WithHttpTransport(options => options.Stateless = true)
    .WithToolsFromAssembly(typeof(Program).Assembly)
    .WithPromptsFromAssembly(typeof(Program).Assembly);

builder.Services.AutoWireAssembly(new[]
{
    typeof(Program).Assembly,
    typeof(AssemblyInfo).Assembly
}, false);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();
    if (db.Database.ProviderName?.Contains("Sqlite") == true)
    {
        db.Database.Migrate();
    }
    else
    {
        // EnsureCreated() is used for PostgreSQL as an MVP approach since the schema is
        // simple and there are no existing PostgreSQL databases to migrate. Once PostgreSQL
        // becomes a first-class long-term provider, this should be replaced with a dedicated
        // set of PostgreSQL migrations (separate migration assembly) and db.Database.Migrate().
        db.Database.EnsureCreated();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

var forwardedHeaders = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
};
forwardedHeaders.KnownIPNetworks.Clear();
forwardedHeaders.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeaders);

app.UseRequestDecompression();
app.UseCors();
app.UseSerilogRequestLogging();
app.UseRouting();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Agoda.DevExTelemetry");
});

if (app.Services.GetRequiredService<IInsightsOptions>().McpEnabled)
    app.MapMcp("/mcp");
else
    app.Map("/mcp", () => Microsoft.AspNetCore.Http.Results.NotFound());

app.Map("/api/{**catch-all}", () => Microsoft.AspNetCore.Http.Results.NotFound());
app.MapFallbackToFile("index.html");

app.Run();
