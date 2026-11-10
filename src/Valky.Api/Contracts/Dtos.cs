using Valky.Api.Models;

namespace Valky.Api.Contracts;

public record AlertaRequest(string? CodigoBo, decimal Latitude, decimal Longitude, string? Status);

public record AlertaResponse(int Id, string CodigoBo, decimal Latitude, decimal Longitude,
    string Status, DateTime CriadoEm, DateTime AtualizadoEm)
{
    public static AlertaResponse De(Alerta a) =>
        new(a.Id, a.CodigoBo, a.Latitude, a.Longitude, a.Status, a.CriadoEm, a.AtualizadoEm);
}

public record AtualizacaoStatusRequest(int AlertaId, string? Status, string? Observacao, string? Responsavel);

public record AtualizacaoStatusResponse(int Id, int AlertaId, string Status, string? Observacao,
    string Responsavel, DateTime CriadoEm)
{
    public static AtualizacaoStatusResponse De(AtualizacaoStatus s) =>
        new(s.Id, s.AlertaId, s.Status, s.Observacao, s.Responsavel, s.CriadoEm);
}
