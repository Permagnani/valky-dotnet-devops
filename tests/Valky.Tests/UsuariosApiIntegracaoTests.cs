using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Valky.Api.Contracts;
using Valky.Api.Models;

namespace Valky.Tests;

/// <summary>
/// Testes funcionais e de integração dos endpoints de Usuários, Contatos de Confiança,
/// Autenticação JWT, Swagger, Rate Limiting e Mapeamento de Erros (ProblemDetails RFC 7807).
/// </summary>
public class UsuariosApiIntegracaoTests(ValkyApiFactory factory) : IClassFixture<ValkyApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<AuthTokenResponse> RegistrarEAutorizarAsync(
        string? email = null,
        string? cpf = null,
        string senha = "ValkyPassword123")
    {
        var req = new RegistrarUsuarioRequest(
            Nome: "Beatriz Oliveira",
            Email: email ?? $"beatriz.{Guid.NewGuid():N}@valky.com",
            Cpf: cpf ?? ApiIntegracaoTests.GerarCpfValidoUnico(),
            Telefone: "11988776655",
            Senha: senha,
            Perfil: PerfilUsuario.Usuaria,
            CodigoBo: "BO-2026-7788",
            PinDisfarce: "2468",
            PossuiMedidaProtetiva: false,
            PermissaoMicrofone: true,
            PermissaoLocalizacao: true);

        var resp = await _client.PostAsJsonAsync("/api/auth/registrar", req);
        resp.EnsureSuccessStatusCode();

        var tokenResp = (await resp.Content.ReadFromJsonAsync<AuthTokenResponse>())!;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenResp.AccessToken);
        return tokenResp;
    }

    [Fact]
    public async Task Auth_Registrar_E_Login_E_Me_FluxoCompletoMobile()
    {
        var email = $"camila.{Guid.NewGuid():N}@valky.com";
        var cpf = ApiIntegracaoTests.GerarCpfValidoUnico();

        // 1. Registro inicial no app mobile
        var registrado = await RegistrarEAutorizarAsync(email, cpf, "SenhaForte2026");
        Assert.False(string.IsNullOrWhiteSpace(registrado.AccessToken));
        Assert.Equal("Bearer", registrado.TipoToken);
        Assert.Equal(email, registrado.Usuario.Email);
        Assert.True(registrado.Usuario.PossuiPinDisfarceConfigurado);

        // 2. Login com as mesmas credenciais
        var loginResp = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "SenhaForte2026"));
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        var loginPayload = await loginResp.Content.ReadFromJsonAsync<AuthTokenResponse>();
        Assert.NotNull(loginPayload);
        Assert.False(string.IsNullOrWhiteSpace(loginPayload!.AccessToken));

        // 3. Consulta /api/auth/me usando o token JWT
        var meResp = await _client.GetFromJsonAsync<UsuarioResponse>("/api/auth/me");
        Assert.NotNull(meResp);
        Assert.Equal(registrado.Usuario.Id, meResp!.Id);
        Assert.Equal("BO-2026-7788", meResp.CodigoBo);
    }

    [Fact]
    public async Task Auth_LoginComSenhaIncorreta_Retorna401ProblemDetails()
    {
        var email = $"erro.senha.{Guid.NewGuid():N}@valky.com";
        await RegistrarEAutorizarAsync(email: email, senha: "SenhaCorreta123");

        var resp = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "SenhaErrada999"));
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);

        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("NAO_AUTORIZADO", body);
    }

    [Fact]
    public async Task Usuarios_SemTokenJwt_Retorna401()
    {
        using var clienteSemToken = factory.CreateClient();
        var resp = await clienteSemToken.GetAsync("/api/usuarios");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Usuarios_CrudCompleto_E_AlteracaoDeSenha()
    {
        var auth = await RegistrarEAutorizarAsync(senha: "SenhaInicial123");
        var usuarioId = auth.Usuario.Id;

        // Read por ID
        var lido = await _client.GetFromJsonAsync<UsuarioResponse>($"/api/usuarios/{usuarioId}");
        Assert.Equal(usuarioId, lido!.Id);

        // Update cadastral e ativação de medida protetiva
        var putResp = await _client.PutAsJsonAsync($"/api/usuarios/{usuarioId}", new AtualizarUsuarioRequest(
            Nome: "Beatriz Oliveira Atualizada",
            Email: lido.Email,
            Telefone: "11977776666",
            Perfil: PerfilUsuario.Usuaria,
            CodigoBo: "BO-2026-9999",
            PinDisfarce: "1357",
            PossuiMedidaProtetiva: true,
            PermissaoMicrofone: true,
            PermissaoLocalizacao: true,
            Ativo: true));
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);
        var atualizado = await putResp.Content.ReadFromJsonAsync<UsuarioResponse>();
        Assert.Equal("Beatriz Oliveira Atualizada", atualizado!.Nome);
        Assert.True(atualizado.PossuiMedidaProtetiva);

        // Alterar senha (PATCH)
        var patchSenha = await _client.PatchAsJsonAsync($"/api/usuarios/{usuarioId}/senha",
            new AlterarSenhaRequest("SenhaInicial123", "NovaSenhaSegura456"));
        Assert.Equal(HttpStatusCode.NoContent, patchSenha.StatusCode);

        // Verificar login com a nova senha
        var loginNovo = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(lido.Email, "NovaSenhaSegura456"));
        Assert.Equal(HttpStatusCode.OK, loginNovo.StatusCode);

        // Delete
        var delResp = await _client.DeleteAsync($"/api/usuarios/{usuarioId}");
        Assert.Equal(HttpStatusCode.NoContent, delResp.StatusCode);

        var getAposDelete = await _client.GetAsync($"/api/usuarios/{usuarioId}");
        Assert.Equal(HttpStatusCode.NotFound, getAposDelete.StatusCode);
    }

    [Fact]
    public async Task Usuarios_EmailOuCpfDuplicado_Retorna409Conflict()
    {
        var email = $"duplicado.{Guid.NewGuid():N}@valky.com";
        var cpf = ApiIntegracaoTests.GerarCpfValidoUnico();
        await RegistrarEAutorizarAsync(email: email, cpf: cpf);

        // Tenta registrar outro usuário com mesmo e-mail
        var respEmail = await _client.PostAsJsonAsync("/api/usuarios", new RegistrarUsuarioRequest(
            Nome: "Outra Usuária",
            Email: email,
            Cpf: ApiIntegracaoTests.GerarCpfValidoUnico(),
            Telefone: "11966665555",
            Senha: "SenhaForte123",
            Perfil: PerfilUsuario.Usuaria,
            CodigoBo: "BO-2026-111",
            PinDisfarce: "1901"));
        Assert.Equal(HttpStatusCode.Conflict, respEmail.StatusCode);

        // Tenta registrar outro usuário com mesmo CPF
        var respCpf = await _client.PostAsJsonAsync("/api/usuarios", new RegistrarUsuarioRequest(
            Nome: "Outra Usuária CPF",
            Email: $"outro.{Guid.NewGuid():N}@valky.com",
            Cpf: cpf,
            Telefone: "11966665555",
            Senha: "SenhaForte123",
            Perfil: PerfilUsuario.Usuaria,
            CodigoBo: "BO-2026-112",
            PinDisfarce: "1901"));
        Assert.Equal(HttpStatusCode.Conflict, respCpf.StatusCode);
    }

    [Fact]
    public async Task ContatosConfianca_CrudCompleto_E_HabilitaProntidaoDeAlertaSilencioso()
    {
        var auth = await RegistrarEAutorizarAsync();
        var usuarioId = auth.Usuario.Id;

        // Antes de cadastrar contato, ainda não está pronta para alerta silencioso
        Assert.False(auth.Usuario.ProntaParaAlertaSilencioso);

        // 1. Adicionar contato de confiança
        var postContato = await _client.PostAsJsonAsync(
            $"/api/usuarios/{usuarioId}/contatos-confianca",
            new ContatoConfiancaRequest("Dona Maria (Mãe)", "11955554444", "mae@email.com", "Mãe", true, true));
        Assert.Equal(HttpStatusCode.Created, postContato.StatusCode);
        var contatoCriado = (await postContato.Content.ReadFromJsonAsync<ContatoConfiancaResponse>())!;

        // 2. Agora a usuária deve estar 100% pronta para o alerta silencioso
        var usuariaPronta = await _client.GetFromJsonAsync<UsuarioResponse>($"/api/usuarios/{usuarioId}");
        Assert.True(usuariaPronta!.ProntaParaAlertaSilencioso);
        Assert.Empty(usuariaPronta.PendenciasConfiguracao);
        Assert.Single(usuariaPronta.ContatosConfianca);

        // 3. Atualizar contato de confiança
        var putContato = await _client.PutAsJsonAsync(
            $"/api/usuarios/{usuarioId}/contatos-confianca/{contatoCriado.Id}",
            new ContatoConfiancaRequest("Dona Maria Silva", "11955554444", "mae.silva@email.com", "Mãe", true, false));
        Assert.Equal(HttpStatusCode.OK, putContato.StatusCode);

        // 4. Remover contato de confiança
        var delContato = await _client.DeleteAsync($"/api/usuarios/{usuarioId}/contatos-confianca/{contatoCriado.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delContato.StatusCode);
    }

    [Fact]
    public async Task ContatosConfianca_ExcederLimiteDe5Contatos_Retorna409Conflict()
    {
        var auth = await RegistrarEAutorizarAsync();
        var usuarioId = auth.Usuario.Id;

        for (int i = 1; i <= 5; i++)
        {
            var resp = await _client.PostAsJsonAsync(
                $"/api/usuarios/{usuarioId}/contatos-confianca",
                new ContatoConfiancaRequest($"Contato {i}", $"1194444000{i}", null, "Amiga", true, true));
            Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        }

        // O 6º contato deve retornar 409 Conflict
        var sextoContato = await _client.PostAsJsonAsync(
            $"/api/usuarios/{usuarioId}/contatos-confianca",
            new ContatoConfiancaRequest("Contato 6", "11944440006", null, "Irmã", true, true));
        Assert.Equal(HttpStatusCode.Conflict, sextoContato.StatusCode);
    }

    [Fact]
    public async Task RateLimiting_ExcederLimiteDoEndpoint_Retorna429TooManyRequests()
    {
        await using var rateLimitFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Testing:DatabaseName"] = $"ValkyRateLimit_{Guid.NewGuid():N}",
                    ["RateLimiting:LimiteInfraPorMinuto"] = "2",
                    ["RateLimiting:JanelaSegundos"] = "60"
                });
            });
        });

        using var cliente = rateLimitFactory.CreateClient();
        cliente.DefaultRequestHeaders.Add("X-Client-Id", $"dispositivo-teste-{Guid.NewGuid():N}");

        var r1 = await cliente.GetAsync("/");
        var r2 = await cliente.GetAsync("/");
        var r3 = await cliente.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, r3.StatusCode);
        Assert.True(r3.Headers.RetryAfter is not null);
    }

    [Fact]
    public async Task Swagger_RetornaEspecificacaoOpenApi_ComSegurancaJwtBearer()
    {
        var resp = await _client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await resp.Content.ReadAsStringAsync();
        Assert.Contains("/api/usuarios", json);
        Assert.Contains("/api/auth/login", json);
        Assert.Contains("Bearer", json);
    }
}
