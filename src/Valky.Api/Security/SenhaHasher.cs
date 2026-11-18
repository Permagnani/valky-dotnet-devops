using System.Security.Cryptography;

namespace Valky.Api.Security;

/// <summary>
/// Utilitário criptográfico para geração e verificação de hashes de senha utilizando PBKDF2-HMAC-SHA256 (padrão OWASP).
/// Protege contra ataques de dicionário, rainbow tables (via salt aleatório por usuário) e timing attacks.
/// </summary>
public static class SenhaHasher
{
    private const int TamanhoSaltBytes = 16;
    private const int TamanhoChaveBytes = 32;
    private const int Iteracoes = 100_000;
    private const string PrefixoAlgoritmo = "PBKDF2-SHA256";

    public static string GerarHash(string senha)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(senha);

        var salt = RandomNumberGenerator.GetBytes(TamanhoSaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password: senha,
            salt: salt,
            iterations: Iteracoes,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: TamanhoChaveBytes);

        return $"{PrefixoAlgoritmo}${Iteracoes}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool VerificarSenha(string senha, string senhaHashArmazenada)
    {
        if (string.IsNullOrWhiteSpace(senha) || string.IsNullOrWhiteSpace(senhaHashArmazenada))
            return false;

        var partes = senhaHashArmazenada.Split('$');
        if (partes.Length != 4 || partes[0] != PrefixoAlgoritmo)
            return false;

        if (!int.TryParse(partes[1], out var iteracoes) || iteracoes <= 0)
            return false;

        byte[] salt;
        byte[] hashEsperado;
        try
        {
            salt = Convert.FromBase64String(partes[2]);
            hashEsperado = Convert.FromBase64String(partes[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var hashCalculado = Rfc2898DeriveBytes.Pbkdf2(
            password: senha,
            salt: salt,
            iterations: iteracoes,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: hashEsperado.Length);

        return CryptographicOperations.FixedTimeEquals(hashCalculado, hashEsperado);
    }
}
