using Valky.Api.Domain;
using Valky.Api.Models;

namespace Valky.Tests;

/// <summary>Testes unitários das regras de negócio (sem banco, sem HTTP).</summary>
public class RegrasAlertaTests
{
    [Fact]
    public void AlertaValido_NaoRetornaErros()
    {
        var erros = RegrasAlerta.ValidarAlerta("BO-2026-0001", -23.5505m, -46.6333m, null);
        Assert.Empty(erros);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AlertaSemBo_RetornaErro(string? bo)
    {
        var erros = RegrasAlerta.ValidarAlerta(bo, -23.55m, -46.63m, null);
        Assert.Contains(erros, e => e.Contains("BO"));
    }

    [Theory]
    [InlineData(-91.0, 0.0)]
    [InlineData(91.0, 0.0)]
    [InlineData(0.0, -181.0)]
    [InlineData(0.0, 181.0)]
    public void CoordenadasForaDoIntervalo_RetornaErro(double lat, double lon)
    {
        var erros = RegrasAlerta.ValidarAlerta("BO-1", (decimal)lat, (decimal)lon, null);
        Assert.NotEmpty(erros);
    }

    [Fact]
    public void StatusInexistente_RetornaErro()
    {
        var erros = RegrasAlerta.ValidarAlerta("BO-1", 0, 0, "QUALQUER");
        Assert.Contains(erros, e => e.Contains("Status"));
    }

    [Theory]
    [InlineData(StatusAlerta.Aberto, true)]
    [InlineData(StatusAlerta.ViaturaACaminho, true)]
    [InlineData(StatusAlerta.NoLocal, true)]
    [InlineData(StatusAlerta.Encerrado, false)]
    public void SoPermiteAtualizacao_SeAlertaNaoEstiverEncerrado(string status, bool esperado)
    {
        var alerta = new Alerta { Status = status };
        Assert.Equal(esperado, RegrasAlerta.PodeRegistrarAtualizacao(alerta));
    }

    [Fact]
    public void AtualizacaoSemResponsavel_RetornaErro()
    {
        var erros = RegrasAlerta.ValidarAtualizacao(StatusAlerta.NoLocal, "", null);
        Assert.Contains(erros, e => e.Contains("responsável"));
    }
}
