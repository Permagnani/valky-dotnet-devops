using Microsoft.EntityFrameworkCore;
using Valky.Api.Contracts;
using Valky.Api.Data;
using Valky.Api.Domain;
using Valky.Api.Models;

namespace Valky.Api.Endpoints;

public static class AlertaEndpoints
{
    public static void MapAlertaEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/alertas").WithTags("Alertas");

        grupo.MapGet("/", async (AppDbContext db) =>
        {
            var alertas = await db.Alertas.AsNoTracking().OrderByDescending(a => a.Id).ToListAsync();
            return Results.Ok(alertas.Select(AlertaResponse.De));
        });

        grupo.MapGet("/{id:int}", async (int id, AppDbContext db) =>
        {
            var alerta = await db.Alertas.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
            return alerta is null ? Results.NotFound() : Results.Ok(AlertaResponse.De(alerta));
        });

        grupo.MapPost("/", async (AlertaRequest req, AppDbContext db) =>
        {
            var erros = RegrasAlerta.ValidarAlerta(req.CodigoBo, req.Latitude, req.Longitude, req.Status);
            if (erros.Count > 0)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["erros"] = erros.ToArray() });

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
        });

        grupo.MapPut("/{id:int}", async (int id, AlertaRequest req, AppDbContext db) =>
        {
            var alerta = await db.Alertas.FindAsync(id);
            if (alerta is null) return Results.NotFound();

            var erros = RegrasAlerta.ValidarAlerta(req.CodigoBo, req.Latitude, req.Longitude, req.Status);
            if (erros.Count > 0)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["erros"] = erros.ToArray() });

            alerta.CodigoBo = req.CodigoBo!.Trim();
            alerta.Latitude = req.Latitude;
            alerta.Longitude = req.Longitude;
            alerta.Status = req.Status ?? alerta.Status;
            alerta.AtualizadoEm = DateTime.UtcNow;

            await db.SaveChangesAsync();
            return Results.Ok(AlertaResponse.De(alerta));
        });

        grupo.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var alerta = await db.Alertas.FindAsync(id);
            if (alerta is null) return Results.NotFound();

            db.Alertas.Remove(alerta);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
