using Microsoft.EntityFrameworkCore;
using Valky.Api.Configuration;
using Valky.Api.Contracts;
using Valky.Api.Data;
using Valky.Api.Domain;
using Valky.Api.Errors;
using Valky.Api.Models;

namespace Valky.Api.Endpoints;

public static class AlertaEndpoints
{
    public static void MapAlertaEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/alertas")
            .WithTags("Alertas")
            .RequireAuthorization();

        grupo.MapGet("/", async (AppDbContext db) =>
        {
            var alertas = await db.Alertas.AsNoTracking().OrderByDescending(a => a.Id).ToListAsync();
            return Results.Ok(alertas.Select(AlertaResponse.De));
        })
        .RequireRateLimiting(PoliticasRateLimit.Leitura)
        .WithSummary("Lista todos os alertas registrados.");

        grupo.MapGet("/{id:int}", async (int id, AppDbContext db) =>
        {
            var alerta = await db.Alertas.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
            if (alerta is null)
                throw new RecursoNaoEncontradoException($"Alerta {id} não encontrado.");

            return Results.Ok(AlertaResponse.De(alerta));
        })
        .RequireRateLimiting(PoliticasRateLimit.Leitura)
        .WithSummary("Obtém os detalhes de um alerta pelo ID.");

        grupo.MapPost("/", async (AlertaRequest req, AppDbContext db) =>
        {
            var erros = RegrasAlerta.ValidarAlerta(req.CodigoBo, req.Latitude, req.Longitude, req.Status);
            if (erros.Count > 0)
                throw new ValidacaoException(erros);

            var agora = DateTime.UtcNow;
            var alerta = new Alerta
            {
                CodigoBo = req.CodigoBo!.Trim(),
                Latitude = req.Latitude,
                Longitude = req.Longitude,
                Status = req.Status ?? StatusAlerta.Aberto,
                CriadoEm = agora,
                AtualizadoEm = agora
            };

            db.Alertas.Add(alerta);
            await db.SaveChangesAsync();
            return Results.Created($"/api/alertas/{alerta.Id}", AlertaResponse.De(alerta));
        })
        .RequireRateLimiting(PoliticasRateLimit.Escrita)
        .WithSummary("Dispara um novo alerta de emergência com geolocalização e código do BO.");

        grupo.MapPut("/{id:int}", async (int id, AlertaRequest req, AppDbContext db) =>
        {
            var alerta = await db.Alertas.FindAsync(id);
            if (alerta is null)
                throw new RecursoNaoEncontradoException($"Alerta {id} não encontrado.");

            var erros = RegrasAlerta.ValidarAlerta(req.CodigoBo, req.Latitude, req.Longitude, req.Status);
            if (erros.Count > 0)
                throw new ValidacaoException(erros);

            alerta.CodigoBo = req.CodigoBo!.Trim();
            alerta.Latitude = req.Latitude;
            alerta.Longitude = req.Longitude;
            alerta.Status = req.Status ?? alerta.Status;
            alerta.AtualizadoEm = DateTime.UtcNow;

            await db.SaveChangesAsync();
            return Results.Ok(AlertaResponse.De(alerta));
        })
        .RequireRateLimiting(PoliticasRateLimit.Escrita)
        .WithSummary("Atualiza os dados ou coordenadas de um alerta existente.");

        grupo.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var alerta = await db.Alertas.FindAsync(id);
            if (alerta is null)
                throw new RecursoNaoEncontradoException($"Alerta {id} não encontrado.");

            db.Alertas.Remove(alerta);
            await db.SaveChangesAsync();
            return Results.NoContent();
        })
        .RequireRateLimiting(PoliticasRateLimit.Escrita)
        .WithSummary("Remove um registro de alerta.");
    }
}
