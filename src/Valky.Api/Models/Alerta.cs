namespace Valky.Api.Models;

/// <summary>
/// Alerta disparado pela usuária. Tabela: dbo.Alertas
/// </summary>
public class Alerta
{
    public int Id { get; set; }
    public string CodigoBo { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string Status { get; set; } = StatusAlerta.Aberto;
    public DateTime CriadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }

    public List<AtualizacaoStatus> Atualizacoes { get; set; } = new();
}
