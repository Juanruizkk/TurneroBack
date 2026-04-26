# Secretos locales — dotnet user-secrets

## Por qué no usar appsettings.json para secretos

Los archivos `appsettings.json` y `appsettings.Development.json` están commiteados en el repo.
Poner valores reales ahí los expone en el historial de Git (ver alertas de GitGuardian).

La solución es **dotnet user-secrets**: un almacén local por máquina, fuera del repo,
que el runtime de ASP.NET Core fusiona automáticamente con la configuración al arrancar en `Development`.

Los valores se guardan en:
- **Windows:** `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`
- **Linux/macOS:** `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`

El `UserSecretsId` del proyecto es `8278d061-1783-4114-9a84-8a0fe7566bb4` (definido en `src/Api/TurneroApp.Api.csproj`).

---

## Setup inicial (una sola vez por máquina)

Todos los comandos se corren desde la carpeta `backend/`:

```bash
cd backend
```

### 1. Inicializar (ya está hecho — el csproj tiene el UserSecretsId)

```bash
dotnet user-secrets init --project src/Api
```

> Si el csproj ya tiene `<UserSecretsId>`, este comando no hace nada. Solo es necesario en proyectos nuevos.

---

## Setear cada secreto

### Conexión a la base de datos

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=localhost;Port=5433;Database=turnero;Username=postgres;Password=TU_PASSWORD_POSTGRES" \
  --project src/Api
```

### JWT

```bash
dotnet user-secrets set "Jwt:Secret" "una-clave-secreta-de-minimo-32-caracteres-aqui" --project src/Api
```

### SuperAdmin

```bash
dotnet user-secrets set "SuperAdmin:Password" "TuPasswordSuperAdmin!" --project src/Api
```

### Resend (emails transaccionales)

```bash
dotnet user-secrets set "Resend:ApiToken" "re_xxxxxxxxxxxxxxxxxxxx" --project src/Api
```

### MercadoPago

```bash
dotnet user-secrets set "MercadoPago:MasterAccessToken" "APP_USR-xxxx-xxxx" --project src/Api
dotnet user-secrets set "MercadoPago:WebhookSecret"     "tu_webhook_secret"  --project src/Api
```

### MercadoPago OAuth

```bash
dotnet user-secrets set "MercadoPagoOAuth:ClientId"     "TU_CLIENT_ID"     --project src/Api
dotnet user-secrets set "MercadoPagoOAuth:ClientSecret" "TU_CLIENT_SECRET" --project src/Api
dotnet user-secrets set "MercadoPagoOAuth:RedirectUri"  "https://TU-NGROK.ngrok-free.dev/api/mercadopago/oauth/callback" --project src/Api
```

### Cloudinary

```bash
dotnet user-secrets set "Cloudinary:CloudName" "tu_cloud_name" --project src/Api
dotnet user-secrets set "Cloudinary:ApiKey"    "123456789012345" --project src/Api
dotnet user-secrets set "Cloudinary:ApiSecret" "tu_api_secret"  --project src/Api
```

---

## Comandos útiles

### Ver todos los secretos seteados

```bash
dotnet user-secrets list --project src/Api
```

### Eliminar un secreto

```bash
dotnet user-secrets remove "Resend:ApiToken" --project src/Api
```

### Eliminar todos los secretos

```bash
dotnet user-secrets clear --project src/Api
```

---

## Cómo funciona en tiempo de ejecución

ASP.NET Core carga la configuración en este orden (cada nivel sobreescribe al anterior):

```
appsettings.json
  ↓ sobreescribe
appsettings.Development.json
  ↓ sobreescribe
dotnet user-secrets         ← solo cuando ASPNETCORE_ENVIRONMENT=Development
  ↓ sobreescribe
Variables de entorno del sistema
```

Cuando corrés `dotnet watch run --project src/Api`, el entorno es `Development` (definido en `launchSettings.json`)
y los user-secrets se cargan automáticamente. No hace falta ningún código extra.

---

## Producción / Servidor

En producción **no se usan user-secrets**. Los valores van en variables de entorno del sistema
o en el archivo `.env` del docker-compose (nunca commiteado).

Ejemplo en `docker-compose.prod.yml`:

```yaml
services:
  api:
    environment:
      - ConnectionStrings__DefaultConnection=Host=db;Port=5432;...
      - Jwt__Secret=clave-produccion-muy-larga
      - Resend__ApiToken=re_prod_xxxx
      - MercadoPago__MasterAccessToken=APP_USR-prod-xxxx
```

> Nota: en variables de entorno, los `:` de la jerarquía JSON se reemplazan por `__` (doble guión bajo).

---

## .gitignore recomendado

Asegurarse de que estos archivos nunca se commiteen:

```gitignore
# Secretos locales (no van al repo nunca)
src/Api/appsettings.Development.json

# Archivos de entorno
.env
.env.local
*.env
```

`appsettings.json` sí va al repo, pero solo con los placeholders `REPLACE_WITH_USER_SECRET`.
