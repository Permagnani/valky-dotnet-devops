using System.Text.RegularExpressions;
using Valky.Api.Models;

namespace Valky.Api.Domain;

// Regras de negócio e validações do domínio de Usuários e Rede de Confiança.
// Isoladas de infraestrutura (banco e HTTP) para garantir Clean Code e testabilidade unitária.
public static partial class RegrasUsuario
{
    public const int LimiteMaximoContatosConfianca = 5;

    public static List<string> ValidarCadastroUsuario(
        string? nome,
        string? email,
        string? cpf,
        string? telefone,
        string? senha,
        string? perfil,
        string? codigoBo,
        string? pinDisfarce)
    {
        var erros = ValidarDadosComunsUsuario(nome, email, telefone, perfil, codigoBo, pinDisfarce);

        if (string.IsNullOrWhiteSpace(cpf))
            erros.Add("O CPF é obrigatório.");
        else if (!CpfValido(cpf))
            erros.Add("O CPF informado é inválido.");

        erros.AddRange(ValidarSenhaForte(senha));

        return erros;
    }

    public static List<string> ValidarAtualizacaoUsuario(
        string? nome,
        string? email,
        string? telefone,
        string? perfil,
        string? codigoBo,
        string? pinDisfarce)
    {
        return ValidarDadosComunsUsuario(nome, email, telefone, perfil, codigoBo, pinDisfarce);
    }

    public static List<string> ValidarAlteracaoSenha(string? senhaAtual, string? novaSenha)
    {
        var erros = new List<string>();

        if (string.IsNullOrWhiteSpace(senhaAtual))
            erros.Add("A senha atual é obrigatória.");

        erros.AddRange(ValidarSenhaForte(novaSenha));

        if (!string.IsNullOrWhiteSpace(senhaAtual) &&
            !string.IsNullOrWhiteSpace(novaSenha) &&
            senhaAtual == novaSenha)
        {
            erros.Add("A nova senha deve ser diferente da senha atual.");
        }

        return erros;
    }

    public static List<string> ValidarLogin(string? email, string? senha)
    {
        var erros = new List<string>();

        if (string.IsNullOrWhiteSpace(email) || !EmailValido(email))
            erros.Add("Informe um e-mail válido.");

        if (string.IsNullOrWhiteSpace(senha))
            erros.Add("A senha é obrigatória.");

        return erros;
    }

    public static List<string> ValidarContatoConfianca(
        string? nome,
        string? telefone,
        string? email,
        string? parentesco,
        bool notificarPorSms,
        bool notificarPorApp)
    {
        var erros = new List<string>();

        if (string.IsNullOrWhiteSpace(nome) || nome.Trim().Length < 2)
            erros.Add("O nome do contato de confiança deve ter pelo menos 2 caracteres.");
        else if (nome.Trim().Length > 120)
            erros.Add("O nome do contato de confiança deve ter no máximo 120 caracteres.");

        if (string.IsNullOrWhiteSpace(telefone) || !TelefoneValido(telefone))
            erros.Add("O telefone do contato de confiança é inválido (informe DDD + número com 10 a 15 dígitos).");

        if (!string.IsNullOrWhiteSpace(email))
        {
            if (email.Trim().Length > 150 || !EmailValido(email))
                erros.Add("O e-mail do contato de confiança é inválido.");
        }

        if (string.IsNullOrWhiteSpace(parentesco))
            erros.Add("O grau de parentesco ou relação é obrigatório (ex.: Mãe, Irmã, Amiga).");
        else if (parentesco.Trim().Length > 50)
            erros.Add("O grau de parentesco deve ter no máximo 50 caracteres.");

        if (!notificarPorSms && !notificarPorApp)
            erros.Add("Pelo menos um canal de notificação (SMS ou App) deve estar habilitado para o contato.");

        return erros;
    }

    public static bool PodeAdicionarContatoConfianca(int quantidadeAtual) =>
        quantidadeAtual < LimiteMaximoContatosConfianca;

    public static List<string> AvaliarPendenciasConfiguracaoAlerta(Usuario usuario, int totalContatosConfianca)
    {
        var pendencias = new List<string>();

        if (!usuario.Ativo)
            pendencias.Add("Conta inativa.");

        if (string.Equals(usuario.Perfil, PerfilUsuario.Usuaria, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(usuario.CodigoBo))
                pendencias.Add("Número do Boletim de Ocorrência (BO) pendente.");

            if (string.IsNullOrWhiteSpace(usuario.PinDisfarce))
                pendencias.Add("Código PIN da calculadora disfarçada não configurado.");

            if (!usuario.PermissaoMicrofone)
                pendencias.Add("Permissão de gravação de áudio (microfone) não concedida no celular.");

            if (!usuario.PermissaoLocalizacao)
                pendencias.Add("Permissão de localização em tempo real não concedida no celular.");

            if (totalContatosConfianca <= 0)
                pendencias.Add("Nenhum contato de confiança cadastrado na rede de apoio.");
        }

        return pendencias;
    }

    public static List<string> ValidarSenhaForte(string? senha)
    {
        var erros = new List<string>();

        if (string.IsNullOrWhiteSpace(senha))
        {
            erros.Add("A senha é obrigatória.");
            return erros;
        }

        if (senha.Length < 8)
            erros.Add("A senha deve ter no mínimo 8 caracteres.");
        if (senha.Length > 100)
            erros.Add("A senha deve ter no máximo 100 caracteres.");
        if (!senha.Any(char.IsUpper))
            erros.Add("A senha deve conter pelo menos uma letra maiúscula.");
        if (!senha.Any(char.IsLower))
            erros.Add("A senha deve conter pelo menos uma letra minúscula.");
        if (!senha.Any(char.IsDigit))
            erros.Add("A senha deve conter pelo menos um número.");

        return erros;
    }

    public static string NormalizarDigitos(string? valor) =>
        string.IsNullOrWhiteSpace(valor)
            ? string.Empty
            : new string(valor.Where(char.IsDigit).ToArray());

    public static string NormalizarEmail(string? email) =>
        string.IsNullOrWhiteSpace(email)
            ? string.Empty
            : email.Trim().ToLowerInvariant();

    public static bool EmailValido(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var limpo = email.Trim();
        return limpo.Length <= 150 && RegexEmail().IsMatch(limpo);
    }

    public static bool TelefoneValido(string? telefone)
    {
        var digitos = NormalizarDigitos(telefone);
        return digitos.Length is >= 10 and <= 15;
    }

    public static bool PinDisfarceValido(string? pin)
    {
        if (string.IsNullOrWhiteSpace(pin))
            return false;

        var limpo = pin.Trim();
        return limpo.Length is >= 4 and <= 8 && limpo.All(char.IsDigit);
    }

    public static bool CpfValido(string? cpf)
    {
        var digitos = NormalizarDigitos(cpf);
        if (digitos.Length != 11)
            return false;

        // Rejeita CPFs com todos os dígitos iguais (ex: 111.111.111-11)
        if (digitos.Distinct().Count() == 1)
            return false;

        int soma1 = 0;
        for (int i = 0; i < 9; i++)
            soma1 += (digitos[i] - '0') * (10 - i);

        int resto1 = (soma1 * 10) % 11;
        if (resto1 == 10) resto1 = 0;
        if (resto1 != digitos[9] - '0')
            return false;

        int soma2 = 0;
        for (int i = 0; i < 10; i++)
            soma2 += (digitos[i] - '0') * (11 - i);

        int resto2 = (soma2 * 10) % 11;
        if (resto2 == 10) resto2 = 0;
        return resto2 == digitos[10] - '0';
    }

    private static List<string> ValidarDadosComunsUsuario(
        string? nome,
        string? email,
        string? telefone,
        string? perfil,
        string? codigoBo,
        string? pinDisfarce)
    {
        var erros = new List<string>();

        if (string.IsNullOrWhiteSpace(nome) || nome.Trim().Length < 3)
            erros.Add("O nome completo deve ter pelo menos 3 caracteres.");
        else if (nome.Trim().Length > 120)
            erros.Add("O nome completo deve ter no máximo 120 caracteres.");

        if (string.IsNullOrWhiteSpace(email) || !EmailValido(email))
            erros.Add("Informe um endereço de e-mail válido.");

        if (string.IsNullOrWhiteSpace(telefone) || !TelefoneValido(telefone))
            erros.Add("Informe um telefone válido com DDD (10 a 15 dígitos).");

        var perfilEfetivo = string.IsNullOrWhiteSpace(perfil)
            ? PerfilUsuario.Usuaria
            : perfil.Trim().ToUpperInvariant();

        if (!PerfilUsuario.Validos.Contains(perfilEfetivo))
            erros.Add($"Perfil inválido. Valores aceitos: {string.Join(", ", PerfilUsuario.Validos)}.");

        if (!string.IsNullOrWhiteSpace(pinDisfarce) && !PinDisfarceValido(pinDisfarce))
            erros.Add("O PIN de disfarce da calculadora deve conter apenas números (entre 4 e 8 dígitos).");

        return erros;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex RegexEmail();
}
