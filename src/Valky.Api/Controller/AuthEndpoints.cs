using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Valky.Api.Configuration;
using Valky.Api.Contracts;
using Valky.Api.Data;
using Valky.Api.Domain;
using Valky.Api.Errors;
using Valky.Api.Models;
using Valky.Api.Security;

namespace Valky.Api.Endpoints;

/// <summary>
/// Endpoints de Autenticação JWT e Registro inicial para o aplicativo mobile Valky.
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/auth").WithTags("Autenticação (JWT)");

        grupo.MapPost("/registrar", async (
            RegistrarUsuarioRequest req,
            AppDbContext db,
            IJwtTokenService jwtService) =>
        {
            var erros = RegrasUsuario.ValidarCadastroUsuario(
                req.Nome, req.Email, req.Cpf, req.Telefone, req.Senha, req.Perfil, req.CodigoBo, req.PinDisfarce);

            if (erros.Count > 0)
                throw new ValidacaoException(erros);

            var emailNormalizado = RegrasUsuario.NormalizarEmail(req.Email);
            var cpfNormalizado = RegrasUsuario.NormalizarDigitos(req.Cpf);
            var telefoneNormalizado = RegrasUsuario.NormalizarDigitos(req.Telefone);

            var conflitoEmail = await db.Usuarios.AsNoTracking().AnyAsync(u => u.Email == emailNormalizado);
            if (conflitoEmail)
                throw new ConflitoNegocioException("Já existe um usuário cadastrado com este endereço de e-mail.");

            var conflitoCpf = await db.Usuarios.AsNoTracking().AnyAsync(u => u.Cpf == cpfNormalizado);
            if (conflitoCpf)
                throw new ConflitoNegocioException("Já existe um usuário cadastrado com este CPF.");

            var agora = DateTime.UtcNow;
            var usuario = new Usuario
            {
                Nome = req.Nome!.Trim(),
                Email = emailNormalizado,
                Cpf = cpfNormalizado,
                Telefone = telefoneNormalizado,
                SenhaHash = SenhaHasher.GerarHash(req.Senha!),
                Perfil = string.IsNullOrWhiteSpace(req.Perfil)
                    ? PerfilUsuario.Usuaria
                    : req.Perfil.Trim().ToUpperInvariant(),
                CodigoBo = req.CodigoBo?.Trim(),
                PinDisfarce = req.PinDisfarce?.Trim(),
                PossuiMedidaProtetiva = req.PossuiMedidaProtetiva,
                PermissaoMicrofone = req.PermissaoMicrofone,
                PermissaoLocalizacao = req.PermissaoLocalizacao,
                Ativo = true,
                CriadoEm = agora,
                AtualizadoEm = agora
            };

            db.Usuarios.Add(usuario);
            await db.SaveChangesAsync();

            var (token, expiraEm) = jwtService.GerarToken(usuario);
            var resposta = new AuthTokenResponse(
                AccessToken: token,
                TipoToken: "Bearer",
                ExpiraEm: expiraEm,
                Usuario: UsuarioResponse.De(usuario));

            return Results.Created($"/api/usuarios/{usuario.Id}", resposta);
        })
        .AllowAnonymous()
        .RequireRateLimiting(PoliticasRateLimit.Autenticacao)
        .WithSummary("Registra um novo usuário e retorna o token JWT para login imediato no app mobile.");

        grupo.MapPost("/login", async (
            LoginRequest req,
            AppDbContext db,
            IJwtTokenService jwtService) =>
        {
            var erros = RegrasUsuario.ValidarLogin(req.Email, req.Senha);
            if (erros.Count > 0)
                throw new ValidacaoException(erros);

            var emailNormalizado = RegrasUsuario.NormalizarEmail(req.Email);
            var usuario = await db.Usuarios
                .Include(u => u.ContatosConfianca)
                .FirstOrDefaultAsync(u => u.Email == emailNormalizado);

            if (usuario is null || !SenhaHasher.VerificarSenha(req.Senha!, usuario.SenhaHash))
                throw new NaoAutorizadoException("E-mail ou senha inválidos.");

            if (!usuario.Ativo)
                throw new AcessoNegadoException("Esta conta de usuário está desativada.");

            var (token, expiraEm) = jwtService.GerarToken(usuario);
            return Results.Ok(new AuthTokenResponse(
                AccessToken: token,
                TipoToken: "Bearer",
                ExpiraEm: expiraEm,
                Usuario: UsuarioResponse.De(usuario)));
        })
        .AllowAnonymous()
        .RequireRateLimiting(PoliticasRateLimit.Autenticacao)
        .WithSummary("Autentica um usuário com e-mail e senha e emite um token JWT assinado.");

        grupo.MapGet("/me", async (
            ClaimsPrincipal claimsPrincipal,
            AppDbContext db) =>
        {
            var idClaim = claimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(idClaim, out var usuarioId))
                throw new NaoAutorizadoException("Token JWT inválido: identificador do usuário não encontrado.");

            var usuario = await db.Usuarios
                .AsNoTracking()
                .Include(u => u.ContatosConfianca)
                .FirstOrDefaultAsync(u => u.Id == usuarioId);

            if (usuario is null)
                throw new RecursoNaoEncontradoException($"Usuário {usuarioId} não encontrado.");

            return Results.Ok(UsuarioResponse.De(usuario));
        })
        .RequireAuthorization()
        .RequireRateLimiting(PoliticasRateLimit.Leitura)
        .WithSummary("Retorna os dados do perfil e status de prontidão da usuária autenticada pelo token JWT.");
    }
}
