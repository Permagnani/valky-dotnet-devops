namespace Valky.Api.Models;

/// <summary>
/// Pessoa da rede de confiança cadastrada pela usuária para acompanhar o andamento de alertas em tempo real.
/// Tabela Oracle: T_VALKY_CONTATOS_CONFIANCA / ContatosConfianca
/// </summary>
public class ContatoConfianca
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Parentesco { get; set; } = string.Empty;
    public bool NotificarPorSms { get; set; } = true;
    public bool NotificarPorApp { get; set; } = true;
    public DateTime CriadoEm { get; set; }

    public Usuario? Usuario { get; set; }
}
