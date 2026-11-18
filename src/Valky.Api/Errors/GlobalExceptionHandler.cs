using Microsoft.AspNetCore.Diagnostics;

namespace Valky.Api.Errors;

/// <summary>
/// Middleware global de tratamento de exceções (IExceptionHandler do ASP.NET Core).
/// Intercepta exceções de domínio, erros do Oracle Database (ORA-*), falhas do EF Core e de JSON,
/// convertendo-as em respostas padronizadas RFC 7807 (ProblemDetails).
/// </summary>
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problemDetails = MapeadorErros.CriarProblemDetailsDeExcecao(
            exception,
            httpContext,
            incluirDetalhesInternos: environment.IsDevelopment() || environment.IsEnvironment("Testing"));

        var statusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        var codigoErro = problemDetails.Extensions.TryGetValue("codigoErro", out var cod)
            ? cod?.ToString() ?? "ERRO_DESCONHECIDO"
            : "ERRO_DESCONHECIDO";

        if (statusCode < 500)
        {
            logger.LogWarning(
                "Erro de negócio/requisição mapeado [{CodigoErro} - HTTP {StatusCode}] em {Method} {Path}: {Detalhe}",
                codigoErro,
                statusCode,
                httpContext.Request.Method,
                httpContext.Request.Path,
                problemDetails.Detail);
        }
        else
        {
            logger.LogError(
                exception,
                "Erro de infraestrutura/servidor mapeado [{CodigoErro} - HTTP {StatusCode}] em {Method} {Path}: {Detalhe}",
                codigoErro,
                statusCode,
                httpContext.Request.Method,
                httpContext.Request.Path,
                problemDetails.Detail);
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}
