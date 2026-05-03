# Azure Key Vault for ApiTuneScore

The API loads secrets from Azure Key Vault when `KeyVault:VaultUri` is set (for example via application setting `KeyVault__VaultUri` in Azure App Service or Container Apps). Configuration order: `appsettings.json`, environment variables, optional `appsettings.Secrets.json`, then Key Vault (vault wins on conflicts).

## App configuration

Set the vault URL for each environment:

| Setting | Example |
|--------|---------|
| `KeyVault__VaultUri` | `https://your-vault-name.vault.azure.net/` |

Enable **system-assigned** or **user-assigned managed identity** on the hosting resource. Grant that identity **Key Vault Secrets User** (RBAC) on the vault (or an equivalent access policy on vaults that use the classic permission model).

## Secret names in Key Vault

ASP.NET Core maps hierarchical keys with `--` instead of `:`.

| Configuration key | Key Vault secret name |
|-------------------|------------------------|
| `ConnectionStrings:TuneScoreDBAzure` | `ConnectionStrings--TuneScoreDBAzure` |
| `ApiOAuthToken:SecretKey` | `ApiOAuthToken--SecretKey` |
| `EmailSettings:Password` | `EmailSettings--Password` |

Other settings (issuer, audience, SMTP host, CORS) can remain in `appsettings.json` or environment variables.

## Local development

- **Without Key Vault:** Create `appsettings.Secrets.json` (gitignored) with the same keys as above, or use environment variables / user secrets.
- **With Key Vault:** Set `KeyVault__VaultUri` (or `KeyVault:VaultUri` in JSON) and sign in with the Azure CLI (`az login`) or Visual Studio so `DefaultAzureCredential` can authenticate.
