# AGENTS.md

## Agente: `delivery-agent`
Rol: implementar cambios de producto sin romper deploy ni seguridad.

### Responsabilidades
1. Hacer cambios de código end-to-end (API, frontend, compose, env/docs).
2. Mantener estándar de naming `ReciboSueldo` / `recibo-sueldo`.
3. Mantener configuración productiva en `.env`.
4. Entregar siempre con resumen de impacto y archivos modificados.

### Criterios de salida
1. No quedan referencias nuevas al naming viejo (`Recibos`, `/recibos`).
2. Variables nuevas documentadas en `.env.example`.
3. Rutas frontend/API coherentes entre sí.

---

## Agente: `qa-agent`
Rol: ejecutar validación técnica antes de deploy o merge.

### Suite QA estándar
1. Validación estática
- Buscar referencias rotas:
  - `rg -n "Recibos|/recibos|services/recibos|pages/Recibos" .`
- Verificar env/config:
  - `.env.example` contiene todas las keys usadas en producción.

2. Build backend/frontend
- API:
  - `dotnet restore SAED-PortalEmpleado.Api/SAED-PortalEmpleado.Api.csproj`
  - `dotnet build SAED-PortalEmpleado.Api/SAED-PortalEmpleado.Api.csproj -v minimal`
- Frontend:
  - `cd frontend/portal-empleado && npm ci && npm run build`

3. Compose y despliegue
- `docker compose -f docker-compose.prod.yml config`
- `docker compose -f docker-compose.prod.yml up -d --build`
- `docker compose -f docker-compose.prod.yml ps`
- `docker compose -f docker-compose.prod.yml logs --tail=200 api web`

4. Smoke funcional
- `GET /api/features` => 200
- `GET /api/auth/me` => 200/302 según sesión
- `GET /api/recibo-sueldo` autenticado => 200
- `GET /api/recibo-sueldo/{id}/pdf` => 200 + `application/pdf`

5. OAuth Google
- Confirmar redirect URI exacto en GCP:
  - `https://mi.saed.digital/signin-google`
- Validar ausencia de:
  - `redirect_uri_mismatch`
  - `Correlation failed`

6. ReciboSueldo (reglas de negocio)
- Solo hasta 12 meses hacia atrás (o valor de `ReciboSueldo__MaxMonthsBack`).
- Considerar SAC `YYYYs1` (junio) y `YYYYs2` (diciembre).
- Usuario solo ve recibos de su propio CUIL.

### Formato de reporte QA
1. Resultado general: `PASS` / `FAIL`.
2. Hallazgos ordenados por severidad (`alta`, `media`, `baja`).
3. Evidencia concreta (comando + salida resumida).
4. Lista de acciones recomendadas para pasar a producción.

---

## Cómo invocar estos agentes en próximas conversaciones
1. Para implementar:
- "Usa delivery-agent: <cambio solicitado>"

2. Para probar:
- "Usa qa-agent: corré pruebas completas y dame reporte"
- "Usa qa-agent: validá OAuth + ReciboSueldo en prod"

