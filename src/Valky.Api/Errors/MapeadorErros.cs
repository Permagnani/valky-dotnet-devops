using System.Data.Common;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;

namespace Valky.Api.Errors;

/// <summary>
/// Centraliza a criação de respostas padronizadas de erro seguindo a RFC 7807 (ProblemDetails).
/// Mapeia exceções de domínio, erros do banco Oracle (ORA-*), falhas do EF Core e erros de serialização JSON.
/// </summary>
public static class MapeadorErros
{
    public static IResult Validacao(IEnumerable<string> erros, HttpContext? httpContext = null)
    {
        var listaErros = erros.ToArray();
        var detalhe = listaErros.FirstOrDefault() ?? "Verifique os campos informados.";
        var extensoes = CriarExtensoesPadrao("VALIDACAO_INVALIDA", detalhe, httpContext);

        return Results.ValidationProblem(
            errors: new Dictionary<string, string[]> { ["erros"] = listaErros },
            detail: detalhe,
            title: "Erro de validação",
            statusCode: StatusCodes.Status400BadRequest,
            type: "https://tools.ietf.org/html/rfc7807#section-3.1",
            extensions: extensoes);
    }

    public static IResult NaoAutorizado(string detalhe = "Credenciais inválidas ou token ausente.", HttpContext? httpContext = null) =>
        CriarProblema(
            statusCode: StatusCodes.Status401Unauthorized,
            titulo: "Não autorizado",
            detalhe: detalhe,
            codigoErro: "NAO_AUTORIZADO",
            httpContext: httpContext);

    public static IResult AcessoNegado(string detalhe = "Permissão insuficiente para acessar este recurso.", HttpContext? httpContext = null) =>
        CriarProblema(
            statusCode: StatusCodes.Status403Forbidden,
            titulo: "Acesso negado",
            detalhe: detalhe,
            codigoErro: "ACESSO_NEGADO",
            httpContext: httpContext);

    public static IResult NaoEncontrado(string detalhe, HttpContext? httpContext = null) =>
        CriarProblema(
            statusCode: StatusCodes.Status404NotFound,
            titulo: "Recurso não encontrado",
            detalhe: detalhe,
            codigoErro: "RECURSO_NAO_ENCONTRADO",
            httpContext: httpContext);

    public static IResult Conflito(string detalhe, HttpContext? httpContext = null) =>
        CriarProblema(
            statusCode: StatusCodes.Status409Conflict,
            titulo: "Conflito de regra de negócio",
            detalhe: detalhe,
            codigoErro: "CONFLITO_NEGOCIO",
            httpContext: httpContext);

    public static IResult LimiteRequisicoes(string detalhe = "Limite de requisições excedido. Tente novamente em instantes.", HttpContext? httpContext = null) =>
        CriarProblema(
            statusCode: StatusCodes.Status429TooManyRequests,
            titulo: "Muitas requisições (Rate Limit)",
            detalhe: detalhe,
            codigoErro: "LIMITE_REQUISICOES_EXCEDIDO",
            httpContext: httpContext);

    public static ProblemDetails CriarProblemDetailsDeExcecao(
        Exception exception,
        HttpContext httpContext,
        bool incluirDetalhesInternos = false)
    {
        // 1. Exceções de Validação de Domínio (400 Bad Request)
        if (exception is ValidacaoException validacaoEx)
        {
            var detalhe = validacaoEx.Erros.FirstOrDefault() ?? validacaoEx.Message;
            var problem = new HttpValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["erros"] = validacaoEx.Erros.ToArray()
            })
            {
                Status = validacaoEx.StatusCode,
                Title = validacaoEx.Titulo,
                Detail = detalhe,
                Type = "https://httpstatuses.com/400",
                Instance = httpContext.Request.Path
            };
            PreencherMetadados(problem.Extensions, validacaoEx.CodigoErro, detalhe, httpContext);
            return problem;
        }

        // 2. Demais Exceções de Domínio (401, 403, 404, 409)
        if (exception is DomainException domainEx)
        {
            return ConstruirProblemDetails(
                statusCode: domainEx.StatusCode,
                titulo: domainEx.Titulo,
                detalhe: domainEx.Message,
                codigoErro: domainEx.CodigoErro,
                httpContext: httpContext);
        }

        // 3. Exceções de JSON malformado ou falha de binding no corpo da requisição (400 Bad Request)
        if (exception is BadHttpRequestException or JsonException)
        {
            return ConstruirProblemDetails(
                statusCode: StatusCodes.Status400BadRequest,
                titulo: "Requisição ou JSON inválido",
                detalhe: "O corpo da requisição (JSON) está malformado ou possui campos em formato inválido.",
                codigoErro: "REQUISICAO_JSON_INVALIDO",
                httpContext: httpContext);
        }

        // 4. Exceções específicas do Oracle Database (diretas ou encapsuladas em DbUpdateException)
        var oracleEx = ExtrairOracleException(exception);
        if (oracleEx is not null)
        {
            return MapearOracleException(oracleEx, httpContext);
        }

        // 5. Exceções de Concorrência Otimista do EF Core (409 Conflict)
        if (exception is DbUpdateConcurrencyException)
        {
            return ConstruirProblemDetails(
                statusCode: StatusCodes.Status409Conflict,
                titulo: "Conflito de concorrência no banco de dados",
                detalhe: "O registro foi modificado simultaneamente por outra requisição. Recarregue os dados e tente novamente.",
                codigoErro: "CONFLITO_CONCORRENCIA_BANCO",
                httpContext: httpContext);
        }

        // 6. Exceções de Persistência do EF Core (DbUpdateException)
        if (exception is DbUpdateException dbUpdateEx)
        {
            return ConstruirProblemDetails(
                statusCode: StatusCodes.Status409Conflict,
                titulo: "Violação de integridade no banco de dados",
                detalhe: "Não foi possível salvar os dados pois violam uma restrição de unicidade ou integridade referencial.",
                codigoErro: "ERRO_INTEGRIDADE_BANCO",
                httpContext: httpContext,
                erroTecnicoOpcional: incluirDetalhesInternos ? LimparMensagemOracle(dbUpdateEx.InnerException?.Message ?? dbUpdateEx.Message) : null);
        }

        // 7. Outras falhas de conexão com Banco de Dados (DbException)
        if (exception is DbException dbEx)
        {
            return ConstruirProblemDetails(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                titulo: "Serviço de banco de dados indisponível",
                detalhe: "Não foi possível estabelecer comunicação com o banco de dados no momento.",
                codigoErro: "BANCO_DADOS_INDISPONIVEL",
                httpContext: httpContext,
                erroTecnicoOpcional: incluirDetalhesInternos ? LimparMensagemOracle(dbEx.Message) : null);
        }

        // 8. Timeout ou cancelamento de requisição (504 Gateway Timeout)
        if (exception is TimeoutException or TaskCanceledException or OperationCanceledException)
        {
            return ConstruirProblemDetails(
                statusCode: StatusCodes.Status504GatewayTimeout,
                titulo: "Tempo limite de processamento excedido",
                detalhe: "A operação demorou mais tempo do que o limite permitido ou foi cancelada.",
                codigoErro: "TEMPO_LIMITE_EXCEDIDO",
                httpContext: httpContext);
        }

        // 9. Fallback para erros internos não previstos (500 Internal Server Error)
        var detalheInterno = incluirDetalhesInternos
            ? LimparMensagemOracle(exception.Message)
            : "Ocorreu um erro inesperado ao processar a solicitação.";

        return ConstruirProblemDetails(
            statusCode: StatusCodes.Status500InternalServerError,
            titulo: "Erro interno no servidor",
            detalhe: detalheInterno,
            codigoErro: "ERRO_INTERNO",
            httpContext: httpContext);
    }

    /// <summary>
    /// Traduz códigos oficiais ORA-* do Oracle Database para respostas HTTP semânticas e amigáveis.
    /// </summary>
    private static ProblemDetails MapearOracleException(OracleException oracleEx, HttpContext httpContext)
    {
        var (statusCode, titulo, codigoErro, detalhe) = oracleEx.Number switch
        {
            // ORA-00001: unique constraint violated
            1 => (
                StatusCodes.Status409Conflict,
                "Registro duplicado no banco de dados Oracle",
                "ORACLE_CONFLITO_UNICIDADE",
                "Já existe um registro cadastrado com este E-mail, CPF ou Telefone no banco de dados (ORA-00001)."
            ),

            // ORA-01005: null password given; logon denied
            1005 => (
                StatusCodes.Status503ServiceUnavailable,
                "Credenciais do Oracle Database não configuradas",
                "ORACLE_SENHA_NAO_CONFIGURADA",
                "A senha do banco de dados Oracle está vazia na configuração (ORA-01005). Configure 'User Id' e 'Password' em ConnectionStrings:OracleConnection no appsettings.json ou User Secrets."
            ),

            // ORA-01017: invalid username/password; logon denied
            1017 => (
                StatusCodes.Status503ServiceUnavailable,
                "Falha de autenticação no Oracle Database",
                "ORACLE_CREDENCIAIS_INVALIDAS",
                "O usuário ou a senha configurados para o banco de dados Oracle são inválidos (ORA-01017)."
            ),

            // ORA-00904: invalid identifier
            904 => (
                StatusCodes.Status503ServiceUnavailable,
                "Identificador ou coluna inválida no Oracle Database",
                "ORACLE_IDENTIFICADOR_INVALIDO",
                $"O Oracle não reconheceu um identificador ou coluna na consulta SQL (ORA-00904: {LimparMensagemOracle(oracleEx.Message)}). Verifique a versão de compatibilidade SQL (Oracle 19c) e a estrutura da tabela."
            ),

            // ORA-00942: table or view does not exist
            942 => (
                StatusCodes.Status503ServiceUnavailable,
                "Tabela não encontrada no schema do Oracle",
                "ORACLE_TABELA_INEXISTENTE",
                "As tabelas da aplicação ainda não foram criadas no Oracle Database (ORA-00942). Execute o script 'scripts/script-bd-oracle.sql'."
            ),

            // ORA-01400: cannot insert NULL / ORA-02290: check constraint violated / ORA-12899: value too large
            1400 or 2290 or 12899 => (
                StatusCodes.Status400BadRequest,
                "Dados incompatíveis com as restrições do banco Oracle",
                "ORACLE_RESTRICAO_DADOS",
                $"Os dados informados violam uma regra de validação ou limite de tamanho de coluna no Oracle (ORA-{oracleEx.Number:D5})."
            ),

            // ORA-02291 / ORA-02292: integrity constraint (FK) violated
            2291 or 2292 => (
                StatusCodes.Status409Conflict,
                "Violação de relacionamento no banco Oracle",
                "ORACLE_INTEGRIDADE_REFERENCIAL",
                $"A operação viola um vínculo de chave estrangeira no banco de dados Oracle (ORA-{oracleEx.Number:D5})."
            ),

            // ORA-12154 / ORA-12170 / ORA-12514 / ORA-12541 / ORA-12545: TNS / Listener / Network timeout
            12154 or 12170 or 12514 or 12541 or 12545 or 50000 => (
                StatusCodes.Status503ServiceUnavailable,
                "Servidor Oracle Database inacessível",
                "ORACLE_SERVIDOR_INACESSIVEL",
                $"Não foi possível conectar ao servidor Oracle Database (ORA-{oracleEx.Number:D5}). Verifique sua conexão de rede e a disponibilidade do servidor."
            ),

            // Demais códigos ORA-*
            _ => (
                StatusCodes.Status503ServiceUnavailable,
                "Erro de processamento no banco de dados Oracle",
                $"ORACLE_ERRO_ORA_{oracleEx.Number:D5}",
                $"O banco de dados Oracle retornou um erro ao processar a requisição (ORA-{oracleEx.Number:D5}: {LimparMensagemOracle(oracleEx.Message)})."
            )
        };

        var problem = ConstruirProblemDetails(statusCode, titulo, detalhe, codigoErro, httpContext);
        problem.Extensions["codigoOracle"] = $"ORA-{oracleEx.Number:D5}";
        return problem;
    }

    private static OracleException? ExtrairOracleException(Exception exception)
    {
        var atual = exception;
        while (atual is not null)
        {
            if (atual is OracleException oraEx)
                return oraEx;
            atual = atual.InnerException;
        }
        return null;
    }

    private static string LimparMensagemOracle(string mensagem)
    {
        if (string.IsNullOrWhiteSpace(mensagem))
            return string.Empty;

        // Remove links automáticos de ajuda anexados pelo driver ODP.NET (ex: \r\nhttps://docs.oracle.com/...)
        var linhas = mensagem
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(l => !l.TrimStart().StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                        !l.TrimStart().StartsWith("https://", StringComparison.OrdinalIgnoreCase));

        return string.Join(" ", linhas).Trim();
    }

    private static ProblemDetails ConstruirProblemDetails(
        int statusCode,
        string titulo,
        string detalhe,
        string codigoErro,
        HttpContext httpContext,
        string? erroTecnicoOpcional = null)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = titulo,
            Detail = detalhe,
            Type = $"https://httpstatuses.com/{statusCode}",
            Instance = httpContext.Request.Path
        };

        PreencherMetadados(problem.Extensions, codigoErro, detalhe, httpContext);

        if (!string.IsNullOrWhiteSpace(erroTecnicoOpcional))
            problem.Extensions["detalheTecnico"] = erroTecnicoOpcional;

        return problem;
    }

    private static IResult CriarProblema(
        int statusCode,
        string titulo,
        string detalhe,
        string codigoErro,
        HttpContext? httpContext)
    {
        var extensoes = CriarExtensoesPadrao(codigoErro, detalhe, httpContext);

        return Results.Problem(
            detail: detalhe,
            instance: httpContext?.Request.Path.Value,
            statusCode: statusCode,
            title: titulo,
            type: $"https://httpstatuses.com/{statusCode}",
            extensions: extensoes);
    }

    private static Dictionary<string, object?> CriarExtensoesPadrao(
        string codigoErro,
        string mensagem,
        HttpContext? httpContext)
    {
        var dict = new Dictionary<string, object?>
        {
            ["codigoErro"] = codigoErro,
            ["mensagem"] = mensagem,
            ["timestamp"] = DateTime.UtcNow
        };

        if (httpContext is not null)
            dict["traceId"] = httpContext.TraceIdentifier;

        return dict;
    }

    private static void PreencherMetadados(
        IDictionary<string, object?> extensions,
        string codigoErro,
        string mensagem,
        HttpContext httpContext)
    {
        extensions["codigoErro"] = codigoErro;
        extensions["mensagem"] = mensagem;
        extensions["timestamp"] = DateTime.UtcNow;
        extensions["traceId"] = httpContext.TraceIdentifier;
    }
}
