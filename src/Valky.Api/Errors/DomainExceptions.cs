namespace Valky.Api.Errors;

/// <summary>
/// Classe base para exceções de domínio da aplicação Valky, permitindo mapeamento limpo para códigos HTTP (Clean Code).
/// </summary>
public abstract class DomainException(string mensagem, int statusCode, string titulo, string codigoErro)
    : Exception(mensagem)
{
    public int StatusCode { get; } = statusCode;
    public string Titulo { get; } = titulo;
    public string CodigoErro { get; } = codigoErro;
}

/// <summary>
/// Exceção lançada quando uma ou mais regras de validação de negócio falham (HTTP 400).
/// </summary>
public sealed class ValidacaoException : DomainException
{
    public IReadOnlyList<string> Erros { get; }

    public ValidacaoException(IEnumerable<string> erros)
        : base("Um ou mais erros de validação ocorreram.", StatusCodes.Status400BadRequest, "Erro de validação", "VALIDACAO_NEGADA")
    {
        Erros = erros.ToList().AsReadOnly();
    }

    public ValidacaoException(string erro)
        : this(new[] { erro })
    {
    }
}

/// <summary>
/// Exceção lançada quando as credenciais ou o token de autenticação são inválidos (HTTP 401).
/// </summary>
public sealed class NaoAutorizadoException(string mensagem = "Credenciais inválidas ou sessão expirada.")
    : DomainException(mensagem, StatusCodes.Status401Unauthorized, "Não autorizado", "NAO_AUTORIZADO");

/// <summary>
/// Exceção lançada quando o usuário autenticado não possui permissão para a operação (HTTP 403).
/// </summary>
public sealed class AcessoNegadoException(string mensagem = "Você não possui permissão para realizar esta operação.")
    : DomainException(mensagem, StatusCodes.Status403Forbidden, "Acesso negado", "ACESSO_NEGADO");

/// <summary>
/// Exceção lançada quando a entidade solicitada não existe no banco de dados (HTTP 404).
/// </summary>
public sealed class RecursoNaoEncontradoException(string mensagem)
    : DomainException(mensagem, StatusCodes.Status404NotFound, "Recurso não encontrado", "RECURSO_NAO_ENCONTRADO");

/// <summary>
/// Exceção lançada quando há conflito de estado ou violação de unicidade de negócio (HTTP 409).
/// </summary>
public sealed class ConflitoNegocioException(string mensagem)
    : DomainException(mensagem, StatusCodes.Status409Conflict, "Conflito de regra de negócio", "CONFLITO_NEGOCIO");
