using Microsoft.EntityFrameworkCore;
using Valky.Api.Configuration;
using Valky.Api.Contracts;
using Valky.Api.Data;
using Valky.Api.Domain;
using Valky.Api.Errors;
using Valky.Api.Models;

namespace Valky.Api.Endpoints;

public static class AtualizacaoStatusEndpoints
{
    public static void MapAtualizacaoStatusEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/atualizacoes-status")
            .WithTags("Atualizações de status")
            .RequireAuthorization();

        grupo.MapGet("/", async (int? alertaId, AppDbContext db) =>
        {
            var consulta = db.AtualizacoesStatus.AsNoTracking();
            if (alertaId is not null) consulta = consulta.Where(s => s.AlertaId == alertaId);

            var lista = await consulta.OrderByDescending(s => s.Id).ToListAsync();
            return Results.Ok(lista.Select(AtualizacaoStatusResponse.De));
        })
        .RequireRateLimiting(PoliticasRateLimit.Leitura)
        .WithSummary("Lista o histórico de atualizações de status (com filtro opcional por alertaId).");

        grupo.MapGet("/{id:int}", async (int id, AppDbContext db) =>
        {
            var item = await db.AtualizacoesStatus.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
            if (item is null)
                throw new RecursoNaoEncontradoException($"Atualização de status {id} não encontrada.");

            return Results.Ok(AtualizacaoStatusResponse.De(item));
        })
        .RequireRateLimiting(PoliticasRateLimit.Leitura)
        .WithSummary("Obtém uma atualização de status pelo ID.");

        grupo.MapPost("/", async (AtualizacaoStatusRequest req, AppDbContext db) =>
        {
            var erros = RegrasAlerta.ValidarAtualizacao(req.Status, req.Responsavel, req.Observacao);
            if (erros.Count > 0)
                throw new ValidacaoException(erros);

            var alerta = await db.Alertas.FindAsync(req.AlertaId);
            if (alerta is null)
                throw new RecursoNaoEncontradoException($"Alerta {req.AlertaId} não encontrado.");

            if (!RegrasAlerta.PodeRegistrarAtualizacao(alerta))
                throw new ConflitoNegocioException("O alerta já está encerrado.");

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
        })
        .RequireRateLimiting(PoliticasRateLimit.Escrita)
        .WithSummary("Registra uma nova evolução no atendimento policial e atualiza o status do alerta.");

        grupo.MapPut("/{id:int}", async (int id, AtualizacaoStatusRequest req, AppDbContext db) =>
        {
            var item = await db.AtualizacoesStatus.FindAsync(id);
            if (item is null)
                throw new RecursoNaoEncontradoException($"Atualização de status {id} não encontrada.");

            var erros = RegrasAlerta.ValidarAtualizacao(req.Status, req.Responsavel, req.Observacao);
            if (erros.Count > 0)
                throw new ValidacaoException(erros);

            item.Status = req.Status!;
            item.Observacao = req.Observacao;
            item.Responsavel = req.Responsavel!.Trim();

            await db.SaveChangesAsync();
            return Results.Ok(AtualizacaoStatusResponse.De(item));
        })
        .RequireRateLimiting(PoliticasRateLimit.Escrita)
        .WithSummary("Atualiza os dados de um registro de histórico de status.");

        grupo.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var item = await db.AtualizacoesStatus.FindAsync(id);
            if (item is null)
                throw new RecursoNaoEncontradoException($"Atualização de status {id} não encontrada.");

            db.AtualizacoesStatus.Remove(item);
            await db.SaveChangesAsync();
            return Results.NoContent();
        })
        .RequireRateLimiting(PoliticasRateLimit.Escrita)
        .WithSummary("Remove um registro de atualização de status.");
    }
}
