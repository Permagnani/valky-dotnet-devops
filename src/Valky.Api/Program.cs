using Microsoft.EntityFrameworkCore;
using Valky.Api.Data;
using Valky.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// A connection string NUNCA fica no código: em produção vem da configuração do App Service
// (Connection strings > DefaultConnection), criada pelo scripts/infra-azure.sh.
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (builder.Environment.IsEnvironment("Testing"))
        options.UseInMemoryDatabase("ValkyTestes");
    else
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("banco-de-dados");

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// Versão exibida na raiz: é o que muda na "simulação de alteração no código" do vídeo.
app.MapGet("/", () => Results.Ok(new
{
    aplicacao = "Valky API",
    versao = "1.0.0",
    status = "online"
})).WithTags("Status");

app.MapHealthChecks("/health");
app.MapAlertaEndpoints();
app.MapAtualizacaoStatusEndpoints();

app.Run();

// Necessário para os testes de integração (WebApplicationFactory).
public partial class Program { }
