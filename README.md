# BBVA Business Reconciliation — Integración

Integración con la API **Business Reconciliation v1.0** de BBVA Perú: un backend .NET 10 que
encapsula OAuth y la firma RSA, una consola Angular 22 para operarla, y las colecciones Bruno
y Postman del plan de pruebas oficial.

Estado actual, verificado contra el sandbox real:

| | |
|---|---|
| Tests unitarios backend | **89 verdes** |
| Plan de pruebas sandbox (API) | **41/41** — status y cuerpo documentados |
| Colección Bruno | **42 requests · 68 assertions** verdes |
| Colección Postman | **42 requests · 68 assertions** verdes |

---

## Por qué existe este backend

BBVA no pide solo un token. Pide **OAuth 2.0 _más_ una firma RSA-SHA512 sobre cada request**.
Eso tiene una consecuencia que define toda la arquitectura: **la clave privada no puede vivir en
un navegador**. Por eso el navegador habla con este backend, y este backend habla con BBVA.

```
Angular 22  ──HTTP──▶  .NET 10 API  ──HTTPS firmado──▶  BBVA API Market
(sin secretos)         (OAuth + RSA)                    sbx.pe.bbvaapimarket.com
```

El material original traía un WinForms de ejemplo. Se rescató su algoritmo de firma —lo único
valioso— y se descartó el resto, que tenía cuatro problemas reales:

| Problema en el ejemplo | Qué se hizo |
|---|---|
| El path param se concatenaba **después** de firmar → toda llamada a detalle iba firmada para otro destino | La firma se aplica en un `DelegatingHandler`, que ve el request ya armado. Ese bug no puede volver |
| No había llamada OAuth: el token se pegaba a mano | `BbvaOAuthTokenProvider` con cache y renovación anticipada |
| El PEM se releía y reimportaba en cada click | Se carga una vez al arrancar; una clave mal configurada falla en el arranque, no en producción |
| `.Result` bloqueando el UI thread, `HttpClient` nuevo por request | `HttpClient` tipado + `AddStandardResilienceHandler` |

---

## Estructura

```
BBVA INTEGRACION/
├─ backend/                    .NET 10 · arquitectura hexagonal
│  ├─ src/
│  │  ├─ …Domain/              value objects e invariantes — sin dependencias
│  │  ├─ …Application/         puertos y casos de uso
│  │  ├─ …Infrastructure/      firma, OAuth, cliente BBVA
│  │  └─ …Api/                 Minimal API + OpenAPI + Scalar
│  └─ tests/                   89 tests
├─ frontend/bbva-reconciliation-ui/   Angular 22 · Spartan UI · Tailwind 4
├─ api-clients/
│  ├─ bruno/                   colección de archivos .bru (42 requests)
│  └─ postman/                 collection v2.1 + environments
├─ fixtures/sandbox-test-plan.json    41 casos extraídos del workbook oficial
└─ 03.BS_GRUPO/ · *.pdf · *.xlsx      material fuente de BBVA — no se modifica
```

---

## Requisitos

| | Versión | Nota |
|---|---|---|
| .NET SDK | **10.0.400+** | ya instalado |
| Node.js | **22.22.3+ o 24.15+** | Angular 22 no arranca con Node 20 |

Este repo trae `frontend/bbva-reconciliation-ui/.node-version`. Con `fnm` o `nvm`:

```bash
fnm use          # o: nvm use
node --version   # v24.20.0
```

> El `ng` global de la máquina es 13.3.11 y no sirve. Usá siempre `npx ng` dentro del proyecto,
> que resuelve al CLI 22 local.

---

## Puesta en marcha

### 1. Credenciales

Nunca van en un archivo del repo. Backend, con `user-secrets`:

```bash
cd backend/src/Bbva.Reconciliation.Api
dotnet user-secrets set "Bbva:ClientId"        "<clientId>"
dotnet user-secrets set "Bbva:ClientSecret"    "<secretId>"
dotnet user-secrets set "Bbva:PrivateKeyPath"  "<ruta absoluta a clave_privada.pem>"
```

Alternativa sin archivo, útil en contenedores: `Bbva:PrivateKeyBase64` con el PEM en base64.
Acepta PKCS#8 (`BEGIN PRIVATE KEY`) y PKCS#1 (`BEGIN RSA PRIVATE KEY`) indistintamente — el
bundle de BBVA trae uno de cada uno.

### 2. Backend

```bash
cd backend
dotnet run --project src/Bbva.Reconciliation.Api
```

Abre **Scalar** en <http://localhost:5150/scalar/v1> con los 8 endpoints documentados.

Verificación rápida:

```bash
curl http://localhost:5150/api/v1/health
# {"status":"healthy","tokenAcquired":true,"tokenValidForSeconds":1736,...}
```

### 3. Frontend

```bash
cd frontend/bbva-reconciliation-ui
npx ng serve
```

<http://localhost:4200> · el backend debe estar corriendo.

---

## La aplicación

### Movimientos
Buscador con los filtros de la API, tabla, totales y detalle en panel lateral.

- **Validación espejo del backend en el cliente**, con los mensajes textuales de BBVA. Una cuenta
  de 17 dígitos falla en la tecla, sin request y sin gastar cuota.
- **Paginación por cursor**: el `paginationKey` de BBVA es opaco y unidireccional, así que la UI
  acumula páginas con "Cargar más". No hay forma de saltar a la página N, y fingir que la hay
  sería mentir sobre la API.
- **Sin resultados es 200, no un error.** BBVA responde `"No transactions to show."` con éxito;
  la UI muestra un empty state, no un banner rojo.
- Los 11 contratos del sandbox como chips, para no tipear 18 dígitos a mano.

### API Playground
El WinForms reencarnado. Elegís método, path y body; te muestra el `Digest`, el **`stringToSign`
renderizado línea por línea con los saltos LF visibles**, la `Signature`, y la respuesta cruda.
Copia como cURL.

Existe por una razón concreta: **BBVA responde a una firma mal armada con un 401 pelado, sin
ningún diagnóstico**. Poder comparar el `stringToSign` es la diferencia entre encontrar el
problema en dos minutos o en una tarde.

### Sandbox Runner
Los 41 casos del plan oficial, ejecutables uno por uno o todos, con diff lado a lado y export a
Markdown para evidencia de certificación.

Los importes, saldos y fechas **no** se comparan por defecto. El workbook los capturó en un
momento dado y el sandbox los sigue moviendo; compararlos pinta todo de rojo y tapa lo que sí
importa: códigos de estado, mensajes de error y forma de la respuesta. Hay un toggle para
activarlos.

---

## Endpoints

| | Ruta | |
|---|---|---|
| POST | `/api/v1/transactions/search` | Listado de movimientos |
| GET | `/api/v1/accounts/{accountId}/transactions/{transactionId}` | Detalle |
| GET | `/api/v1/health` | Estado + conectividad OAuth |
| POST | `/api/v1/diagnostics/signature-preview` | Calcula la firma sin enviar nada |
| POST | `/api/v1/playground/execute` | Envía un request arbitrario firmado |
| GET | `/api/v1/sandbox/cases` | El plan de pruebas |
| GET | `/api/v1/sandbox/cases/{caseId}` | Un caso |
| POST | `/api/v1/sandbox/cases/{caseId}/run` | Ejecuta y compara |

Los últimos cuatro **no se registran en producción**: son herramientas de integración, y exponer
un proxy de requests arbitrarios en producción sería un agujero de confused deputy.

Los errores salen como Problem Details RFC 9457 **conservando el envelope `messages` de BBVA**:

```json
{
  "type": "https://developer.bbva.com/errors/forbidden",
  "title": "Access denied",
  "status": 403,
  "detail": "Access denied",
  "messages": [{ "code": "forbidden", "message": "Access denied", "type": "ERROR" }]
}
```

Nada de lo que dijo el banco se pierde en la traducción.

---

## El algoritmo de firma

```
digest       = "SHA-512=" + base64(SHA512(bodyUtf8))       // GET → hashea ""
stringToSign = "(request-target):{método-minúscula} {path}\n"
               "host:{host}\n"
               "date:{RFC1123 UTC}\n"
               "digest:{digest}"
Signature    = algorithm="rsa-sha512",headers="(request-target) host date digest",
               signature="{base64(RSA-PKCS1v15-SHA512(stringToSign))}"
```

Tres detalles cargan todo el peso, y equivocarse en cualquiera devuelve el mismo 401 mudo:

1. El separador es **LF**, nunca CRLF. Un `Environment.NewLine` en Windows rompe todas las firmas.
2. El método va en **minúscula**.
3. El path firmado **excluye el query string**.

Está implementado una sola vez, en
[`RsaSha512RequestSigner`](backend/src/Bbva.Reconciliation.Infrastructure/Signing/RsaSha512RequestSigner.cs),
y fijado por un **golden vector**: el digest de una firma real capturada, verificado byte a byte.
Si ese test se rompe, la serialización del body cambió y todas las llamadas van a dar 401.

> **`expires`** — El WinForms agregaba `expires="{now+3600}"` al header; el script de Bruno
> original no. La firma real capturada tampoco lo lleva, así que el default es omitirlo.
> `Bbva:IncludeSignatureExpires` lo activa si BBVA alguna vez lo pide.

---

## Colecciones

### Bruno — `api-clients/bruno`

Firma nativamente con el `crypto` de Node. **No necesita el backend.**

```bash
export BBVA_CLIENT_ID=...
export BBVA_CLIENT_SECRET=...
export BBVA_PRIVATE_KEY_BASE64=$(base64 -w0 /ruta/clave_privada.pem)

cd api-clients/bruno
npx bru run -r --env sandbox --sandbox developer
```

`--sandbox developer` es obligatorio: el sandbox QuickJS por defecto de Bruno 4 no expone
`crypto`. Es lo mismo que el "Developer Mode" del manual de instalación de BBVA. Ya está fijado
en `bruno.json` para la GUI.

`BBVA_DEBUG_SIGNING=1` imprime el `stringToSign` de cada request.

### Postman — `api-clients/postman`

```bash
cd api-clients/postman
npx newman run BBVA-Business-Reconciliation.postman_collection.json \
  -e sandbox.postman_environment.json \
  --env-var "BBVA_CLIENT_ID=..." --env-var "BBVA_CLIENT_SECRET=..."
```

**Postman requiere el backend corriendo**, y esto es una limitación real de Postman, no una
decisión de diseño: la firma es RSA-PKCS1v15 sobre SHA-512, y el sandbox de Postman no puede
hacer una operación de clave privada RSA. No tiene `crypto` de Node, y las librerías que sí trae
son simétricas y de hashing. `node-forge` existe en la app de escritorio pero **no en newman**,
así que una colección que dependa de él anda a mano y falla en CI — lo peor de los dos mundos.

Por eso el pre-request script delega la firma en `/api/v1/diagnostics/signature-preview`. Los
requests siguen yendo de Postman directo a BBVA; solo se delega el cálculo de la firma. Como
efecto secundario deseable, Postman, Bruno y la app firman idéntico porque hay una sola
implementación.

Ninguna de las dos colecciones contiene un secreto. Ambas se generan del mismo fixture, así que
no pueden divergir.

---

## Verificación

```bash
# Backend: 89 tests
cd backend && dotnet build && dotnet test

# Frontend
cd frontend/bbva-reconciliation-ui && npx ng build

# Los 41 casos del plan contra el sandbox real
curl -X POST http://localhost:5150/api/v1/sandbox/cases/SRCH-03/run

# El digest debe coincidir carácter por carácter con la firma real capturada
curl -s -X POST http://localhost:5150/api/v1/diagnostics/signature-preview \
  -H "Content-Type: application/json" \
  -d '{"method":"POST","path":"/pe/business-reconciliation/v1/transactions/search",
       "body":"{\"contracts\":[\"001109100100500997\"]}"}' | jq -r .digest
# SHA-512=GSAgyW9sj/HVINRImvMlhJGht0tUciPszaKeSHQJCW2jkHS0PWrewCylBBxwcr7zvcw0RbjGL66btG0rXWTlBQ==
```

---

## ⚠️ Rotar credenciales

El bundle de documentación de BBVA trae, **en texto plano**:

- `credenciales.txt` — `clientId` y `secretId` (dos copias)
- `key.p8` y `clave_privada.pem` — la clave privada de firma (cuatro copias)
- `API-PAY-BR.json` — el `client_secret` y la clave privada completa en base64, dentro del
  environment exportado de Bruno

El `.gitignore` los excluye, y este proyecto no los referencia. **Pero excluirlos no es
remediarlos**: esos valores ya circularon por mail, zip y disco. Hay que rotarlos con BBVA.
La decisión es tuya, pero quedó dicho.

---

## Fuentes

| | |
|---|---|
| `03. PER Business reconciliation - Documentación técnica.pdf` | Contrato de la API, v1.0, dic 2024 |
| `07. …Plan de pruebas Sandbox…xlsx` | 41 casos con body y respuesta esperada |
| `03.BS_GRUPO/02.PROGRAMA_C#/` | WinForms de ejemplo — origen del algoritmo de firma |
| `03.BS_GRUPO/01.PROYECTO_BRUNO/API-PAY-BR.json` | Colección original y golden vector de firma |

El XLSX es la pieza más valiosa del material: no es documentación, es una suite de tests ya
escrita. Está extraída a `fixtures/sandbox-test-plan.json` y es la fuente única de los tests de
integración, el Sandbox Runner y las dos colecciones.
