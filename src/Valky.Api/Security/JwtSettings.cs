namespace Valky.Api.Security;

/// <summary>
/// Configurações de emissão e validação de tokens JWT (JSON Web Token) para o aplicativo mobile Valky.
/// </summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// Chave simétrica HMAC-SHA256 (mínimo 32 caracteres / 256 bits).
    /// Lida da configuração (App Service: Jwt__ChaveSecreta; local: user-secrets).
    /// </summary>
    public string ChaveSecreta { get; set; } = string.Empty;

    public string Emissor { get; set; } = "Valky.Api";

    public string Audiencia { get; set; } = "Valky.Mobile";

    public int ExpiracaoMinutos { get; set; } = 120;
}
