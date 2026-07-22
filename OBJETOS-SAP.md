# Inventario de tablas y campos de usuario

Todo lo que el servicio lee o escribe, clasificado por **quién lo crea**. Esto define qué se
puede automatizar y qué no.

---

## Resumen: qué se puede crear automáticamente

| Grupo | Objetos | ¿Automatizable? | Cómo |
|---|---|---|---|
| **A. Tablas HANA propias** | `SAP_SIFEN.DOCUMENTOS_SIFEN`, `SAP_SIFEN.COLA_EMAILS` | ✅ Sí, el servicio ya lo hacía | `CREATE COLUMN TABLE` por ODBC al arrancar |
| **B. UDT propias del proyecto** | `@U_CERTIFICADOS`, `@U_CONFIG_EMAIL` | ✅ Sí | Service Layer `UserTablesMD` + `UserFieldsMD` |
| **C. UDF que el servicio escribe** | `U_EXX_FE_*` en OINV/ORIN/ODLN | ⚠️ Sí técnicamente, pero normalmente ya existen | Service Layer `UserFieldsMD` |
| **D. Objetos del addon de localización PY** | todo `EPY_*` y los UDF de negocio (`U_TIM`, `U_EST`, `U_PDE`, …) | ❌ **No** — no crearlos a mano | Instalar el addon en la empresa |

> **Importante:** si estás moviendo el servicio a otro **servidor** pero apuntando a las
> **mismas empresas SAP**, no hay que crear nada del grupo B/C/D — ya existen en la base.
> Esta lista aplica cuando se da de alta una **empresa nueva**.

---

## A. Tablas propias en HANA (esquema `SAP_SIFEN`)

No son de SAP: son del servicio. Se acceden por ODBC directo, no por Service Layer.

| Tabla | Uso | Código |
|---|---|---|
| `SAP_SIFEN.DOCUMENTOS_SIFEN` | bitácora de cada envío a SIFEN (XML, estado, respuesta) | [LoggerSifenService.cs:128](src/Services/LoggerSifenService.cs:128) |
| `SAP_SIFEN.COLA_EMAILS` | cola de correos pendientes con PDF+XML | [EmailQueueRepository.cs:11](src/Services/EmailQueueRepository.cs:11) |

**Automatizable: sí, y ya estuvo implementado.** El método `EnsureTableExists()` creaba
`COLA_EMAILS` al construir el repositorio; se eliminó en el commit `7c7d1b6` porque la tabla
ya existía en producción. Se puede restaurar y extender a `DOCUMENTOS_SIFEN`:

```csharp
// patrón original (git show 7c7d1b6^:src/Services/EmailQueueRepository.cs)
cmd.CommandText = $"CREATE COLUMN TABLE {Schema}.{Table} ( ... )";
cmd.ExecuteNonQuery();
// catch OdbcException con SQLState 42S01 -> ya existe, seguir
```

El DDL completo de ambas tablas está en [DEPLOY.md](DEPLOY.md) sección 6.

---

## B. UDT propias del proyecto (Service Layer)

Estas dos sí son "del servicio" y hay que crearlas por empresa.

### `@U_CERTIFICADOS` — certificado de firma digital
Consultada en [EnvioSifenService.cs:443](src/Services/EnvioSifenService.cs:443) y
[SAPCDCService.cs:1174](src/Services/SAPCDCService.cs:1174) con filtro `U_ACTIVO eq 'Y'`.

| Campo | Tipo | Contenido |
|---|---|---|
| `U_ARCHIVO` | Texto (Memo) | el `.pfx` en **base64** |
| `U_PWD` | Alfanumérico | contraseña del certificado |
| `U_ACTIVO` | Alfanumérico (1) | `Y` / `N` |

### `@U_CONFIG_EMAIL` — configuración SMTP y plantillas
Consultada en [EmailQueueService.cs:523](src/Services/EmailQueueService.cs:523) filtrando por
`U_BASE_DATOS eq '<CompanyDB>'`.

| Campo | Tipo | Contenido |
|---|---|---|
| `U_BASE_DATOS` | Alfanumérico | nombre de la CompanyDB |
| `U_CORREO` | Alfanumérico | remitente |
| `U_SMTP` | Alfanumérico | host SMTP |
| `U_PUERTO` | Numérico | puerto (default 587) |
| `U_PWD` | Alfanumérico | contraseña **encriptada** (`EmailService.DesencriptarContrasena`) |
| `U_SSL` | Alfanumérico (1) | `Y` / `N` |
| `U_TPL_FAC` / `U_TPL_NC` | Texto | plantilla HTML del correo |
| `U_RPT_FAC` / `U_RPT_NC` | Alfanumérico (254) | **ruta al `.rpt`** de Crystal |

**Automatizable: sí.** Vía Service Layer con el usuario `manager`:

```http
POST /b1s/v1/UserTablesMD
{ "TableName": "CERTIFICADOS", "TableDescription": "Certificados SIFEN",
  "TableType": "bott_NoObject" }

POST /b1s/v1/UserFieldsMD
{ "TableName": "@CERTIFICADOS", "Name": "ARCHIVO", "Description": "PFX base64",
  "Type": "db_Memo", "EditSize": 0 }

POST /b1s/v1/UserFieldsMD
{ "TableName": "@CERTIFICADOS", "Name": "PWD", "Description": "Password",
  "Type": "db_Alpha", "EditSize": 100 }
```

Notas del API:
- En `UserTablesMD` el nombre va **sin** `@` y sin `U_`; SAP lo prefija.
- En `UserFieldsMD` el `TableName` va **con** `@`, y el `Name` **sin** `U_`.
- `UserFieldsMD` no acepta `PATCH` de `Type`/`EditSize` — para cambiar tipo hay que borrar y recrear.
- Requiere que ningún usuario esté conectado a la empresa en algunos casos (bloqueo de metadatos).

### Nombres reales vs. los del código
El código consulta `U_CERTIFICADOS` y `U_CONFIG_EMAIL` como **entidades** del Service Layer,
lo que implica que las UDT en SAP se llaman literalmente `@U_CERTIFICADOS` y `@U_CONFIG_EMAIL`
(es decir, se crearon con `TableName = "U_CERTIFICADOS"`, quedando el prefijo duplicado).
Al crearlas en una empresa nueva hay que respetar ese nombre exacto o el `GetAsync` falla con 404.

---

## C. UDF que el servicio **escribe** en documentos de marketing

Se aplican sobre `Invoices` (OINV), `CreditNotes` (ORIN) y `DeliveryNotes` (ODLN):

| Campo | Escrito por | Contenido |
|---|---|---|
| `U_EXX_FE_CDC` | [EnvioSifenService.cs:556](src/Services/EnvioSifenService.cs:556) | CDC de 44 dígitos |
| `U_EXX_FE_Estado` | idem | `NEN` → `GEN` → `AUT` / `REC` |
| `U_EXX_FE_CODERR` / `U_EXX_FE_DESERR` | idem | código y descripción de error SIFEN |
| `U_EXX_FE_FECAUT` | idem | fecha de autorización |
| `U_EXX_FE_QR` | idem | URL del QR |
| `U_EXX_FE_MailEnviado` / `U_EXX_FE_MailError` | [EmailQueueService.cs](src/Services/EmailQueueService.cs) | control de envío de correo |
| `U_EXX_FE_ANULACION_ESTADO` / `_FECHA` / `_RESP` | [EventoServiceCanc.cs](src/Services/EventoServiceCanc.cs) | evento de cancelación |
| `U_EXX_FE_INUTILIZA_*` (ESTADO, FECHA, RESP, GEN, CODERR, DESERR) | [EventoService.cs](src/Services/EventoService.cs) | evento de inutilización, sobre `@EPY_DVAN` |

**Automatizable: sí** con `UserFieldsMD` (`TableName: "OINV"`, `Name: "EXX_FE_CDC"`, …), pero
el prefijo `EXX` indica que **vienen del addon**, así que en una empresa con el addon instalado
ya existen. Crearlos a mano solo si confirmás que faltan.

---

## D. Objetos del addon de localización Paraguay — **no crear a mano**

Todo el prefijo `EPY_` es un modelo de datos de terceros, con formularios, numeración y
validaciones propias. El servicio solo los **lee**:

| UDT | Para qué | Campos que lee el servicio |
|---|---|---|
| `@EPY_PLPY` / `EPY_DEMPCollection` | datos del emisor (empresa) | `U_TIPCONT`, `U_DEPT`, `U_DIST`, `U_BALO`, `U_NUMCASA`, `U_DRUC`, `U_DVEMI`, `U_NEMP`, `U_DIRA`, `U_EMAIL`, `U_PHONE` |
| `@EPY_DPTO` / `@EPY_DIST` / `@EPY_BALO` | geografía SIFEN | `U_NDEP`, `U_NCIU`, `U_NLOC` |
| `@EPY_ACG` / `EPY_ACEGRACollection` | actividades económicas | `U_CACEG`, `U_DACEG` |
| `@EPY_OCG` / `EPY_OBLICollection` | obligaciones fiscales | `U_COBLI`, `U_NOBLI` |
| `@EPY_NRDE` | datos de transporte de la nota de remisión | `U_FINI`, `U_FFIN`, `U_MEMI`, `U_REFL`, `U_TITRA`, `U_KMR`, `U_NVUE`, `U_TIVE`, `U_IDVE` |
| `@EPY_TRAN` | transportistas | `U_NOMT`, `U_RUCT`, `U_DOMT`, `U_NIDT`, `U_NACT`, `U_DIRC`, `U_NIDC`, `U_NACH`, `U_NOAG`, `U_RUCA`, `U_DIRA`, `U_DVEA` |
| `@EPY_VEHI` | vehículos | `U_TIVE`, `U_MARC`, `U_NRMA`, `U_DAVE`, `U_NIDV`, `U_MOD`, `U_NVUE` |
| `@EPY_DVAN` | documentos a inutilizar | `U_TDOC`, `U_ETTE`, `U_PETE`, `U_NROD`, `U_NROH`, `U_TIM`, `U_SFTE`, `U_FDOC`, `U_ANUD` |

Y los UDF de negocio en documentos y socios, también del addon:

- **Documentos** (OINV/ORIN/ODLN): `U_CDOC`, `U_EST`, `U_PDE`, `U_TIM`, `U_FITE`, `U_DOCD`,
  `U_NUMFC`, `U_TIMFC`, `U_DASO`, `U_NORE`, `U_FECHAV`
- **Socios de negocio** (OCRD): `U_TIPCONT`, `U_CRSI`, `U_CRID`, `U_EXX_FE_TipoOperacion`
- **Direcciones** (CRD1): `U_EXX_FE_DEPT`, `U_EXX_FE_DIST`, `U_EXX_FE_BALO`

Crear estas tablas vacías con un script haría que el servicio arranque pero **genere XML
inválidos** (sin timbrado, sin códigos de geografía, sin actividad económica). El camino
correcto es instalar el addon y cargar sus maestros.

---

## Conclusión práctica

1. **Servidor nuevo, mismas empresas** → no hay que crear ningún objeto SAP. Solo verificar que
   las 2 tablas de `SAP_SIFEN` existan en ese HANA.
2. **Empresa nueva** → instalar/replicar el addon PY (grupo D), y luego crear las 2 UDT propias
   (grupo B) — eso sí se puede scriptear por Service Layer.
3. **Vale la pena implementar un bootstrap** en el servicio que, al arrancar, verifique y cree:
   - las tablas de `SAP_SIFEN` por ODBC (restaurar `EnsureTableExists`),
   - `@U_CERTIFICADOS` y `@U_CONFIG_EMAIL` por `UserTablesMD`/`UserFieldsMD`,
   - y que solo **valide y avise** (sin crear) los objetos `EPY_*`, fallando con un mensaje
     claro si faltan.
