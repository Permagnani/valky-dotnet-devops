using Microsoft.EntityFrameworkCore;
using Valky.Api.Data;
using Valky.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Connection string lida da configuração (App Service em produção; user-secrets localmente).
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("banco-de-dados");

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// Identificação e versão da API.
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