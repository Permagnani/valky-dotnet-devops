using Valky.Api.Models;

namespace Valky.Api.Domain;

/// <summary>
/// Regras de negócio do alerta. Ficam isoladas aqui para serem testadas sem banco.
/// </summary>
public static class RegrasAlerta
{
    public static List<string> ValidarAlerta(string? codigoBo, decimal latitude, decimal longitude, string? status)
    {
        var erros = new List<string>();

        if (string.IsNullOrWhiteSpace(codigoBo))
            erros.Add("O número do BO é obrigatório.");
        else if (codigoBo.Length > 30)
            erros.Add("O número do BO deve ter no máximo 30 caracteres.");

        if (latitude < -90m || latitude > 90m)
            erros.Add("A latitude deve estar entre -90 e 90.");

        if (longitude < -180m || longitude > 180m)
            erros.Add("A longitude deve estar entre -180 e 180.");

        if (status is not null && !StatusValido(status))
            erros.Add($"Status inválido. Use: {string.Join(", ", StatusAlerta.Validos)}.");

        return erros;
    }

    public static List<string> ValidarAtualizacao(string? status, string? responsavel, string? observacao)
    {
        var erros = new List<string>();

        if (string.IsNullOrWhiteSpace(status) || !StatusValido(status))
            erros.Add($"Status inválido. Use: {string.Join(", ", StatusAlerta.Validos)}.");

        if (string.IsNullOrWhiteSpace(responsavel))
            erros.Add("O responsável é obrigatório.");
        else if (responsavel.Length > 100)
            erros.Add("O responsável deve ter no máximo 100 caracteres.");

        if (observacao is { Length: > 500 })
            erros.Add("A observação deve ter no máximo 500 caracteres.");

        return erros;
    }

    public static bool StatusValido(string status) => StatusAlerta.Validos.Contains(status);

    /// <summary>Um alerta encerrado não recebe novas atualizações de status.</summary>
    public static bool PodeRegistrarAtualizacao(Alerta alerta) => alerta.Status != StatusAlerta.Encerrado;
}
