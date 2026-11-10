namespace Valky.Api.Models;

public static class StatusAlerta
{
    public const string Aberto = "ABERTO";
    public const string ViaturaACaminho = "VIATURA_A_CAMINHO";
    public const string NoLocal = "NO_LOCAL";
    public const string Encerrado = "ENCERRADO";

    public static readonly IReadOnlySet<string> Validos =
        new HashSet<string> { Aberto, ViaturaACaminho, NoLocal, Encerrado };
}
