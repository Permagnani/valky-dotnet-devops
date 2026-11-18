using Microsoft.EntityFrameworkCore;
using Valky.Api.Configuration;
using Valky.Api.Contracts;
using Valky.Api.Data;
using Valky.Api.Domain;
using Valky.Api.Errors;
using Valky.Api.Models;
using Valky.Api.Security;

namespace Valky.Api.Endpoints;


/// Endpoints RESTful completos para gerenciamento de Usuários e Rede de Contatos de Confiança.
/// Todos os endpoints contam com Rate Limiting individualizado e mapeamento de erros via Clean Code.
public static class UsuarioEndpoints
{
    public static void MapUsuarioEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/usuarios").WithTags("Usuários e Rede de Confiança");

        // GET /api/usuarios
        grupo.MapGet("/", async (string? perfil, bool? ativo, AppDbContext db) =>
        {
            var query = db.Usuarios
                .AsNoTracking()
                .Include(u => u.ContatosConfianca)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(perfil))
            {
                var perfilFiltro = perfil.Trim().ToUpperInvariant();
                query = query.Where(u => u.Perfil == perfilFiltro);
            }

            if (ativo.HasValue)
                query = query.Where(u => u.Ativo == ativo.Value);

            var usuarios = await query.OrderByDescending(u => u.Id).ToListAsync();
            return Results.Ok(usuarios.Select(UsuarioResponse.De));
        })
        .RequireAuthorization()
        .RequireRateLimiting(PoliticasRateLimit.Leitura)
        .WithSummary("Lista os usuários cadastrados (permite filtrar por perfil e status ativo).");

        // GET /api/usuarios/{id}
        grupo.MapGet("/{id:int}", async (int id, AppDbContext db) =>
        {
            var usuario = await db.Usuarios
                .AsNoTracking()
                .Include(u => u.ContatosConfianca)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (usuario is null)
                throw new RecursoNaoEncontradoException($"Usuário com ID {id} não foi encontrado.");

            return Results.Ok(UsuarioResponse.De(usuario));
        })
        .RequireAuthorization()
        .RequireRateLimiting(PoliticasRateLimit.Leitura)
        .WithSummary("Obtém um usuário pelo ID, incluindo seus contatos de confiança e diagnóstico de prontidão.");

        // POST /api/usuarios (Público para permitir cadastro inicial da conta pelo app mobile)
        grupo.MapPost("/", async (RegistrarUsuarioRequest req, AppDbContext db) =>
        {
            var erros = RegrasUsuario.ValidarCadastroUsuario(
                req.Nome, req.Email, req.Cpf, req.Telefone, req.Senha, req.Perfil, req.CodigoBo, req.PinDisfarce);

            if (erros.Count > 0)
                throw new ValidacaoException(erros);

            var emailNormalizado = RegrasUsuario.NormalizarEmail(req.Email);
            var cpfNormalizado = RegrasUsuario.NormalizarDigitos(req.Cpf);
            var telefoneNormalizado = RegrasUsuario.NormalizarDigitos(req.Telefone);

            if (await db.Usuarios.AsNoTracking().AnyAsync(u => u.Email == emailNormalizado))
                throw new ConflitoNegocioException("Já existe um usuário cadastrado com este endereço de e-mail.");

            if (await db.Usuarios.AsNoTracking().AnyAsync(u => u.Cpf == cpfNormalizado))
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

            return Results.Created($"/api/usuarios/{usuario.Id}", UsuarioResponse.De(usuario));
        })
        .AllowAnonymous()
        .RequireRateLimiting(PoliticasRateLimit.Autenticacao)
        .WithSummary("Cadastra um novo usuário na plataforma Valky.");

        // PUT /api/usuarios/{id}
        grupo.MapPut("/{id:int}", async (int id, AtualizarUsuarioRequest req, AppDbContext db) =>
        {
            var usuario = await db.Usuarios
                .Include(u => u.ContatosConfianca)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (usuario is null)
                throw new RecursoNaoEncontradoException($"Usuário com ID {id} não foi encontrado.");

            var erros = RegrasUsuario.ValidarAtualizacaoUsuario(
                req.Nome, req.Email, req.Telefone, req.Perfil, req.CodigoBo, req.PinDisfarce);

            if (erros.Count > 0)
                throw new ValidacaoException(erros);

            var emailNormalizado = RegrasUsuario.NormalizarEmail(req.Email);
            var emailEmUso = await db.Usuarios
                .AsNoTracking()
                .AnyAsync(u => u.Email == emailNormalizado && u.Id != id);

            if (emailEmUso)
                throw new ConflitoNegocioException("O endereço de e-mail informado já está em uso por outro usuário.");

            usuario.Nome = req.Nome!.Trim();
            usuario.Email = emailNormalizado;
            usuario.Telefone = RegrasUsuario.NormalizarDigitos(req.Telefone);
            usuario.Perfil = string.IsNullOrWhiteSpace(req.Perfil)
                ? usuario.Perfil
                : req.Perfil.Trim().ToUpperInvariant();
            usuario.CodigoBo = req.CodigoBo?.Trim();
            if (!string.IsNullOrWhiteSpace(req.PinDisfarce))
                usuario.PinDisfarce = req.PinDisfarce.Trim();
            usuario.PossuiMedidaProtetiva = req.PossuiMedidaProtetiva;
            usuario.PermissaoMicrofone = req.PermissaoMicrofone;
            usuario.PermissaoLocalizacao = req.PermissaoLocalizacao;
            usuario.Ativo = req.Ativo;
            usuario.AtualizadoEm = DateTime.UtcNow;

            await db.SaveChangesAsync();
            return Results.Ok(UsuarioResponse.De(usuario));
        })
        .RequireAuthorization()
        .RequireRateLimiting(PoliticasRateLimit.Escrita)
        .WithSummary("Atualiza dados cadastrais, BO, PIN da calculadora e permissões do aplicativo mobile.");

        // PATCH /api/usuarios/{id}/senha
        grupo.MapPatch("/{id:int}/senha", async (int id, AlterarSenhaRequest req, AppDbContext db) =>
        {
            var usuario = await db.Usuarios.FindAsync(id);
            if (usuario is null)
                throw new RecursoNaoEncontradoException($"Usuário com ID {id} não foi encontrado.");

            var erros = RegrasUsuario.ValidarAlteracaoSenha(req.SenhaAtual, req.NovaSenha);
            if (erros.Count > 0)
                throw new ValidacaoException(erros);

            if (!SenhaHasher.VerificarSenha(req.SenhaAtual!, usuario.SenhaHash))
                throw new NaoAutorizadoException("A senha atual informada está incorreta.");

            usuario.SenhaHash = SenhaHasher.GerarHash(req.NovaSenha!);
            usuario.AtualizadoEm = DateTime.UtcNow;

            await db.SaveChangesAsync();
            return Results.NoContent();
        })
        .RequireAuthorization()
        .RequireRateLimiting(PoliticasRateLimit.Escrita)
        .WithSummary("Altera a senha do usuário mediante validação da senha atual.");

        // DELETE /api/usuarios/{id}
        grupo.MapDelete("/{id:int}", async (int id, AppDbContext db) =>
        {
            var usuario = await db.Usuarios.FindAsync(id);
            if (usuario is null)
                throw new RecursoNaoEncontradoException($"Usuário com ID {id} não foi encontrado.");

            db.Usuarios.Remove(usuario);
            await db.SaveChangesAsync();
            return Results.NoContent();
        })
        .RequireAuthorization()
        .RequireRateLimiting(PoliticasRateLimit.Escrita)
        .WithSummary("Remove o cadastro de um usuário e seus contatos de confiança vinculados.");

        // ==================== REDE DE CONTATOS DE CONFIANÇA ====================

        // GET /api/usuarios/{usuarioId}/contatos-confianca
        grupo.MapGet("/{usuarioId:int}/contatos-confianca", async (int usuarioId, AppDbContext db) =>
        {
            var existeUsuario = await db.Usuarios.AsNoTracking().AnyAsync(u => u.Id == usuarioId);
            if (!existeUsuario)
                throw new RecursoNaoEncontradoException($"Usuária com ID {usuarioId} não foi encontrada.");

            var contatos = await db.ContatosConfianca
                .AsNoTracking()
                .Where(c => c.UsuarioId == usuarioId)
                .OrderBy(c => c.Id)
                .ToListAsync();

            return Results.Ok(contatos.Select(ContatoConfiancaResponse.De));
        })
        .RequireAuthorization()
        .RequireRateLimiting(PoliticasRateLimit.Leitura)
        .WithSummary("Lista todos os contatos da rede de confiança vinculados a uma usuária.");

        // POST /api/usuarios/{usuarioId}/contatos-confianca
        grupo.MapPost("/{usuarioId:int}/contatos-confianca", async (
            int usuarioId,
            ContatoConfiancaRequest req,
            AppDbContext db) =>
        {
            var erros = RegrasUsuario.ValidarContatoConfianca(
                req.Nome, req.Telefone, req.Email, req.Parentesco, req.NotificarPorSms, req.NotificarPorApp);

            if (erros.Count > 0)
                throw new ValidacaoException(erros);

            var usuario = await db.Usuarios
                .Include(u => u.ContatosConfianca)
                .FirstOrDefaultAsync(u => u.Id == usuarioId);

            if (usuario is null)
                throw new RecursoNaoEncontradoException($"Usuária com ID {usuarioId} não foi encontrada.");

            if (!RegrasUsuario.PodeAdicionarContatoConfianca(usuario.ContatosConfianca.Count))
            {
                throw new ConflitoNegocioException(
                    $"Limite máximo de {RegrasUsuario.LimiteMaximoContatosConfianca} contatos de confiança por usuária atingido.");
            }

            var telefoneNormalizado = RegrasUsuario.NormalizarDigitos(req.Telefone);
            if (usuario.ContatosConfianca.Any(c => c.Telefone == telefoneNormalizado))
            {
                throw new ConflitoNegocioException(
                    "Já existe um contato de confiança com este telefone cadastrado para esta usuária.");
            }

            var contato = new ContatoConfianca
            {
                UsuarioId = usuarioId,
                Nome = req.Nome!.Trim(),
                Telefone = telefoneNormalizado,
                Email = string.IsNullOrWhiteSpace(req.Email) ? null : RegrasUsuario.NormalizarEmail(req.Email),
                Parentesco = req.Parentesco!.Trim(),
                NotificarPorSms = req.NotificarPorSms,
                NotificarPorApp = req.NotificarPorApp,
                CriadoEm = DateTime.UtcNow
            };

            db.ContatosConfianca.Add(contato);
            await db.SaveChangesAsync();

            return Results.Created(
                $"/api/usuarios/{usuarioId}/contatos-confianca/{contato.Id}",
                ContatoConfiancaResponse.De(contato));
        })
        .RequireAuthorization()
        .RequireRateLimiting(PoliticasRateLimit.Escrita)
        .WithSummary("Adiciona uma pessoa de confiança à rede de apoio da usuária (máx. 5 contatos).");

        // PUT /api/usuarios/{usuarioId}/contatos-confianca/{contatoId}
        grupo.MapPut("/{usuarioId:int}/contatos-confianca/{contatoId:int}", async (
            int usuarioId,
            int contatoId,
            ContatoConfiancaRequest req,
            AppDbContext db) =>
        {
            var erros = RegrasUsuario.ValidarContatoConfianca(
                req.Nome, req.Telefone, req.Email, req.Parentesco, req.NotificarPorSms, req.NotificarPorApp);

            if (erros.Count > 0)
                throw new ValidacaoException(erros);

            var contato = await db.ContatosConfianca
                .FirstOrDefaultAsync(c => c.Id == contatoId && c.UsuarioId == usuarioId);

            if (contato is null)
                throw new RecursoNaoEncontradoException($"Contato de confiança {contatoId} não encontrado para a usuária {usuarioId}.");

            var telefoneNormalizado = RegrasUsuario.NormalizarDigitos(req.Telefone);
            var telefoneDuplicado = await db.ContatosConfianca
                .AsNoTracking()
                .AnyAsync(c => c.UsuarioId == usuarioId && c.Telefone == telefoneNormalizado && c.Id != contatoId);

            if (telefoneDuplicado)
                throw new ConflitoNegocioException("Outro contato de confiança desta usuária já utiliza este telefone.");

            contato.Nome = req.Nome!.Trim();
            contato.Telefone = telefoneNormalizado;
            contato.Email = string.IsNullOrWhiteSpace(req.Email) ? null : RegrasUsuario.NormalizarEmail(req.Email);
            contato.Parentesco = req.Parentesco!.Trim();
            contato.NotificarPorSms = req.NotificarPorSms;
            contato.NotificarPorApp = req.NotificarPorApp;

            await db.SaveChangesAsync();
            return Results.Ok(ContatoConfiancaResponse.De(contato));
        })
        .RequireAuthorization()
        .RequireRateLimiting(PoliticasRateLimit.Escrita)
        .WithSummary("Atualiza os dados e canais de notificação de um contato de confiança.");

        // DELETE /api/usuarios/{usuarioId}/contatos-confianca/{contatoId}
        grupo.MapDelete("/{usuarioId:int}/contatos-confianca/{contatoId:int}", async (
            int usuarioId,
            int contatoId,
            AppDbContext db) =>
        {
            var contato = await db.ContatosConfianca
                .FirstOrDefaultAsync(c => c.Id == contatoId && c.UsuarioId == usuarioId);

            if (contato is null)
                throw new RecursoNaoEncontradoException($"Contato de confiança {contatoId} não encontrado para a usuária {usuarioId}.");

            db.ContatosConfianca.Remove(contato);
            await db.SaveChangesAsync();
            return Results.NoContent();
        })
        .RequireAuthorization()
        .RequireRateLimiting(PoliticasRateLimit.Escrita)
        .WithSummary("Remove um contato da rede de confiança da usuária.");
    }
}
