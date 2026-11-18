namespace Valky.Api.Models;

/// <summary>
/// Perfis de acesso disponíveis no ecossistema Valky (conforme especificação CitizenTech).
/// </summary>
public static class PerfilUsuario
{
    public const string Usuaria = "USUARIA";
    public const string ContatoConfianca = "CONTATO_CONFIANCA";
    public const string Policia = "POLICIA";

    public static readonly IReadOnlySet<string> Validos =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Usuaria,
            ContatoConfianca,
            Policia
        };
}
