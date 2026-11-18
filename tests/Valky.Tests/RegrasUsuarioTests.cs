using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using Valky.Api.Domain;
using Valky.Api.Errors;
using Valky.Api.Models;
using Valky.Api.Security;

namespace Valky.Tests;

/// <summary>
/// Testes unitários das regras de negócio de Usuários, Rede de Confiança e Criptografia (sem banco e sem HTTP).
/// </summary>
public class RegrasUsuarioTests
{
    private const string CpfValidoExemplo = "52998224725";

    [Fact]
    public void CadastroUsuariaValido_NaoRetornaErros()
    {
        var erros = RegrasUsuario.ValidarCadastroUsuario(
            nome: "Maria da Silva",
            email: "maria.silva@email.com",
            cpf: CpfValidoExemplo,
            telefone: "(11) 98765-4321",
            senha: " V@lky2026Strong",
            perfil: PerfilUsuario.Usuaria,
            codigoBo: "BO-2026-00981",
            pinDisfarce: "1901");

        Assert.Empty(erros);
    }

    [Theory]
    [InlineData("52998224725", true)]
    [InlineData("529.982.247-25", true)]
    [InlineData("11111111111", false)]
    [InlineData("00000000000", false)]
    [InlineData("52998224700", false)]
    [InlineData("12345", false)]
    [InlineData("", false)]
    public void ValidacaoCpf_CalculaDigitosVerificadoresCorretamente(string cpf, bool esperado)
    {
        Assert.Equal(esperado, RegrasUsuario.CpfValido(cpf));
    }

    [Theory]
    [InlineData("curta1A")]
    [InlineData("semmaiuscula123")]
    [InlineData("SEMMINUSCULA123")]
    [InlineData("SemNumerosAqui")]
    [InlineData("")]
    public void SenhaFraca_RetornaErrosDeValidacao(string senhaFraca)
    {
        var erros = RegrasUsuario.ValidarSenhaForte(senhaFraca);
        Assert.NotEmpty(erros);
    }

    [Fact]
    public void UsuariaSemCodigoBo_CadastroPermitido_BoFicaComoPendencia()
    {
        var erros = RegrasUsuario.ValidarCadastroUsuario(
            nome: "Ana Souza",
            email: "ana@email.com",
            cpf: CpfValidoExemplo,
            telefone: "11999998888",
            senha: "ValkyPassword123",
            perfil: PerfilUsuario.Usuaria,
            codigoBo: null,
            pinDisfarce: "2580");

        Assert.Empty(erros);

        var usuaria = new Usuario
        {
            Perfil = PerfilUsuario.Usuaria,
            CodigoBo = null,
            PinDisfarce = "2580",
            Ativo = true
        };
        var pendencias = RegrasUsuario.AvaliarPendenciasConfiguracaoAlerta(usuaria, totalContatosConfianca: 0);

        Assert.Contains(pendencias, p => p.Contains("Boletim de Ocorrência (BO)"));
    }

    [Fact]
    public void PoliciaSemCodigoBo_NaoRetornaErroDeBo()
    {
        var erros = RegrasUsuario.ValidarCadastroUsuario(
            nome: "Agente Central 190",
            email: "central190@policia.sp.gov.br",
            cpf: CpfValidoExemplo,
            telefone: "1133334444",
            senha: "PoliciaPassword123",
            perfil: PerfilUsuario.Policia,
            codigoBo: null,
            pinDisfarce: null);

        Assert.Empty(erros);
    }

    [Theory]
    [InlineData("1901", true)]
    [InlineData("12345678", true)]
    [InlineData("123", false)]
    [InlineData("123456789", false)]
    [InlineData("12a4", false)]
    public void PinDisfarceCalculadora_ValidaApenas4A8DigitosNumericos(string pin, bool esperado)
    {
        Assert.Equal(esperado, RegrasUsuario.PinDisfarceValido(pin));
    }

    [Fact]
    public void ContatoConfianca_SemNenhumCanalDeNotificacao_RetornaErro()
    {
        var erros = RegrasUsuario.ValidarContatoConfianca(
            nome: "Helena Silva",
            telefone: "11988887777",
            email: "helena@email.com",
            parentesco: "Mãe",
            notificarPorSms: false,
            notificarPorApp: false);

        Assert.Contains(erros, e => e.Contains("notificação"));
    }

    [Fact]
    public void ProntidaoAlertaSilencioso_IdentificaPendenciasEValidaQuandoCompleta()
    {
        var usuariaIncompleta = new Usuario
        {
            Perfil = PerfilUsuario.Usuaria,
            Ativo = true,
            CodigoBo = "BO-2026-100",
            PinDisfarce = null,
            PermissaoMicrofone = false,
            PermissaoLocalizacao = true
        };

        var pendencias = RegrasUsuario.AvaliarPendenciasConfiguracaoAlerta(usuariaIncompleta, totalContatosConfianca: 0);
        Assert.Equal(3, pendencias.Count); // Falta PIN, Microfone e Contato

        var usuariaCompleta = new Usuario
        {
            Perfil = PerfilUsuario.Usuaria,
            Ativo = true,
            CodigoBo = "BO-2026-100",
            PinDisfarce = "1901",
            PermissaoMicrofone = true,
            PermissaoLocalizacao = true
        };

        var pendenciasCompleta = RegrasUsuario.AvaliarPendenciasConfiguracaoAlerta(usuariaCompleta, totalContatosConfianca: 1);
        Assert.Empty(pendenciasCompleta);
    }

    [Fact]
    public void SenhaHasher_GeraHashComSaltAleatorio_E_VerificaCorretamente()
    {
        var hash1 = SenhaHasher.GerarHash("MinhaSenhaSegura123");
        var hash2 = SenhaHasher.GerarHash("MinhaSenhaSegura123");

        Assert.NotEqual(hash1, hash2); // Salts diferentes geram hashes diferentes
        Assert.True(SenhaHasher.VerificarSenha("MinhaSenhaSegura123", hash1));
        Assert.True(SenhaHasher.VerificarSenha("MinhaSenhaSegura123", hash2));
        Assert.False(SenhaHasher.VerificarSenha("SenhaErrada123", hash1));
    }

    [Fact]
    public void MapeadorErros_OracleException_MapeiaPara503ComMensagemLimpa()
    {
        OracleException? oracleException = null;
        try
        {
            using var conn = new OracleConnection("Data Source=localhost;User Id=;Password=;");
            conn.Open();
        }
        catch (OracleException ex)
        {
            oracleException = ex;
        }

        Assert.NotNull(oracleException);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/auth/registrar";

        var problem = MapeadorErros.CriarProblemDetailsDeExcecao(oracleException!, httpContext);

        // O código ORA retornado depende do ambiente (ex.: ORA-01005 ou ORA-50201 sem servidor local);
        // o que se valida é o mapeamento para 503 com códigos do Oracle e sem links do driver.
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.Status);
        Assert.StartsWith("ORACLE_", (string)problem.Extensions["codigoErro"]!);
        Assert.StartsWith("ORA-", (string)problem.Extensions["codigoOracle"]!);
        Assert.NotNull(problem.Extensions["mensagem"]);
        Assert.DoesNotContain("https://docs.oracle.com", problem.Detail);
    }

    [Fact]
    public void MapeadorErros_JsonException_E_DbUpdateConcurrency_MapeiamCorretamenteSemMensagemNula()
    {
        var httpContext = new DefaultHttpContext();

        var problemJson = MapeadorErros.CriarProblemDetailsDeExcecao(new JsonException("JSON inválido"), httpContext);
        Assert.Equal(StatusCodes.Status400BadRequest, problemJson.Status);
        Assert.Equal("REQUISICAO_JSON_INVALIDO", problemJson.Extensions["codigoErro"]);
        Assert.NotNull(problemJson.Extensions["mensagem"]);

        var problemConcorrencia = MapeadorErros.CriarProblemDetailsDeExcecao(new DbUpdateConcurrencyException("Conflito"), httpContext);
        Assert.Equal(StatusCodes.Status409Conflict, problemConcorrencia.Status);
        Assert.Equal("CONFLITO_CONCORRENCIA_BANCO", problemConcorrencia.Extensions["codigoErro"]);
        Assert.NotNull(problemConcorrencia.Extensions["mensagem"]);
    }
}
