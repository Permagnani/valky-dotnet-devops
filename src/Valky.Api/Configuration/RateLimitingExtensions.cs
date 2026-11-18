using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Valky.Api.Errors;

namespace Valky.Api.Configuration;

/// <summary>
/// Nomes das políticas de Rate Limiting aplicadas em cada endpoint da API.
/// </summary>
public static class PoliticasRateLimit
{
    public const string Autenticacao = "autenticacao";
    public const string Escrita = "escrita";
    public const string Leitura = "leitura";
    public const string Infraestrutura = "infraestrutura";
}

/// <summary>
/// Configuração centralizada de Rate Limiting por endpoint usando janelas fixas particionadas por usuário/dispositivo/IP.
/// </summary>
public static class RateLimitingExtensions
{
    public static IServiceCollection AdicionarRateLimitingValky(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, cancellationToken) =>
            {
                var httpContext = context.HttpContext;
                var config = httpContext.RequestServices.GetRequiredService<IConfiguration>();
                var janelaSegundos = config.GetValue("RateLimiting:JanelaSegundos", 60);

                httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    httpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    httpContext.Response.Headers.RetryAfter = janelaSegundos.ToString(CultureInfo.InvariantCulture);
                }

                var resultado = MapeadorErros.LimiteRequisicoes(
                    "Muitas requisições realizadas em um curto intervalo. Aguarde antes de tentar novamente.",
                    httpContext);

                await resultado.ExecuteAsync(httpContext);
            };

            // Política estrita para Login e Registro (proteção contra força bruta e credential stuffing)
            options.AddPolicy(PoliticasRateLimit.Autenticacao, httpContext =>
            {
                var config = httpContext.RequestServices.GetRequiredService<IConfiguration>();
                var limiteAuth = config.GetValue("RateLimiting:LimiteAutenticacaoPorMinuto", 10);
                var janelaSegundos = config.GetValue("RateLimiting:JanelaSegundos", 60);

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: ObterChaveParticao(httpContext, PoliticasRateLimit.Autenticacao),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limiteAuth,
                        Window = TimeSpan.FromSeconds(janelaSegundos),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });

            // Política para endpoints de mutação (POST, PUT, PATCH, DELETE)
            options.AddPolicy(PoliticasRateLimit.Escrita, httpContext =>
            {
                var config = httpContext.RequestServices.GetRequiredService<IConfiguration>();
                var limiteEscrita = config.GetValue("RateLimiting:LimiteEscritaPorMinuto", 30);
                var janelaSegundos = config.GetValue("RateLimiting:JanelaSegundos", 60);

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: ObterChaveParticao(httpContext, PoliticasRateLimit.Escrita),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limiteEscrita,
                        Window = TimeSpan.FromSeconds(janelaSegundos),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });

            // Política para endpoints de consulta (GET)
            options.AddPolicy(PoliticasRateLimit.Leitura, httpContext =>
            {
                var config = httpContext.RequestServices.GetRequiredService<IConfiguration>();
                var limiteLeitura = config.GetValue("RateLimiting:LimiteLeituraPorMinuto", 60);
                var janelaSegundos = config.GetValue("RateLimiting:JanelaSegundos", 60);

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: ObterChaveParticao(httpContext, PoliticasRateLimit.Leitura),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limiteLeitura,
                        Window = TimeSpan.FromSeconds(janelaSegundos),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });

            // Política para endpoints de monitoramento (/ e /health)
            options.AddPolicy(PoliticasRateLimit.Infraestrutura, httpContext =>
            {
                var config = httpContext.RequestServices.GetRequiredService<IConfiguration>();
                var limiteInfra = config.GetValue("RateLimiting:LimiteInfraPorMinuto", 120);
                var janelaSegundos = config.GetValue("RateLimiting:JanelaSegundos", 60);

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: ObterChaveParticao(httpContext, PoliticasRateLimit.Infraestrutura),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limiteInfra,
                        Window = TimeSpan.FromSeconds(janelaSegundos),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });
        });

        return services;
    }

    /// <summary>
    /// Determina a chave de partição priorizando:
    /// 1. Identificador do Usuário Autenticado no JWT (Sub)
    /// 2. Header de dispositivo mobile (X-Client-Id) — evita bloqueio coletivo em redes móveis 4G/5G com CGNAT
    /// 3. Endereço IP remoto da requisição
    /// </summary>
    private static string ObterChaveParticao(HttpContext httpContext, string politica)
    {
        var usuarioId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(usuarioId))
            return $"{politica}:user:{usuarioId}";

        if (httpContext.Request.Headers.TryGetValue("X-Client-Id", out var clientId) &&
            !string.IsNullOrWhiteSpace(clientId))
        {
            return $"{politica}:device:{clientId.ToString().Trim()}";
        }

        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "ip-desconhecido";
        return $"{politica}:ip:{ip}";
    }
}
