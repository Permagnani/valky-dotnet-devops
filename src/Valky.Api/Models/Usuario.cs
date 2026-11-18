namespace Valky.Api.Models;

/// <summary>
/// Representa um usuário cadastrado na plataforma Valky (Usuária, Contato de Confiança ou agente da Polícia).
/// Tabela Oracle: T_VALKY_USUARIOS / Usuarios
/// </summary>
public class Usuario
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public string SenhaHash { get; set; } = string.Empty;
    public string Perfil { get; set; } = PerfilUsuario.Usuaria;


    public string? CodigoBo { get; set; }


    public string? PinDisfarce { get; set; }

    public bool PossuiMedidaProtetiva { get; set; }


    public bool PermissaoMicrofone { get; set; }

    public bool PermissaoLocalizacao { get; set; }

    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }

    public List<ContatoConfianca> ContatosConfianca { get; set; } = new();
}
