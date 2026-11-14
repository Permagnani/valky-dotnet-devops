namespace Valky.Api.Endpoints;

/// <summary>
/// Respostas HTTP padronizadas compartilhadas pelos endpoints.
/// </summary>
internal static class RespostasHttp
{
    /// <summary>
    /// HTTP 400 no formato ProblemDetails, com a lista de erros de validação em "erros".
    /// </summary>
    public static IResult ErroDeValidacao(IEnumerable<string> erros) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["erros"] = erros.ToArray() });
}
