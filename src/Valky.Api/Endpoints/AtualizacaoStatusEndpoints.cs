using Microsoft.EntityFrameworkCore;
using Valky.Api.Contracts;
using Valky.Api.Data;
using Valky.Api.Domain;
using Valky.Api.Models;

namespace Valky.Api.Endpoints;

public static class AtualizacaoStatusEndpoints
{
    public static void MapAtualizacaoStatusEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/atualizacoes-status").WithTags("Atualizações de status");

        grupo.MapGet("/", async (int? alertaId, AppDbContext db) =>
        {
            var consulta = db.AtualizacoesStatus.AsNoTracking();
            if (alertaId is not null) consulta = consulta.Where(s => s.AlertaId == alertaId);

            var lista = await consulta.OrderByDescending(s => s.Id).ToListAsync();
            return Results.Ok(lista.Select(AtualizacaoStatusResponse.De));
        });

        grupo.MapGet("/{id:int}", async (int id, AppDbContext db) =>
        {
            var item = await db.AtualizacoesStatus.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
            return item is null ? Results.NotFound() : Results.Ok(AtualizacaoStatusResponse.De(item));
        });

        grupo.MapPost("/", async (AtualizacaoStatusRequest req, AppDbContext db) =>
        {
            var erros = RegrasAlerta.ValidarAtualizacao(req.Status, req.Responsavel, req.Observacao);
            if (erros.Count > 0)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["erros"] = erros.ToArray() });

            var alerta = await db.Alertas.FindAsync(req.AlertaId);
            if (alerta is null)
                return Results.NotFound(new { mensagem = $"Alerta {req.AlertaId} não encontrado." });

            if (!RegrasAlerta.PodeRegistrarAtualizacao(alerta))
                return Results.Conflict(new { mensagem = "O alerta já está encerrado." });

            var agora = DateTime.UtcNow;
            var atualizacao = new AtualizacaoStatus
            {
                AlertaId = alerta.Id,
                Status = req.Status!,
                Observacao = req.Observacao,
                Responsavel = req.Responsavel!.Trim(),
                CriadoEm = agora
            };

            // Regra: o status do alerta acompanha a última atualização registrada.
            alerta.Status = atualizacao.Status;
            alerta.AtualizadoEm = agora;

            db.AtualizacoesStatus.Add(atualizacao);
            await db.SaveChangesAsync();
            return Results.Created($"/api/atualizacoes-status/{atualizacao.Id}", AtualizacaoStatusResponse.De(atualizacao));
        });

        grupo.MapPut("/{id:int}", async (int id, AtualizacaoStatusRequest req, AppDbContext db) =>
        {
            var item = await db.AtualizacoesStatus.FindAsync(id);
            if (item is null) return Results.NotFound();

            var erros = RegrasAlerta.ValidarAtualizacao(req.Status, req.Responsavel, req.Observacao);
            if (erros.Count > 0)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["erros"] = erros.ToArray() });

            item.Status = req.Status!;
            item.Observacao = req.Observacao;
            item.Responsavel = req.Responsavel!.Trim();

            await db.SaveChangesAsync();
            return Results.Ok(AtualizacaoStatusResponse.De(item));
        });

        grupo.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var item = await db.AtualizacoesStatus.FindAsync(id);
            if (item is null) return Results.NotFound();

            db.AtualizacoesStatus.Remove(item);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
