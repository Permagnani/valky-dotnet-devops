namespace Valky.Api.Models;

/// <summary>
/// Histórico de andamento do atendimento de um alerta. Tabela: dbo.AtualizacoesStatus
/// </summary>
public class AtualizacaoStatus
{
    public int Id { get; set; }
    public int AlertaId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Observacao { get; set; }
    public string Responsavel { get; set; } = string.Empty;
    public DateTime CriadoEm { get; set; }

    public Alerta? Alerta { get; set; }
}
