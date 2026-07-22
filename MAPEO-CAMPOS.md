# Mapeo de campos de usuario — OINV (Invoices) y ORIN (CreditNotes)

Completá la columna **"Nombre en la base nueva"** y con eso hago el reemplazo en el código.
Si un campo tiene el mismo nombre, dejalo vacío o poné `=`.

---

## 1. OINV — Facturas (`Invoices`)

### 1.1 Campos que el servicio **LEE** (filtros y armado del XML)

| # | Campo actual | Para qué sirve | Valores esperados | Nombre en la base nueva |
|---|---|---|---|---|
| 1 | `U_EXX_FE_CDC` | CDC del documento; vacío = pendiente de generar | 44 dígitos | |
| 2 | `U_EXX_FE_Estado` | estado del ciclo SIFEN | `NEN`,`GEN`,`ENV`,`AUT`,`NAU`,`OFF` | |
| 3 | `U_EXX_FE_CODERR` | código de error devuelto por SIFEN | numérico | |
| 4 | `U_DOCD` | marca "es documento electrónico" | `S` / `N` | |
| 5 | `U_CDOC` | código de tipo de documento | `01`, `04`… (se rellena a 2 con `PadLeft`) | |
| 6 | `U_EST` | establecimiento | `001` | |
| 7 | `U_PDE` | punto de expedición | `001` | |
| 8 | `U_TIM` | número de timbrado | 8 dígitos | |
| 9 | `U_FITE` | fecha de inicio de vigencia del timbrado | fecha | |
| 10 | `U_EXX_FE_TipoTran` | tipo de transacción SIFEN (`iTipTra`) | 1–13 | |
| 11 | `U_EXX_FE_IndPresencia` | indicador de presencia (`iIndPres`) | 1–5 | |

### 1.2 Campos que el servicio **ESCRIBE** (PATCH)

| # | Campo actual | Cuándo se escribe | Contenido | Nombre en la base nueva |
|---|---|---|---|---|
| 12 | `U_EXX_FE_CDC` | tras generar el CDC | 44 dígitos | |
| 13 | `U_EXX_FE_Estado` | tras respuesta SIFEN | `ENV`/`AUT`/`NAU`/`OFF` | |
| 14 | `U_EXX_FE_CODERR` | tras respuesta SIFEN | código | |
| 15 | `U_EXX_FE_DESERR` | tras respuesta SIFEN | descripción del error | |
| 16 | `U_EXX_FE_FECAUT` | solo si `AUT` | fecha/hora de autorización | |
| 17 | `U_EXX_FE_QR` | tras autorización | URL del QR | |
| 18 | `U_EXX_FE_MailEnviado` | tras enviar el correo | `SI` / `NO` | |
| 19 | `U_EXX_FE_MailError` | tras enviar el correo | `Enviado` o el error (200 chars) | |
| 20 | `U_EXX_FE_ANULACION_ESTADO` | evento de cancelación | `NEN`/`NAU`/`AUT` | |
| 21 | `U_EXX_FE_ANULACION_RESP` | evento de cancelación | respuesta SIFEN | |
| 22 | `U_EXX_FE_ANULACION_FECHA` | evento de cancelación | fecha | |

---

## 2. ORIN — Notas de crédito (`CreditNotes`)

Usa **los mismos** campos 1–9, 11–22 de arriba, salvo que **no** usa `U_EXX_FE_TipoTran`.
Además tiene estos tres propios:

| # | Campo actual | Para qué sirve | Valores esperados | Nombre en la base nueva |
|---|---|---|---|---|
| 23 | `U_NUMFC` | nº de factura asociada (documento asociado SIFEN) | `001-001-0000123` | |
| 24 | `U_TIMFC` | timbrado de la factura asociada | 8 dígitos | |
| 25 | `U_DASO` | fecha del documento asociado | fecha | |
| 26 | `U_EXX_FE_MotEmision` | motivo de emisión de la NC | 1–5 | |

---

## 3. Campos relacionados (por si también cambian)

No son de OINV/ORIN pero se consultan en la misma query y rompen igual si el nombre difiere:

| Objeto | Campo actual | Para qué | Nombre en la base nueva |
|---|---|---|---|
| OCRD (BusinessPartners) | `U_TIPCONT` | tipo de contribuyente del receptor | |
| OCRD | `U_CRSI` | condición del receptor | |
| OCRD | `U_CRID` | tipo de identificación del receptor (`iTipIDRec`) | |
| OCRD | `U_EXX_FE_TipoOperacion` | tipo de operación (`iTiOpe`) | |
| CRD1 (direcciones) | `U_EXX_FE_DEPT` / `U_EXX_FE_DIST` / `U_EXX_FE_BALO` | geografía del receptor | |

---

## 4. Alcance del cambio en el código

Los nombres aparecen en dos capas, hay que tocar las dos:

**Modelos** (propiedades C# deserializadas por Newtonsoft — el nombre de la propiedad *es* el
nombre del campo):
- [Models/SAP/FacturasResponse.cs](src/Models/SAP/FacturasResponse.cs)
- [Models/SAP/NotaCreditoResponse.cs](src/Models/SAP/NotaCreditoResponse.cs)
- [Models/SIFEN/Factura.cs](src/Models/SIFEN/Factura.cs), [Models/SIFEN/NotaCredito.cs](src/Models/SIFEN/NotaCredito.cs)

**Servicios** (queries OData `$select`/`$filter` y cuerpos de PATCH):
- [Services/FacturaService.cs](src/Services/FacturaService.cs) — líneas 19-26 y 631-640
- [Services/NotaCreditoService.cs](src/Services/NotaCreditoService.cs) — líneas 19-25 y 388-396
- [Services/EnvioSifenService.cs:556](src/Services/EnvioSifenService.cs:556) — PATCH principal
- [Services/EnvioSifenService.cs:757](src/Services/EnvioSifenService.cs:757) — PATCH cancelación
- [Services/EmailQueueService.cs:251](src/Services/EmailQueueService.cs:251) — PATCH de correo
- [Services/EventoServiceCanc.cs](src/Services/EventoServiceCanc.cs) — líneas 22 y 60
- [Services/SAPCDCService.cs](src/Services/SAPCDCService.cs) — orquestación

Volumen por campo (ocurrencias en `.cs`): `U_EXX_FE_CDC` 55, `U_CDOC` 43, `U_TIM` 43,
`U_EXX_FE_Estado` 35, `U_EST` 31, `U_PDE` 31, `U_FITE` 29, `U_EXX_FE_CODERR` 23, `U_NUMFC` 21,
`U_TIMFC` 10, el resto ≤7.

---

## 5. Ojo: además de los nombres, revisá los **valores**

Aunque los campos se llamen igual, la otra base puede usar otros códigos. Estos son los
literales que el código da por sentado:

- `U_EXX_FE_Estado`: arranca en `'NEN'` (sin enviar) y el servicio escribe `ENV`/`AUT`/`NAU`/`OFF`
  → [EnvioSifenService.cs:542](src/Services/EnvioSifenService.cs:542)
- `U_DOCD eq 'S'` como condición para procesar el documento
- `U_EXX_FE_ANULACION_ESTADO`: `'NAU'` en facturas, `'NEN'` en NC y remisiones (inconsistencia
  que ya existe hoy) → [EventoServiceCanc.cs:22](src/Services/EventoServiceCanc.cs:22) vs [:60](src/Services/EventoServiceCanc.cs:60)
- `U_EXX_FE_MailEnviado`: `'SI'` / `'NO'`
- Fechas hardcodeadas de corte en los filtros (`DocDate ge '20260301'`, `'20260201'`,
  `'2026-06-01'`) — hay que ajustarlas a la fecha de arranque en la base nueva.
