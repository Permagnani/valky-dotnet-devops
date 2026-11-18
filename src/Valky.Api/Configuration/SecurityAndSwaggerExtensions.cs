using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Valky.Api.Errors;
using Valky.Api.Security;

namespace Valky.Api.Configuration;

/// <summary>
/// Configuração modular de Autenticação JWT, Autorização e Documentação Interativa no Swagger (OpenAPI).
/// </summary>
public static class SecurityAndSwaggerExtensions
{
    public static IServiceCollection AdicionarAutenticacaoJwtValky(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection(JwtSettings.SectionName);
        services.Configure<JwtSettings>(jwtSection);

        var jwtSettings = jwtSection.Get<JwtSettings>() ?? new JwtSettings();
        if (jwtSettings.ChaveSecreta.Length < 32)
            throw new InvalidOperationException(
                "Configuração Jwt:ChaveSecreta ausente ou com menos de 32 caracteres.");
        var chaveBytes = Encoding.UTF8.GetBytes(jwtSettings.ChaveSecreta);

        services.AddScoped<IJwtTokenService, JwtTokenService>();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(chaveBytes),
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Emissor,
                ValidateAudience = true,
                ValidAudience = jwtSettings.Audiencia,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            // Mapeia falhas de autenticação/autorização (401/403) para o padrão RFC 7807 ProblemDetails (Clean Code)
            options.Events = new JwtBearerEvents
            {
                OnChallenge = async context =>
                {
                    context.HandleResponse();
                    var resultado = MapeadorErros.NaoAutorizado(
                        "Token JWT ausente, inválido ou expirado. Faça login em /api/auth/login para obter um novo token.",
                        context.HttpContext);
                    await resultado.ExecuteAsync(context.HttpContext);
                },
                OnForbidden = async context =>
                {
                    var resultado = MapeadorErros.AcessoNegado(
                        "Seu perfil de usuário não possui permissão para acessar este recurso.",
                        context.HttpContext);
                    await resultado.ExecuteAsync(context.HttpContext);
                }
            };
        });

        services.AddAuthorization();

        return services;
    }

    public static IServiceCollection AdicionarSwaggerComJwtValky(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Valky API — Proteção à Mulher & Central de Emergência",
                Version = "v1.0.0",
                Description = "API RESTful (.NET 10, Entity Framework Core, Oracle Database) consumida pelo aplicativo mobile Valky. " +
                              "Inclui Cadastro de Usuárias, Rede de Contatos de Confiança, Autenticação JWT, Rate Limiting por Endpoint, " +
                              "Mapeamento de Erros (RFC 7807 ProblemDetails), HealthChecks e Central de Alertas."
            });

            var esquemaJwt = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "Insira o token JWT obtido no endpoint /api/auth/login ou /api/auth/registrar (sem precisar digitar 'Bearer ' antes).",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Reference = new OpenApiReference
                {
                    Id = JwtBearerDefaults.AuthenticationScheme,
                    Type = ReferenceType.SecurityScheme
                }
            };

            options.AddSecurityDefinition(esquemaJwt.Reference.Id, esquemaJwt);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                { esquemaJwt, Array.Empty<string>() }
            });

            // Documenta todos os códigos de erro padronizados (RFC 7807 ProblemDetails) no Swagger
            // para que nenhuma resposta de erro apareça como "Undocumented" na interface do Swagger UI.
            options.OperationFilter<PadraoRespostasProblemDetailsOperationFilter>();

            var arquivoXml = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var caminhoXml = Path.Combine(AppContext.BaseDirectory, arquivoXml);
            if (File.Exists(caminhoXml))
                options.IncludeXmlComments(caminhoXml);
        });

        return services;
    }
}

/// <summary>
/// Filtro do Swagger (OpenAPI) que registra os esquemas de resposta de erro padronizados (RFC 7807 ProblemDetails)
/// em todos os endpoints (400, 401, 403, 404, 409, 429, 500, 503).
/// </summary>
public sealed class PadraoRespostasProblemDetailsOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var problemSchema = context.SchemaGenerator.GenerateSchema(typeof(ProblemDetails), context.SchemaRepository);
        var mediaType = new Dictionary<string, OpenApiMediaType>
        {
            ["application/problem+json"] = new() { Schema = problemSchema }
        };

        AdicionarRespostaSeAusente(operation, "400", "Erro de validação de negócio ou JSON inválido (VALIDACAO_INVALIDA).", mediaType);
        AdicionarRespostaSeAusente(operation, "401", "Não autorizado — Token JWT ausente, expirado ou credenciais incorretas (NAO_AUTORIZADO).", mediaType);
        AdicionarRespostaSeAusente(operation, "403", "Acesso negado — Perfil sem permissão ou conta inativa (ACESSO_NEGADO).", mediaType);
        AdicionarRespostaSeAusente(operation, "404", "Recurso não encontrado no banco de dados (RECURSO_NAO_ENCONTRADO).", mediaType);
        AdicionarRespostaSeAusente(operation, "409", "Conflito de regra de negócio ou unicidade como CPF/E-mail duplicado (CONFLITO_NEGOCIO).", mediaType);
        AdicionarRespostaSeAusente(operation, "429", "Limite de requisições do endpoint excedido — Rate Limiting (LIMITE_REQUISICOES_EXCEDIDO).", mediaType);
        AdicionarRespostaSeAusente(operation, "500", "Erro interno mapeado no servidor (ERRO_INTERNO).", mediaType);
        AdicionarRespostaSeAusente(operation, "503", "Banco de dados Oracle indisponível ou credenciais pendentes de configuração (ORACLE_*).", mediaType);
    }

    private static void AdicionarRespostaSeAusente(
        OpenApiOperation operation,
        string statusCode,
        string descricao,
        IDictionary<string, OpenApiMediaType> content)
    {
        if (!operation.Responses.ContainsKey(statusCode))
        {
            operation.Responses[statusCode] = new OpenApiResponse
            {
                Description = descricao,
                Content = new Dictionary<string, OpenApiMediaType>(content)
            };
        }
    }
}
