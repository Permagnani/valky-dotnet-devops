using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Valky.Api.Models;

namespace Valky.Api.Security;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiraEm) GerarToken(Usuario usuario);
}

/// <summary>
/// Serviço responsável pela geração de tokens JWT assinados digitalmente para autenticação stateless no app mobile.
/// </summary>
public sealed class JwtTokenService(IOptions<JwtSettings> options) : IJwtTokenService
{
    private readonly JwtSettings _settings = options.Value;

    public (string Token, DateTime ExpiraEm) GerarToken(Usuario usuario)
    {
        var chaveBytes = Encoding.UTF8.GetBytes(_settings.ChaveSecreta);
        var chaveSeguranca = new SymmetricSecurityKey(chaveBytes);
        var credenciais = new SigningCredentials(chaveSeguranca, SecurityAlgorithms.HmacSha256);

        var agora = DateTime.UtcNow;
        var expiraEm = agora.AddMinutes(_settings.ExpiracaoMinutos);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, usuario.Email),
            new(ClaimTypes.Name, usuario.Nome),
            new(ClaimTypes.Role, usuario.Perfil),
            new("perfil", usuario.Perfil),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (!string.IsNullOrWhiteSpace(usuario.CodigoBo))
            claims.Add(new Claim("codigoBo", usuario.CodigoBo));

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            NotBefore = agora,
            Expires = expiraEm,
            Issuer = _settings.Emissor,
            Audience = _settings.Audiencia,
            SigningCredentials = credenciais
        };

        var handler = new JwtSecurityTokenHandler();
        var securityToken = handler.CreateToken(tokenDescriptor);
        var tokenString = handler.WriteToken(securityToken);

        return (tokenString, expiraEm);
    }
}
