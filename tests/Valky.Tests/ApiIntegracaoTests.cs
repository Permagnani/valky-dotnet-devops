using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Valky.Api.Contracts;
using Valky.Api.Models;

namespace Valky.Tests;

/// <summary>Sobe a API em memória (ambiente "Testing", banco InMemory) para os testes de integração.</summary>
public class ValkyApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing");
}

public class ApiIntegracaoTests(ValkyApiFactory factory) : IClassFixture<ValkyApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<AlertaResponse> CriarAlertaAsync()
    {
        var resp = await _client.PostAsJsonAsync("/api/alertas",
            new AlertaRequest("BO-TESTE-001", -23.5505m, -46.6333m, null));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<AlertaResponse>())!;
    }

    [Fact]
    public async Task Raiz_RetornaOk()
    {
        var resp = await _client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task HealthCheck_RetornaOk()
    {
        var resp = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Alerta_CrudCompleto()
    {
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
        var resp = await _client.PostAsJsonAsync("/api/alertas", new AlertaRequest("", 200, 0, null));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Alerta_Inexistente_Retorna404()
    {
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
