# Azure Key Vault for ApiTuneScore

The API loads secrets from Azure Key Vault when `KeyVault:VaultUri` is set (for example via application setting `KeyVault__VaultUri` in Azure App Service or Container Apps). Configuration order: `appsettings.json`, environment variables, optional `appsettings.Secrets.json`, then Key Vault (vault wins on conflicts).

**Compared to [ApiOAuthEmpleadosACV](https://github.com/AlvaroCascoV/ApiOAuthEmpleadosACV):** that sample uses `SecretClient.GetSecret` only for the **SQL** connection string; the JWT `UserData` encryption key is read from normal configuration (`ClavesCrypto:Key` in `appsettings.json`). ApiTuneScore additionally supports **`AddAzureKeyVault`** (secrets become configuration keys) and an optional **imperative** crypto secret via `ClavesCrypto:KeyVaultSecretName`, matching the same `GetSecret` style as the reference SQL pattern.

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
| `ClavesCrypto:Key` | `ClavesCrypto--Key` (two dashes between `ClavesCrypto` and `Key`) |
| `ClavesCrypto:KeyVaultSecretName` | Not a vault secret name by itself—set this **configuration value** (App Service: `ClavesCrypto__KeyVaultSecretName`) to the **Key Vault secret name as stored** (e.g. `ClavesCrypto--Key` or any custom name like `TuneScoreJwtCrypto`) to load the crypto passphrase via `SecretClient.GetSecret` at startup. When set, this value **overrides** `ClavesCrypto:Key` from configuration for encryption only. |
| `EmailSettings:Password` | `EmailSettings--Password` |

`ClavesCrypto:Key` encrypts user profile data inside the JWT `UserData` claim (any non-empty string is accepted; the API derives a 32-byte AES key with SHA256).

**Naming:** The vault secret must be named `ClavesCrypto--Key` so ASP.NET maps it to `ClavesCrypto:Key`. If the secret was created as `ClavesCrypto-Key` (one dash), it becomes a flat configuration key; the app also reads `ClavesCrypto-Key` so that typo still works.

**Imperative crypto key (`ClavesCrypto:KeyVaultSecretName`):** When `KeyVault:VaultUri` is set, the app registers a DI **`SecretClient`** (same pattern family as ApiOAuthEmpleadosACV). If `ClavesCrypto:KeyVaultSecretName` is non-empty, startup calls `GetSecret` with that **exact** vault secret name and uses the returned value as the crypto passphrase. Leave it empty to rely only on `AddAzureKeyVault` + `ClavesCrypto:Key` / `ClavesCrypto-Key`.

The key is loaded **after** the web host is built so Key Vault and App Service overrides are applied (empty values in `appsettings.json` are normal when secrets live only in the vault).

Other settings (issuer, audience, SMTP host, CORS) can remain in `appsettings.json` or environment variables.

## JWT notes

- New tokens carry encrypted profile fields in `UserData` and a plain `role` claim (same pattern as ApiOAuthEmpleadosACV).
- Tokens issued **before** this change may still work briefly: if the JWT has plain `NameIdentifier` / name / email claims and no `UserData`, validation accepts them as legacy.
- If `ClavesCrypto:Key` is still missing after configuration load, the API **still starts** (OpenAPI/Scalar work) but **login returns 503** until you set `ClavesCrypto__Key` or the correct Key Vault secret name. This avoids Azure **HTTP 500.30** when the setting was forgotten on deploy.

## Local development

- **Without Key Vault:** Create `appsettings.Secrets.json` (gitignored) with the same keys as above, or use environment variables / user secrets.
- **With Key Vault:** Set `KeyVault__VaultUri` (or `KeyVault:VaultUri` in JSON) and sign in with the Azure CLI (`az login`) or Visual Studio so `DefaultAzureCredential` can authenticate.
