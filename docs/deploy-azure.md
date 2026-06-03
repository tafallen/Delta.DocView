# Triangle Step Library — Azure Deployment Guide

Deploys the Blazor WASM hosted app as an **Azure Container App** backed by an **Azure Files** mount for the step library JSON.

---

## Azure resources used

| Resource | Name | Notes |
|---|---|---|
| Subscription | Dev/Test (`3518aea0-24ed-4812-a8a1-39de0821b8d0`) | |
| Resource group | `rg-docview` | Create below |
| Region | `uksouth` | Closest to users |
| Container Registry | `trinitatuminternal.azurecr.io` | Existing; in `rg-internal--container-registry` |
| Storage account | `strdocview` | Azure Files for library mount |
| File share | `docview-data` | Mounted at `/data` in the container |
| ACA environment | `cae-docview` | Shared managed environment |
| Container App | `ca-docview` | The running app |

---

## Prerequisites

```powershell
# Log in
az login

# Set the Dev/Test subscription as default
az account set --subscription 3518aea0-24ed-4812-a8a1-39de0821b8d0

# Confirm
az account show --query "name" -o tsv
```

Install the Container Apps extension if not present:
```powershell
az extension add --name containerapp --upgrade
```

---

## Step 1 — Log in to the container registry

```powershell
az acr login --name trinitatuminternal
```

---

## Step 2 — Build and push the image

Run from the repo root (`C:\dev\Delta.DocView`):

```powershell
$TAG = "trinitatuminternal.azurecr.io/docview:latest"
podman build -t $TAG .
podman push $TAG
```

> For production releases, use a versioned tag instead of `:latest`:
> ```powershell
> $VERSION = "1.2.0"   # match library version
> $TAG = "trinitatuminternal.azurecr.io/docview:$VERSION"
> ```

---

## Step 3 — Create the resource group

```powershell
az group create --name rg-docview --location uksouth
```

---

## Step 4 — Create the storage account and upload the library file

```powershell
# Storage account (name must be globally unique, 3-24 lowercase alphanumeric)
az storage account create `
  --name strdocview `
  --resource-group rg-docview `
  --location uksouth `
  --sku Standard_LRS `
  --kind StorageV2 `
  --allow-blob-public-access false

# Get the storage key
$STORAGE_KEY = az storage account keys list `
  --account-name strdocview `
  --resource-group rg-docview `
  --query "[0].value" -o tsv

# Create the file share
az storage share create `
  --name docview-data `
  --account-name strdocview `
  --account-key $STORAGE_KEY `
  --quota 1

# Upload the step library file
# Replace the path below with your actual library file path
az storage file upload `
  --share-name docview-data `
  --account-name strdocview `
  --account-key $STORAGE_KEY `
  --source "C:\dev\Delta.DocView\triangle-step-libraryv1.1.json" `
  --path "step-library.json"
```

> **Updating the library later:** Simply re-run the `az storage file upload` command with the new file. No image rebuild needed — just restart the Container App:
> ```powershell
> az containerapp revision restart --name ca-docview --resource-group rg-docview --revision-name $(az containerapp revision list --name ca-docview --resource-group rg-docview --query "[0].name" -o tsv)
> ```

---

## Step 5 — Create the ACA environment

```powershell
az containerapp env create `
  --name cae-docview `
  --resource-group rg-docview `
  --location uksouth
```

---

## Step 6 — Link the Azure Files storage to the ACA environment

```powershell
az containerapp env storage set `
  --name cae-docview `
  --resource-group rg-docview `
  --storage-name docview-files `
  --azure-file-account-name strdocview `
  --azure-file-account-key $STORAGE_KEY `
  --azure-file-share-name docview-data `
  --access-mode ReadOnly
```

---

## Step 7 — Grant ACA pull access to the ACR

```powershell
# Get the ACR resource ID
$ACR_ID = az acr show `
  --name trinitatuminternal `
  --query id -o tsv

# Create a service principal for ACA to pull images
$SP = az ad sp create-for-rbac `
  --name "sp-docview-acr-pull" `
  --role AcrPull `
  --scope $ACR_ID `
  --output json | ConvertFrom-Json

$SP_CLIENT_ID = $SP.appId
$SP_CLIENT_SECRET = $SP.password
```

> Store `$SP_CLIENT_SECRET` securely (Key Vault). You will need it for Step 8.

---

## Step 8 — Deploy the Container App

```powershell
az containerapp create `
  --name ca-docview `
  --resource-group rg-docview `
  --environment cae-docview `
  --image "trinitatuminternal.azurecr.io/docview:latest" `
  --registry-server trinitatuminternal.azurecr.io `
  --registry-username $SP_CLIENT_ID `
  --registry-password $SP_CLIENT_SECRET `
  --target-port 8080 `
  --ingress external `
  --min-replicas 0 `
  --max-replicas 2 `
  --cpu 0.5 `
  --memory 1.0Gi `
  --env-vars `
    ASPNETCORE_ENVIRONMENT=Production `
    DOCVIEW_LIBRARY_PATH=/data/step-library.json `
  --secrets `
    "acrpassword=$SP_CLIENT_SECRET" `
  --volume-mounts "docview-files:/data"
```

> After creation, get the app URL:
> ```powershell
> az containerapp show --name ca-docview --resource-group rg-docview --query "properties.configuration.ingress.fqdn" -o tsv
> ```

---

## Step 9 — Entra ID app registration (manual, done once)

The app requires Entra ID OIDC in production (`ASPNETCORE_ENVIRONMENT=Production`). Create the app registration in the Azure Portal:

1. **Azure Portal** → **Microsoft Entra ID** → **App registrations** → **New registration**
2. Name: `Triangle Step Library`
3. Supported account types: `Accounts in this organizational directory only`
4. Redirect URI: `Web` → `https://<your-aca-fqdn>/signin-oidc`
5. After creation, note the **Application (client) ID** and **Directory (tenant) ID**
6. Go to **Certificates & secrets** → **New client secret** → copy the value immediately
7. Go to **Authentication** → ensure `ID tokens` is checked under implicit grant

Then set the secrets on the Container App (never in code):

```powershell
az containerapp secret set `
  --name ca-docview `
  --resource-group rg-docview `
  --secrets `
    "azuread-tenantid=<YOUR_TENANT_ID>" `
    "azuread-clientid=<YOUR_CLIENT_ID>" `
    "azuread-clientsecret=<YOUR_CLIENT_SECRET>"

az containerapp update `
  --name ca-docview `
  --resource-group rg-docview `
  --set-env-vars `
    "AzureAd__TenantId=secretref:azuread-tenantid" `
    "AzureAd__ClientId=secretref:azuread-clientid" `
    "AzureAd__ClientSecret=secretref:azuread-clientsecret" `
    "AzureAd__CallbackPath=/signin-oidc" `
    "AzureAd__Instance=https://login.microsoftonline.com/"
```

---

## Step 10 — Verify health

```powershell
$FQDN = az containerapp show `
  --name ca-docview `
  --resource-group rg-docview `
  --query "properties.configuration.ingress.fqdn" -o tsv

Invoke-WebRequest -Uri "https://$FQDN/health" -UseBasicParsing | Select-Object StatusCode, Content
```

Expected: `200 {"status":"healthy"}`

---

## Updating the app (new image version)

```powershell
# Build and push a new image
podman build -t trinitatuminternal.azurecr.io/docview:latest .
podman push trinitatuminternal.azurecr.io/docview:latest

# Deploy the new revision
az containerapp update `
  --name ca-docview `
  --resource-group rg-docview `
  --image trinitatuminternal.azurecr.io/docview:latest
```

Azure Container Apps creates a new revision and gradually shifts traffic to it.

---

## Development mode (bypass auth, quick test)

To spin up a dev instance using Development environment (no Entra ID required):

```powershell
az containerapp update `
  --name ca-docview `
  --resource-group rg-docview `
  --set-env-vars ASPNETCORE_ENVIRONMENT=Development
```

> Remember to revert to `Production` before sharing the URL with users.

---

## Tear down

```powershell
az group delete --name rg-docview --yes
```
This deletes the Container App, ACA environment, and storage account in one step. The ACR image and the service principal remain — remove them manually if needed.

---

## Cost estimate (UK South, Basic ACR already paid)

| Resource | SKU | Approx monthly cost |
|---|---|---|
| Container App (0.5 vCPU, 1GB, ~8h/day) | Consumption | ~£3–8 |
| Storage account + 1GB file share | Standard LRS | ~£0.50 |
| ACA environment | Consumption | Free (no dedicated) |
| **Total** | | **~£4–9/month** |

Scales to zero when idle — no charges for pods that aren't running.
