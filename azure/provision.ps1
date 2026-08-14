<#
.SYNOPSIS
    Azure 资源创建脚本 - CloudNotes Demo
.DESCRIPTION
    本脚本用于创建 CloudNotes 所需的全部 Azure 资源。
    请逐段运行，每段之间需要在 Azure 门户手动配置部分内容。
    建议在运行前先修改脚本顶部的变量值。
.NOTES
    需要 Azure CLI 2.0+ 已安装并登录 (az login)
    用户需要对订阅有 Contributor 权限
#>

# ============================================
# 请修改以下变量为你自己的命名
# ============================================
$ResourceGroup = "rg-cloudnotes-demo"
$Location = "eastasia"
$AppServicePlanName = "asp-cloudnotes"
$AppServiceName = "app-cloudnotes-demo"
$FunctionAppName = "func-cloudnotes-demo"
$StorageAccountName = "stcloudnotesdemo"  # 必须全局唯一，全小写字母+数字
$KeyVaultName = "kv-cloudnotes-demo"      # 必须全局唯一
$ContainerAppEnvName = "cae-cloudnotes"
$ContainerAppName = "ca-cloudnotes-report"
$AcrName = "acrcloudnotesdemo"            # 必须全局唯一，全小写字母+数字

Write-Host "====== 第 1 步：创建资源组 ======" -ForegroundColor Green
az group create --name $ResourceGroup --location $Location

Write-Host "====== 第 2 步：创建存储账户 (Blob Storage) ======" -ForegroundColor Green
az storage account create `
    --name $StorageAccountName `
    --resource-group $ResourceGroup `
    --location $Location `
    --sku Standard_LRS `
    --kind StorageV2

$StorageConnStr = az storage account show-connection-string `
    --name $StorageAccountName `
    --resource-group $ResourceGroup `
    --query connectionString -o tsv

Write-Host "存储账户连接字符串: $StorageConnStr" -ForegroundColor Yellow
Write-Host "请保存此连接字符串，后续会存入 Key Vault" -ForegroundColor Yellow

Write-Host "====== 第 3 步：创建 Key Vault ======" -ForegroundColor Green
az keyvault create `
    --name $KeyVaultName `
    --resource-group $ResourceGroup `
    --location $Location `
    --sku standard

Write-Host "====== 第 4 步：向 Key Vault 添加 Secret ======" -ForegroundColor Green
az keyvault secret set `
    --vault-name $KeyVaultName `
    --name "BlobConnectionString" `
    --value $StorageConnStr

az keyvault secret set `
    --vault-name $KeyVaultName `
    --name "StorageAccountName" `
    --value $StorageAccountName

# ReportServiceUrl 先留空，等 Container Apps 创建后再填入
az keyvault secret set `
    --vault-name $KeyVaultName `
    --name "ReportServiceUrl" `
    --value "https://placeholder-url"

Write-Host "====== 第 5 步：创建 App Service Plan + App Service ======" -ForegroundColor Green
az appservice plan create `
    --name $AppServicePlanName `
    --resource-group $ResourceGroup `
    --location $Location `
    --sku F1

az webapp create `
    --name $AppServiceName `
    --resource-group $ResourceGroup `
    --plan $AppServicePlanName `
    --runtime "DOTNET:10.0"

Write-Host "====== 第 6 步：配置 App Service Settings ======" -ForegroundColor Green
$KeyVaultUri = "https://$KeyVaultName.vault.azure.net/"

az webapp config appsettings set `
    --name $AppServiceName `
    --resource-group $ResourceGroup `
    --settings `
        AppName="CloudNotes" `
        Environment="Production" `
        KeyVaultUri=$KeyVaultUri

Write-Host "====== 第 7 步：开启 App Service 的 Managed Identity ======" -ForegroundColor Green
az webapp identity assign `
    --name $AppServiceName `
    --resource-group $ResourceGroup

$AppServicePrincipalId = az webapp identity show `
    --name $AppServiceName `
    --resource-group $ResourceGroup `
    --query principalId -o tsv

Write-Host "App Service Principal ID: $AppServicePrincipalId" -ForegroundColor Yellow

Write-Host "====== 第 8 步：授权 App Service 访问 Key Vault ======" -ForegroundColor Green
az keyvault set-policy `
    --name $KeyVaultName `
    --resource-group $ResourceGroup `
    --object-id $AppServicePrincipalId `
    --secret-permissions get list

Write-Host "====== 第 9 步：创建 Function App ======" -ForegroundColor Green
az functionapp create `
    --name $FunctionAppName `
    --resource-group $ResourceGroup `
    --storage-account $StorageAccountName `
    --consumption-plan-location $Location `
    --runtime dotnet-isolated `
    --runtime-version 10 `
    --functions-version 4

Write-Host "====== 第 10 步：配置 Function App Settings ======" -ForegroundColor Green
az functionapp config appsettings set `
    --name $FunctionAppName `
    --resource-group $ResourceGroup `
    --settings `
        KeyVaultUri=$KeyVaultUri `
        BlobConnectionString="@Microsoft.KeyVault(SecretUri=$KeyVaultUri/secrets/BlobConnectionString/)"

Write-Host "====== 第 11 步：开启 Function App 的 Managed Identity ======" -ForegroundColor Green
az functionapp identity assign `
    --name $FunctionAppName `
    --resource-group $ResourceGroup

$FuncPrincipalId = az functionapp identity show `
    --name $FunctionAppName `
    --resource-group $ResourceGroup `
    --query principalId -o tsv

az keyvault set-policy `
    --name $KeyVaultName `
    --resource-group $ResourceGroup `
    --object-id $FuncPrincipalId `
    --secret-permissions get list

Write-Host "====== 第 12 步：创建 Azure Container Registry ======" -ForegroundColor Green
az acr create `
    --name $AcrName `
    --resource-group $ResourceGroup `
    --location $Location `
    --sku Basic `
    --admin-enabled true

Write-Host "====== 第 13 步：创建 Container Apps Environment + Container App ======" -ForegroundColor Green
az containerapp env create `
    --name $ContainerAppEnvName `
    --resource-group $ResourceGroup `
    --location $Location

az containerapp create `
    --name $ContainerAppName `
    --resource-group $ResourceGroup `
    --environment $ContainerAppEnvName `
    --image "mcr.microsoft.com/k8se/quickstart:latest" `
    --target-port 8080 `
    --ingress external `
    --min-replicas 0 `
    --max-replicas 1 `
    --env-vars `
        KeyVaultUri=$KeyVaultUri

Write-Host "====== 第 14 步：开启 Container App 的 Managed Identity ======" -ForegroundColor Green
az containerapp identity assign `
    --name $ContainerAppName `
    --resource-group $ResourceGroup `
    --system-assigned

$ContainerPrincipalId = az containerapp identity show `
    --name $ContainerAppName `
    --resource-group $ResourceGroup `
    --query principalId -o tsv

az keyvault set-policy `
    --name $KeyVaultName `
    --resource-group $ResourceGroup `
    --object-id $ContainerPrincipalId `
    --secret-permissions get list

Write-Host "====== 资源创建完成！ ======" -ForegroundColor Green
Write-Host "========================================="
Write-Host "以下是关键信息，请记录下来：" -ForegroundColor Yellow
Write-Host "  Resource Group:     $ResourceGroup"
Write-Host "  App Service:        $AppServiceName.azurewebsites.net"
Write-Host "  Function App:       $FunctionAppName"
Write-Host "  Container App:      $ContainerAppName"
Write-Host "  Key Vault URI:      $KeyVaultUri"
Write-Host "  Storage Account:    $StorageAccountName"
Write-Host "  ACR:                $AcrName.azurecr.io"
Write-Host "========================================="

Write-Host "`n====== 第 15 步（手动）：配置 GitHub OIDC 认证 ======" -ForegroundColor Cyan
Write-Host @"
在 Azure Portal 中执行以下操作：

1. 导航到 Microsoft Entra ID → 应用注册 → 新注册
   - 名称：gh-cloudnotes-oidc
   - 支持的账户类型：仅此组织目录中的账户

2. 进入该应用注册 → 证书和密码 → 联合凭据 → 添加凭据
   - 联合凭据方案：GitHub Actions
   - 组织：你的GitHub用户名
   - 存储库：你的repo名 (如 yourname/Azure-demo)
   - 实体类型：分支
   - 分支名称：main
   - 名称：gh-oidc-main

3. 进入该应用注册 → 概述，记录以下值：
   - 应用程序(客户端) ID → 作为 AZURE_CLIENT_ID
   - 目录(租户) ID    → 作为 AZURE_TENANT_ID

然后进入你的订阅 → 访问控制(IAM) → 添加角色分配：
   - 角色：参与者(Contributor)
   - 成员：选择上面创建的应用注册 gh-cloudnotes-oidc

最后在 GitHub Repo → Settings → Secrets and variables → Actions 添加以下 Secrets：
   - AZURE_CLIENT_ID
   - AZURE_TENANT_ID
   - AZURE_SUBSCRIPTION_ID
   - RESOURCE_GROUP
   - APP_SERVICE_NAME
   - FUNCTION_APP_NAME
   - CONTAINER_APP_NAME
   - ACR_NAME
"@ -ForegroundColor White

Write-Host "`n脚本执行完毕。请根据上面的指引继续手动配置。" -ForegroundColor Green

