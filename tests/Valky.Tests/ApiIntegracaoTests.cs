using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Valky.Api.Contracts;
using Valky.Api.Models;

namespace Valky.Tests;

/// <summary>Sobe a API em memória (ambiente "Testing", banco InMemory) para os testes funcionais e de integração.</summary>
public class ValkyApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:ChaveSecreta", "chave-de-teste-valky-somente-para-testes-automatizados");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:LimiteAutenticacaoPorMinuto"] = "500",
                ["RateLimiting:LimiteEscritaPorMinuto"] = "500",
                ["RateLimiting:LimiteLeituraPorMinuto"] = "500",
                ["RateLimiting:LimiteInfraPorMinuto"] = "500"
            });
        });
    }
}

public class ApiIntegracaoTests(ValkyApiFactory factory) : IClassFixture<ValkyApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task AutenticarClienteAsync()
    {
        if (_client.DefaultRequestHeaders.Authorization is not null)
            return;

        var email = $"operador.{Guid.NewGuid():N}@valky.com";
        var resp = await _client.PostAsJsonAsync("/api/auth/registrar", new RegistrarUsuarioRequest(
            Nome: "Operadora Teste Alerta",
            Email: email,
            Cpf: GerarCpfValidoUnico(),
            Telefone: "11987654321",
            Senha: "ValkyPassword123",
            Perfil: PerfilUsuario.Usuaria,
            CodigoBo: "BO-2026-ALERTA",
            PinDisfarce: "1901"));

        resp.EnsureSuccessStatusCode();
        var auth = (await resp.Content.ReadFromJsonAsync<AuthTokenResponse>())!;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
    }

    private async Task<AlertaResponse> CriarAlertaAsync()
    {
        await AutenticarClienteAsync();
        var resp = await _client.PostAsJsonAsync("/api/alertas",
            new AlertaRequest("BO-TESTE-001", -23.5505m, -46.6333m, null));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<AlertaResponse>())!;
    }

    internal static string GerarCpfValidoUnico()
    {
        var random = Random.Shared;
        var digitos = new int[11];
        do
        {
            for (int i = 0; i < 9; i++)
                digitos[i] = random.Next(0, 10);
        } while (digitos.Take(9).Distinct().Count() == 1);

        int soma1 = 0;
        for (int i = 0; i < 9; i++)
            soma1 += digitos[i] * (10 - i);
        int resto1 = (soma1 * 10) % 11;
        digitos[9] = resto1 == 10 ? 0 : resto1;

        int soma2 = 0;
        for (int i = 0; i < 10; i++)
            soma2 += digitos[i] * (11 - i);
        int resto2 = (soma2 * 10) % 11;
        digitos[10] = resto2 == 10 ? 0 : resto2;

        return string.Concat(digitos);
    }

    [Fact]
    public async Task Raiz_RetornaOk()
    {
        var resp = await _client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task HealthCheck_RetornaOk_ComRelatorioJson()
    {
        var resp = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var conteudo = await resp.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", conteudo);
        Assert.Contains("banco-de-dados", conteudo);
    }

    [Fact]
    public async Task Alerta_SemTokenJwt_Retorna401()
    {
        using var clienteAnonimo = factory.CreateClient();
        var resp = await clienteAnonimo.GetAsync("/api/alertas");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Alerta_CrudCompleto()
    {
        await AutenticarClienteAsync();

        // Create
        var criado = await CriarAlertaAsync();
        Assert.Equal(StatusAlerta.Aberto, criado.Status);

        // Read
        var lido = await _client.GetFromJsonAsync<AlertaResponse>($"/api/alertas/{criado.Id}");
        Assert.Equal("BO-TESTE-001", lido!.CodigoBo);

        // Update
        var put = await _client.PutAsJsonAsync($"/api/alertas/{criado.Id}",
            new AlertaRequest("BO-TESTE-001", -23.56m, -46.64m, StatusAlerta.ViaturaACaminho));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var atualizado = await put.Content.ReadFromJsonAsync<AlertaResponse>();
        Assert.Equal(StatusAlerta.ViaturaACaminho, atualizado!.Status);

        // Delete
        var del = await _client.DeleteAsync($"/api/alertas/{criado.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);
        var depois = await _client.GetAsync($"/api/alertas/{criado.Id}");
        Assert.Equal(HttpStatusCode.NotFound, depois.StatusCode);
    }

    [Fact]
    public async Task Alerta_Invalido_Retorna400()
    {
        await AutenticarClienteAsync();
        var resp = await _client.PostAsJsonAsync("/api/alertas", new AlertaRequest("", 200, 0, null));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Alerta_Inexistente_Retorna404()
    {
        await AutenticarClienteAsync();
        var resp = await _client.GetAsync("/api/alertas/999999");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task AtualizacaoStatus_CrudCompleto_E_AtualizaStatusDoAlerta()
    {
        var alerta = await CriarAlertaAsync();

        // Create
        var post = await _client.PostAsJsonAsync("/api/atualizacoes-status",
            new AtualizacaoStatusRequest(alerta.Id, StatusAlerta.ViaturaACaminho, "Viatura despachada", "Central 190"));
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        var criada = (await post.Content.ReadFromJsonAsync<AtualizacaoStatusResponse>())!;

        var alertaDepois = await _client.GetFromJsonAsync<AlertaResponse>($"/api/alertas/{alerta.Id}");
        Assert.Equal(StatusAlerta.ViaturaACaminho, alertaDepois!.Status);

        // Read
        var lida = await _client.GetFromJsonAsync<AtualizacaoStatusResponse>($"/api/atualizacoes-status/{criada.Id}");
        Assert.Equal("Central 190", lida!.Responsavel);

        // Update
        var put = await _client.PutAsJsonAsync($"/api/atualizacoes-status/{criada.Id}",
            new AtualizacaoStatusRequest(alerta.Id, StatusAlerta.NoLocal, "Viatura no local", "Central 190"));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        // Delete
        var del = await _client.DeleteAsync($"/api/atualizacoes-status/{criada.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);
    }

    [Fact]
    public async Task AtualizacaoStatus_EmAlertaEncerrado_Retorna409()
    {
        var alerta = await CriarAlertaAsync();
        await _client.PostAsJsonAsync("/api/atualizacoes-status",
            new AtualizacaoStatusRequest(alerta.Id, StatusAlerta.Encerrado, "Ocorrência encerrada", "Central 190"));

        var resp = await _client.PostAsJsonAsync("/api/atualizacoes-status",
            new AtualizacaoStatusRequest(alerta.Id, StatusAlerta.NoLocal, null, "Central 190"));
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }
}
