# INSTRUCTION.md

## Objetivo del proyecto
Portal Empleado SAED con:
- Login Google (dominio permitido: `saed.ar`).
- API .NET + frontend React.
- Módulo de recibos de sueldo unificado bajo nombre `ReciboSueldo`.
- Deploy por Docker Compose (dev/prod) con configuración en `.env`.

## Estado funcional actual (base de trabajo)
1. Naming estándar adoptado:
- Ruta frontend: `/recibo-sueldo`.
- Ruta API: `/api/recibo-sueldo`.
- Config: `ReciboSueldo__*` / `ReciboSueldo:*`.
- Carpeta de datos local: `recibo-sueldo/`.

2. ReciboSueldo:
- Fuente local JSON por CUIL y período.
- Estructura esperada: carpeta por período dentro de `recibo-sueldo/` (ej: `202601/`, `2025-11/`, `2025s1/`).
- Soporte S3 (AWS) con `KeyTemplate`.
- Límite de antigüedad configurable por `ReciboSueldo__MaxMonthsBack` (default 12).
- Incluye períodos SAC (`YYYYs1`, `YYYYs2`).

3. Producción:
- Base actual: `Sqlite`.
- `docker-compose.prod.yml` usa `.env` y monta:
  - `./recibo-sueldo:/app/recibo-sueldo:ro`
  - `./secrets/saed-rrhh-sa.json` (vía `Directory__ServiceAccountHostPath`) hacia `/app/secrets/saed-rrhh-sa.json`

## Reglas de implementación
1. No hardcodear secretos en código ni en `appsettings*.json` versionado.
2. Toda configuración desplegable debe poder salir de `.env`.
3. Mantener naming consistente: usar solo `ReciboSueldo` / `recibo-sueldo`.
4. Evitar romper compatibilidad de endpoints sin aviso explícito.
5. Cualquier cambio en rutas o env vars debe actualizar docs y compose.

## Reglas de seguridad/despliegue
1. `saed-rrhh-sa.json` no se copia a imagen Docker; se monta como volumen.
2. Validar en Google Cloud Console redirect URI exacto:
- `https://mi.saed.digital/signin-google`
3. Verificar cookies detrás de proxy con HTTPS real y host correcto.
4. En producción, no habilitar `Authentication__DevLogin__Enabled=true`.

## Checklist de pruebas mínimas por cambio
1. Configuración:
- `docker compose -f docker-compose.prod.yml config` sin errores.
- Variables nuevas reflejadas en `.env.example`.

2. Backend:
- Build API exitoso (`dotnet build` del csproj API).
- Smoke endpoints:
  - `GET /api/features`
  - `GET /api/auth/me`
  - `GET /api/recibo-sueldo`

3. Frontend:
- Build frontend exitoso.
- Navegación a `/recibo-sueldo` funcional.

4. OAuth Google:
- Login redirige y vuelve sin `redirect_uri_mismatch`.
- Sin error de correlación por cookies (`Correlation failed`).

5. ReciboSueldo:
- Lista de períodos solo dentro de ventana permitida.
- SAC visible cuando corresponde.
- `GET /api/recibo-sueldo/{id}/pdf` retorna PDF válido para período permitido.

## Forma de pedir trabajo (recomendado)
- "Aplicá cambio + ejecutá checklist mínimo".
- "Corré pruebas QA completas de ReciboSueldo".
- "Prepará deploy prod y validá configuración efectiva".
