#!/usr/bin/env bash
# =====================================================================
# Valky - Provisionamento da infraestrutura no Azure (Azure CLI)
#
# Cria: Resource Group, Azure SQL Server + Database (PaaS),
#       App Service Plan (Linux) + Web App .NET, connection string no Web App.
#
# Como executar (Azure Cloud Shell - Bash, ou terminal com az login feito):
#   chmod +x scripts/infra-azure.sh
#   ./scripts/infra-azure.sh
#
# A senha do SQL NÃO fica no script: é pedida na execução
# (ou lida da variável de ambiente SQL_PASSWORD).
# =====================================================================
set -euo pipefail

# ---------- Variáveis (podem ser sobrescritas por variáveis de ambiente) ----------
RM="${RM:-rm564995}"
LOCATION="${LOCATION:-brazilsouth}"
RESOURCE_GROUP="${RESOURCE_GROUP:-rg-valky}"
SQL_SERVER="${SQL_SERVER:-sql-valky-${RM}}"
SQL_DATABASE="${SQL_DATABASE:-valkydb}"
SQL_ADMIN="${SQL_ADMIN:-valkyadmin}"
APP_PLAN="${APP_PLAN:-plan-valky}"
APP_SKU="${APP_SKU:-B1}"
WEBAPP="${WEBAPP:-app-valky-${RM}}"
RUNTIME="${RUNTIME:-DOTNETCORE:10.0}"

if [ -z "${SQL_PASSWORD:-}" ]; then
  read -r -s -p "Defina a senha do administrador do SQL (min. 8 caracteres, maiúscula, minúscula, número e símbolo): " SQL_PASSWORD
  echo
fi

echo ">> Assinatura em uso:"
az account show --query "{assinatura:name, id:id}" -o table

# ---------- 1. Resource Group ----------
echo ">> [1/6] Criando Resource Group ${RESOURCE_GROUP} em ${LOCATION}..."
az group create --name "$RESOURCE_GROUP" --location "$LOCATION" -o none

# ---------- 2. Azure SQL Server ----------
echo ">> [2/6] Criando Azure SQL Server ${SQL_SERVER}..."
az sql server create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$SQL_SERVER" \
  --location "$LOCATION" \
  --admin-user "$SQL_ADMIN" \
  --admin-password "$SQL_PASSWORD" \
  -o none

# Permite que serviços do Azure (Web App e agente da Pipeline) acessem o banco
az sql server firewall-rule create \
  --resource-group "$RESOURCE_GROUP" \
  --server "$SQL_SERVER" \
  --name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0 \
  -o none

# ---------- 3. Azure SQL Database ----------
echo ">> [3/6] Criando banco ${SQL_DATABASE} (Basic)..."
az sql db create \
  --resource-group "$RESOURCE_GROUP" \
  --server "$SQL_SERVER" \
  --name "$SQL_DATABASE" \
  --service-objective Basic \
  --backup-storage-redundancy Local \
  -o none

# ---------- 4. App Service Plan ----------
echo ">> [4/6] Criando App Service Plan ${APP_PLAN} (${APP_SKU}, Linux)..."
az appservice plan create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$APP_PLAN" \
  --location "$LOCATION" \
  --is-linux \
  --sku "$APP_SKU" \
  -o none

# ---------- 5. Web App ----------
echo ">> [5/6] Criando Web App ${WEBAPP} (${RUNTIME})..."
az webapp create \
  --resource-group "$RESOURCE_GROUP" \
  --plan "$APP_PLAN" \
  --name "$WEBAPP" \
  --runtime "$RUNTIME" \
  -o none

# ---------- 6. Configuração do Web App (variáveis de ambiente) ----------
echo ">> [6/6] Configurando connection string e variáveis de ambiente..."
CONNECTION_STRING="Server=tcp:${SQL_SERVER}.database.windows.net,1433;Initial Catalog=${SQL_DATABASE};User ID=${SQL_ADMIN};Password=${SQL_PASSWORD};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

az webapp config connection-string set \
  --resource-group "$RESOURCE_GROUP" \
  --name "$WEBAPP" \
  --connection-string-type SQLAzure \
  --settings DefaultConnection="$CONNECTION_STRING" \
  -o none

az webapp config appsettings set \
  --resource-group "$RESOURCE_GROUP" \
  --name "$WEBAPP" \
  --settings ASPNETCORE_ENVIRONMENT=Production \
  -o none

if [ "$APP_SKU" != "F1" ]; then
  az webapp config set --resource-group "$RESOURCE_GROUP" --name "$WEBAPP" --always-on true -o none
fi

echo
echo "================ RECURSOS CRIADOS ================"
echo "Resource Group : ${RESOURCE_GROUP}"
echo "SQL Server     : ${SQL_SERVER}.database.windows.net"
echo "Database       : ${SQL_DATABASE}"
echo "SQL Admin      : ${SQL_ADMIN}"
echo "App Plan       : ${APP_PLAN} (${APP_SKU})"
echo "Web App        : https://${WEBAPP}.azurewebsites.net"
echo "Swagger        : https://${WEBAPP}.azurewebsites.net/swagger"
echo "=================================================="
