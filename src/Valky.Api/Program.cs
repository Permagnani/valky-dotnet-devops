using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Valky.Api.Configuration;
using Valky.Api.Data;
using Valky.Api.Endpoints;
using Valky.Api.Errors;

var builder = WebApplication.CreateBuilder(args);

// Provedor do banco definido por DatabaseProvider: SqlServer (Azure SQL, produção) ou Oracle.
// No ambiente "Testing" é usado o provedor InMemory.
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (builder.Environment.IsEnvironment("Testing"))
    {
        var dbName = builder.Configuration["Testing:DatabaseName"] ?? "ValkyTestes";
        options.UseInMemoryDatabase(dbName);
    }
    else
    {
        var provider = builder.Configuration.GetValue<string>("DatabaseProvider") ?? "SqlServer";
        if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
        }
        else if (string.Equals(provider, "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            options.UseInMemoryDatabase("ValkyLocalInMemory");
        }
        else
        {
            var oracleConn = builder.Configuration.GetConnectionString("OracleConnection")
                             ?? builder.Configuration.GetConnectionString("DefaultConnection");

            // IMPORTANTE: Configura compatibilidade SQL com Oracle 19c (DatabaseVersion19),
            // evitando que o EF Core 10 gere literais booleanos TRUE/FALSE exclusivos do Oracle 23ai (causadores de ORA-00904).
            options.UseOracle(oracleConn, oracleOptions =>
                oracleOptions.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19));
        }
    }
});

// Tratamento Global de Erros (Clean Code / RFC 7807 ProblemDetails)
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Autenticação por JWT + Autorização + Documentação Interativa no Swagger
builder.Services.AdicionarAutenticacaoJwtValky(builder.Configuration);
builder.Services.AdicionarSwaggerComJwtValky();

// Rate Limiting individualizado por política em cada endpoint
builder.Services.AdicionarRateLimitingValky(builder.Configuration);

// HealthChecks verificando conectividade da API e do banco via EF Core
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(
        name: "banco-de-dados",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["db", "efcore"]);

var app = builder.Build();

app.UseExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// Versão exibida na raiz
app.MapGet("/", () => Results.Ok(new
{
    aplicacao = "Valky API",
    versao = "2.0.0",
    bancoDeDados = app.Configuration.GetValue<string>("DatabaseProvider") ?? "SqlServer",
    status = "online"
}))
.AllowAnonymous()
.RequireRateLimiting(PoliticasRateLimit.Infraestrutura)
.WithTags("Status")
.WithSummary("Retorna o status operacional e a versão da API.");

// Endpoint de HealthCheck com relatório estruturado em JSON
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status.ToString(),
            duracaoTotalMs = report.TotalDuration.TotalMilliseconds,
            timestamp = DateTime.UtcNow,
            verificacoes = report.Entries.Select(e => new
            {
                componente = e.Key,
                status = e.Value.Status.ToString(),
                duracaoMs = e.Value.Duration.TotalMilliseconds,
                descricao = e.Value.Description,
                tags = e.Value.Tags
            })
        };
        await context.Response.WriteAsJsonAsync(payload);
    }
})
.AllowAnonymous()
.RequireRateLimiting(PoliticasRateLimit.Infraestrutura)
.WithTags("HealthCheck");

app.MapAuthEndpoints();
app.MapUsuarioEndpoints();
app.MapAlertaEndpoints();
app.MapAtualizacaoStatusEndpoints();

app.Run();

// Necessário para os testes funcionais e de integração (WebApplicationFactory).
public partial class Program { }
