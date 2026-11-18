using Valky.Api.Domain;
using Valky.Api.Models;

namespace Valky.Api.Contracts;

/// <summary>
/// Dados de entrada para cadastro de um novo usuário (via /api/auth/registrar ou POST /api/usuarios).
/// </summary>
public record RegistrarUsuarioRequest(
    string? Nome,
    string? Email,
    string? Cpf,
    string? Telefone,
    string? Senha,
    string? Perfil,
    string? CodigoBo,
    string? PinDisfarce,
    bool PossuiMedidaProtetiva = false,
    bool PermissaoMicrofone = false,
    bool PermissaoLocalizacao = false);

/// <summary>
/// Dados de entrada para atualização cadastral e de permissões do aplicativo mobile (PUT /api/usuarios/{id}).
/// </summary>
public record AtualizarUsuarioRequest(
    string? Nome,
    string? Email,
    string? Telefone,
    string? Perfil,
    string? CodigoBo,
    string? PinDisfarce,
    bool PossuiMedidaProtetiva,
    bool PermissaoMicrofone,
    bool PermissaoLocalizacao,
    bool Ativo = true);

/// <summary>
/// Dados de entrada para alteração segura de senha (PATCH /api/usuarios/{id}/senha).
/// </summary>
public record AlterarSenhaRequest(string? SenhaAtual, string? NovaSenha);

/// <summary>
/// Credenciais de autenticação do usuário (POST /api/auth/login).
/// </summary>
public record LoginRequest(string? Email, string? Senha);

/// <summary>
/// Dados de entrada para cadastro ou atualização de um contato da rede de confiança.
/// </summary>
public record ContatoConfiancaRequest(
    string? Nome,
    string? Telefone,
    string? Email,
    string? Parentesco,
    bool NotificarPorSms = true,
    bool NotificarPorApp = true);

/// <summary>
/// Representação pública de um contato da rede de confiança.
/// </summary>
public record ContatoConfiancaResponse(
    int Id,
    int UsuarioId,
    string Nome,
    string Telefone,
    string? Email,
    string Parentesco,
    bool NotificarPorSms,
    bool NotificarPorApp,
    DateTime CriadoEm)
{
    public static ContatoConfiancaResponse De(ContatoConfianca c) =>
        new(
            c.Id,
            c.UsuarioId,
            c.Nome,
            c.Telefone,
            c.Email,
            c.Parentesco,
            c.NotificarPorSms,
            c.NotificarPorApp,
            c.CriadoEm);
}

/// <summary>
/// Representação pública de um usuário (nunca expõe SenhaHash nem PinDisfarce em texto aberto).
/// </summary>
public record UsuarioResponse(
    int Id,
    string Nome,
    string Email,
    string Cpf,
    string Telefone,
    string Perfil,
    string? CodigoBo,
    bool PossuiPinDisfarceConfigurado,
    bool PossuiMedidaProtetiva,
    bool PermissaoMicrofone,
    bool PermissaoLocalizacao,
    bool Ativo,
    bool ProntaParaAlertaSilencioso,
    IReadOnlyList<string> PendenciasConfiguracao,
    IReadOnlyList<ContatoConfiancaResponse> ContatosConfianca,
    DateTime CriadoEm,
    DateTime AtualizadoEm)
{
    public static UsuarioResponse De(Usuario u)
    {
        var contatos = (u.ContatosConfianca ?? [])
            .OrderBy(c => c.Id)
            .Select(ContatoConfiancaResponse.De)
            .ToList();

        var pendencias = RegrasUsuario.AvaliarPendenciasConfiguracaoAlerta(u, contatos.Count);

        return new UsuarioResponse(
            Id: u.Id,
            Nome: u.Nome,
            Email: u.Email,
            Cpf: u.Cpf,
            Telefone: u.Telefone,
            Perfil: u.Perfil,
            CodigoBo: u.CodigoBo,
            PossuiPinDisfarceConfigurado: !string.IsNullOrWhiteSpace(u.PinDisfarce),
            PossuiMedidaProtetiva: u.PossuiMedidaProtetiva,
            PermissaoMicrofone: u.PermissaoMicrofone,
            PermissaoLocalizacao: u.PermissaoLocalizacao,
            Ativo: u.Ativo,
            ProntaParaAlertaSilencioso: pendencias.Count == 0,
            PendenciasConfiguracao: pendencias,
            ContatosConfianca: contatos,
            CriadoEm: u.CriadoEm,
            AtualizadoEm: u.AtualizadoEm);
    }
}

/// <summary>
/// Resposta de autenticação contendo o token JWT assinado e os dados do usuário autenticado.
/// </summary>
public record AuthTokenResponse(
    string AccessToken,
    string TipoToken,
    DateTime ExpiraEm,
    UsuarioResponse Usuario);
