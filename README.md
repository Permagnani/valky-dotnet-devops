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

Esta API (.NET 10, Minimal API, EF Core, Azure SQL) recebe os **alertas** e o **histórico de status** do atendimento.

ODS: 11 (cidades e comunidades sustentáveis), 10 (redução das desigualdades), 3 (saúde e bem-estar).

## Arquitetura macro

> TODO: inserir o desenho em `docs/arquitetura.png`

![Arquitetura](docs/arquitetura.png)

Fluxo: Azure Boards (tarefa) → Azure Repos (branch + PR com políticas) → Azure Pipelines CI (build + testes xUnit + artefato) → Release (executa `script-bd.sql` no Azure SQL e faz deploy no Azure App Service) → App mobile consome a API.

## Estrutura do repositório

```
├── azure-pipeline.yml        # Pipeline de Build (CI)
├── scripts/
│   ├── infra-azure.sh        # Provisionamento via Azure CLI
│   └── script-bd.sql         # DDL das tabelas (Azure SQL)
├── src/Valky.Api/            # Minimal API
└── tests/Valky.Tests/        # Testes unitários e de integração (xUnit)
```

## Infraestrutura (Azure CLI)

No Azure Cloud Shell (Bash):

```bash
git clone <url-do-azure-repos>
cd valky-devops
chmod +x scripts/infra-azure.sh
./scripts/infra-azure.sh
```

Recursos criados: Resource Group `rg-valky`, Azure SQL Server + Database `valkydb`, App Service Plan Linux e Web App `.NET 10`. A senha do banco é pedida na execução e fica apenas na configuração do Web App (connection string `DefaultConnection`), nunca no código.

## Tabelas

- `dbo.Alertas` — alerta disparado pela usuária (BO, localização, status).
- `dbo.AtualizacoesStatus` — andamento do atendimento (FK para `Alertas`).

Status válidos: `ABERTO`, `VIATURA_A_CAMINHO`, `NO_LOCAL`, `ENCERRADO`.

## Endpoints e JSON do CRUD

Swagger: `https://<webapp>.azurewebsites.net/swagger` · Health check: `/health`

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

Comprovação no banco (Query Editor do Azure SQL):
```sql
SELECT * FROM dbo.Alertas ORDER BY Id DESC;
SELECT * FROM dbo.AtualizacoesStatus ORDER BY Id DESC;
```

## Rodar localmente

```bash
# defina a connection string sem gravá-la no código
dotnet user-secrets init --project src/Valky.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<sua-connection-string>" --project src/Valky.Api
dotnet run --project src/Valky.Api
dotnet test tests/Valky.Tests
```

## Declaração de uso de IA generativa

Ferramenta: Claude (Anthropic). Usada como apoio para estruturar o esqueleto da API, os scripts de Azure CLI e a pipeline YAML. Todo o conteúdo foi revisado, executado e entendido pelo grupo.
