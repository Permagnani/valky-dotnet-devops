# Valky — API .NET (Global Solution 2026/2 · CitizenTech)

**Grupo Valky** · 2TDSPV

| RM | Nome |
|---|---|
| 563379 | Enzo Xavier Coelho |
| 564995 | Gustavo Ribeiro Permagnani |
| 565568 | Júlia Kauane Menezes Farias |
| 566457 | Larissa Juvenal de Magalhães |
| 565258 | Matheus Gianolli |

## A solução

A Valky é um app de proteção para mulheres em risco de violência doméstica. Disfarçado de calculadora, ele permite disparar um alerta silencioso que envia localização e contexto para a polícia, enquanto a rede de confiança acompanha o andamento do atendimento. Ele protege desde o registro do BO, antes da medida protetiva.

Esta API (.NET 10, Minimal API, EF Core, Azure SQL) centraliza:
- cadastro de usuárias e da rede de contatos de confiança;
- autenticação por JWT;
- central de alertas e histórico de atendimento;
- rate limiting por política (`autenticacao`, `escrita`, `leitura`, `infraestrutura`);
- tratamento global de erros no padrão ProblemDetails (RFC 7807);
- health check da API e do banco (`/health`).

ODS: 3 (saúde e bem-estar), 5 (igualdade de gênero), 10 (redução das desigualdades) e 11 (cidades e comunidades sustentáveis).

## Arquitetura macro

![Arquitetura macro da Valky](docs/arquitetura-valky.drawio.png)

Fonte editável: [`docs/arquitetura.drawio`](docs/arquitetura.drawio) (abrir em app.diagrams.net).

1. A equipe cria a Task no Azure Boards.
2. Faz push da branch `feature/task-N` no Azure Repos e abre o Pull Request vinculado à Task (`#N`).
3. O merge na `main` dispara a pipeline de CI `valky-CI`.
4. A CI compila, executa os testes xUnit e publica o artefato.
5. O artefato `drop` (`Valky.Api.zip` + `script-bd.sql`) dispara a Release `valky-CD`.
6. A Release executa o `script-bd.sql` no Azure SQL Database `valkydb`.
7. A Release publica o `Valky.Api.zip` no Azure App Service `app-valky-rm564995`.
8. O app mobile consome a API por HTTPS, autenticado com JWT.
9. A API lê e grava os dados no Azure SQL Database via EF Core.

## Links

| | |
|---|---|
| API em produção | https://app-valky-rm564995.azurewebsites.net |
| Swagger | https://app-valky-rm564995.azurewebsites.net/swagger |
| Health check | https://app-valky-rm564995.azurewebsites.net/health |
| Azure DevOps (projeto) | https://dev.azure.com/RM564995/valky-devops |
| Vídeo de demonstração | _link do YouTube_ |

## CI/CD (Azure DevOps)

- **Azure Boards:** cada mudança nasce de uma Task; commits usam `#N` para vincular.
- **Azure Repos:** branch `main` protegida (mínimo 1 revisor, work item obrigatório, revisor padrão). Branches no padrão `feature/task-N`.
- **CI (`azure-pipeline.yml`):** dispara a cada commit na `main`. Etapas: instalar .NET 10, restore, build, testes xUnit (com publicação dos resultados e da cobertura), publish da API em `.zip`, cópia do `script-bd.sql` e publicação do artefato `drop`.
- **Release (`valky-CD`):** gatilho de deploy contínuo no artefato da `main`. Etapas: executar `script-bd.sql` no Azure SQL e fazer o deploy no App Service. Usa variáveis de pipeline; a senha do banco é uma variável secreta.
- **Versões:** SDK do .NET `10.0.103` (`global.json`); pacotes NuGet com versões exatas e `packages.lock.json`.
- **Agente:** self-hosted `PC-GUSTAVO` (pool Default).
- **Service connection:** `sc-azure-valky` (Azure Resource Manager, escopo do Resource Group `rg-valky`).
- **GitHub:** cópia da `main` para a entrega da disciplina de .NET.

## Estrutura do repositório

```
├── azure-pipeline.yml            # Pipeline de Build (CI)
├── global.json                   # Versão fixa do SDK .NET
├── docs/
│   ├── arquitetura-valky.drawio.png  # Desenho da arquitetura macro
│   └── arquitetura.drawio        # Fonte editável (Draw.io)
├── scripts/
│   ├── infra-azure.sh            # Provisionamento via Azure CLI
│   ├── script-bd.sql             # DDL das tabelas (Azure SQL)
│   └── script-bd-oracle.sql      # DDL equivalente para Oracle
├── src/Valky.Api/                # Minimal API (+ packages.lock.json)
│   ├── Configuration/            # JWT, Swagger e rate limiting
│   ├── Contracts/                # DTOs de request/response
│   ├── Controller/               # Endpoints (auth, usuários, alertas, status)
│   ├── Data/                     # AppDbContext (EF Core)
│   ├── Domain/                   # Regras de negócio
│   ├── Errors/                   # Exceções de domínio e handler global
│   ├── Migrations/               # Migrations do EF Core (Oracle)
│   ├── Models/                   # Entidades
│   └── Security/                 # Hash de senha e emissão de JWT
└── tests/Valky.Tests/            # Testes unitários e de integração xUnit (+ packages.lock.json)
```

## Infraestrutura (Azure CLI)

No Azure Cloud Shell (Bash):

```bash
git clone https://dev.azure.com/RM564995/valky-devops/_git/valky-devops
cd valky-devops
az account set --subscription <id-da-assinatura>
chmod +x scripts/infra-azure.sh
./scripts/infra-azure.sh
```

Recursos criados (região `canadacentral`):

| Recurso | Nome |
|---|---|
| Resource Group | `rg-valky` |
| Azure SQL Server | `sql-valky-rm564995` |
| Azure SQL Database (Basic) | `valkydb` |
| App Service Plan (Linux, F1) | `plan-valky` |
| Web App (.NET 10) | `app-valky-rm564995` |

A senha do banco e a chave do JWT são informadas na execução do script. No Web App ficam a connection string `DefaultConnection` e as configurações `DatabaseProvider=SqlServer` e `Jwt__ChaveSecreta`; na Release, a senha do banco é uma variável secreta.

## Tabelas (Azure SQL)

| Tabela | Conteúdo |
|---|---|
| `TB_VALKY_USUARIOS` | usuárias, contatos e policiais (CPF e e-mail únicos, hash da senha, BO, PIN da calculadora, permissões do app) |
| `TB_VALKY_CONTATOS_CONFIANCA` | rede de confiança da usuária (até 5 contatos; FK para usuários) |
| `TB_VALKY_ALERTAS` | alerta disparado pela usuária (BO, localização, status) |
| `TB_VALKY_ATUALIZACOES_STATUS` | andamento do atendimento (FK para alertas) |

Status válidos: `ABERTO`, `VIATURA_A_CAMINHO`, `NO_LOCAL`, `ENCERRADO`. Perfis: `USUARIA`, `CONTATO_CONFIANCA`, `POLICIA`.

O provedor é definido por `DatabaseProvider`: `SqlServer` (Azure SQL, produção) ou `Oracle` (`scripts/script-bd-oracle.sql`).

## Endpoints e JSON do CRUD

Swagger com botão **Authorize** (Bearer JWT): `/swagger`. Exceto cadastro e login, os endpoints exigem token.

### Autenticação — `/api/auth`

| Operação | Método | Rota |
|---|---|---|
| Registrar e autenticar | POST | `/api/auth/registrar` |
| Login (obter JWT) | POST | `/api/auth/login` |
| Usuário autenticado | GET | `/api/auth/me` |

POST `/api/auth/registrar`:
```json
{
  "nome": "Maria da Silva",
  "email": "maria@exemplo.com",
  "cpf": "52998224725",
  "telefone": "11987654321",
  "senha": "Valky2026Segura",
  "perfil": "USUARIA",
  "codigoBo": "BO-2026-000123",
  "pinDisfarce": "1901",
  "possuiMedidaProtetiva": false,
  "permissaoMicrofone": true,
  "permissaoLocalizacao": true
}
```

POST `/api/auth/login`:
```json
{
  "email": "maria@exemplo.com",
  "senha": "Valky2026Segura"
}
```

### Usuários e rede de confiança — `/api/usuarios`

| Operação | Método | Rota |
|---|---|---|
| Create | POST | `/api/usuarios` |
| Read | GET | `/api/usuarios` e `/api/usuarios/{id}` |
| Update | PUT | `/api/usuarios/{id}` |
| Alterar senha | PATCH | `/api/usuarios/{id}/senha` |
| Delete | DELETE | `/api/usuarios/{id}` |
| Contatos (CRUD) | GET, POST, PUT, DELETE | `/api/usuarios/{usuarioId}/contatos-confianca[/{contatoId}]` |

PUT `/api/usuarios/{id}`:
```json
{
  "nome": "Maria da Silva Souza",
  "email": "maria@exemplo.com",
  "telefone": "11987654321",
  "perfil": "USUARIA",
  "codigoBo": "BO-2026-000123",
  "pinDisfarce": "1901",
  "possuiMedidaProtetiva": true,
  "permissaoMicrofone": true,
  "permissaoLocalizacao": true,
  "ativo": true
}
```

POST `/api/usuarios/{usuarioId}/contatos-confianca`:
```json
{
  "nome": "Ana Souza",
  "telefone": "11912345678",
  "email": "ana@exemplo.com",
  "parentesco": "Irmã",
  "notificarPorSms": true,
  "notificarPorApp": true
}
```

### Alertas — `/api/alertas`

| Operação | Método | Rota |
|---|---|---|
| Create | POST | `/api/alertas` |
| Read | GET | `/api/alertas` e `/api/alertas/{id}` |
| Update | PUT | `/api/alertas/{id}` |
| Delete | DELETE | `/api/alertas/{id}` |

POST:
```json
{
  "codigoBo": "BO-2026-000123",
  "latitude": -23.550520,
  "longitude": -46.633308
}
```

PUT:
```json
{
  "codigoBo": "BO-2026-000123",
  "latitude": -23.551000,
  "longitude": -46.634000,
  "status": "VIATURA_A_CAMINHO"
}
```

### Atualizações de status — `/api/atualizacoes-status`

| Operação | Método | Rota |
|---|---|---|
| Create | POST | `/api/atualizacoes-status` |
| Read | GET | `/api/atualizacoes-status`, `/api/atualizacoes-status?alertaId={id}` e `/api/atualizacoes-status/{id}` |
| Update | PUT | `/api/atualizacoes-status/{id}` |
| Delete | DELETE | `/api/atualizacoes-status/{id}` |

POST:
```json
{
  "alertaId": 1,
  "status": "VIATURA_A_CAMINHO",
  "observacao": "Viatura despachada",
  "responsavel": "Central 190"
}
```

PUT:
```json
{
  "alertaId": 1,
  "status": "NO_LOCAL",
  "observacao": "Viatura chegou ao local",
  "responsavel": "Central 190"
}
```

Consultas no banco (Query Editor do Azure SQL):
```sql
SELECT * FROM dbo.TB_VALKY_USUARIOS ORDER BY ID DESC;
SELECT * FROM dbo.TB_VALKY_CONTATOS_CONFIANCA ORDER BY ID DESC;
SELECT * FROM dbo.TB_VALKY_ALERTAS ORDER BY ID DESC;
SELECT * FROM dbo.TB_VALKY_ATUALIZACOES_STATUS ORDER BY ID DESC;
```

## Rodar localmente

```bash
dotnet user-secrets init --project src/Valky.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<connection-string-sql-server>" --project src/Valky.Api
dotnet user-secrets set "Jwt:ChaveSecreta" "<chave-com-32-caracteres-ou-mais>" --project src/Valky.Api
dotnet run --project src/Valky.Api
dotnet test tests/Valky.Tests
```

Para usar Oracle localmente: `DatabaseProvider=Oracle` e `ConnectionStrings:OracleConnection` via user-secrets. Os testes de integração usam o ambiente `Testing`, com o provedor EF Core InMemory.

## Declaração de uso de IA generativa

Ferramenta: Claude (Anthropic). Usada como apoio para estruturar o esqueleto da API, os scripts de Azure CLI e a pipeline YAML. Todo o conteúdo foi revisado, executado e entendido pelo grupo.
