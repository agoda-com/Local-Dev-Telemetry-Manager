using System.Runtime.CompilerServices;
using Agoda.DevExTelemetry.Core.Configs;
using Agoda.DevExTelemetry.Core.Data;
using Agoda.IoC.NetCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;
using Serilog;

[assembly: InternalsVisibleTo("Agoda.DevExTelemetry.IntegrationTests")]

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

builder.Services.AddDbContext<TelemetryDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
                      ?? "Data Source=devex-telemetry.db"));

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

builder.Services.AutoWireAssembly(new[]
{
    typeof(Program).Assembly,
    typeof(AssemblyInfo).Assembly
}, false);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

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

app.MapFallbackToFile("index.html");

app.Run();
