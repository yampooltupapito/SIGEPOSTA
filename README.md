# SIGEPOSTA

**Sistema de gestión de la Posta Jesús Nazareno** — aplicación de escritorio (Windows Forms, C#, .NET Framework 4.7.2) con base de datos SQL Server.

Resuelve **dos servicios**:

1. **Pacientes con historia clínica:** registro de pacientes (con DNI cifrado), historia clínica automática (`HC-000001`…) y atenciones con diagnóstico cifrado.
2. **Inventario de medicamentos:** stock, ingresos y salidas con historial (kardex), y alertas de **stock bajo**, **por vencer** y **vencido**.

Ambos servicios están **conectados por la receta**: al registrar una atención el médico puede recetar un medicamento y el sistema descuenta el stock y registra la salida **en una sola transacción** (si el stock no alcanza, no se guarda nada).

La pantalla de inicio es un **dashboard** con tarjetas de indicadores, gráficos (atenciones por día del mes y de los últimos 7 días) y alertas de inventario, que cambia según el rol y se actualiza solo cada 60 segundos.

Incluye además: **inicio de sesión con roles** (Administrador, Médico, Farmacia), gestión de usuarios, **respaldo cifrado** de historias (`.sigebak`), **reportes** con exportación a CSV y medidas de seguridad (hash de contraseñas, cifrado, cierre de sesión por inactividad, manejo seguro de errores).

## Requisitos

| Requisito | Detalle |
|---|---|
| Sistema operativo | Windows 10 / 11 |
| .NET | **.NET Framework 4.7.2** (viene con Windows 10/11 actualizado) |
| Base de datos | **SQL Server** (desarrollado con SQL Server 2022, instancia por defecto en `localhost`) y SSMS para administrarla |
| Para compilar | Visual Studio (reciente, que abra archivos `.slnx`) con la carga de trabajo *Desarrollo de escritorio con .NET*; o MSBuild |
| Acceso a la base | Su usuario de Windows debe tener permiso sobre la base `SIGEPOSTA` (el programa usa **Autenticación de Windows**) |

## Estructura del proyecto

```
SIGEPOSTA.slnx
README.md
docs/                     ← documentación (UML, guía de sustentación, manual, pruebas, esquema SQL)
SIGEPOSTA/                ← proyecto de Visual Studio (SIGEPOSTA.csproj)
├── Presentacion/         ← ventanas (Frm*.cs), Estilo.cs, MonitorInactividad, ManejadorErrores
├── Negocio/              ← entidades, reglas, servicios y permisos
├── Datos/                ← acceso a SQL Server (ADO.NET, consultas parametrizadas)
├── Seguridad/            ← hash, cifrado AES, HMAC, llaves (DPAPI), sesión, archivo de respaldo
├── App.config            ← cadena de conexión
└── Program.cs            ← punto de entrada
```

## 1. Preparar la base de datos

1. En SQL Server cree una base de datos llamada **`SIGEPOSTA`**.
2. Cree en ella las tablas que el programa usa: **`Rol`, `Usuario`, `Posta`, `Paciente`, `HistoriaClinica`, `Atencion`, `Medicamento`, `MovimientoInventario`.**
   - Un script de **referencia** con las columnas, tipos y restricciones que el código espera está en [`docs/Esquema_BD_referencia.sql`](docs/Esquema_BD_referencia.sql). Si usted ya creó las tablas a mano, úselo solo para comparar.
3. Cargue los datos iniciales:
   - **`Rol`**: `1 = Administrador`, `2 = Medico`, `3 = Farmacia` (los números deben coincidir con el programa).
   - **`Posta`**: exactamente **una** fila, la del establecimiento (**Posta Jesús Nazareno**). El programa lee su `IdPosta` una sola vez al iniciar y lo asigna automáticamente a todo paciente y medicamento nuevo. La tabla y la columna `IdPosta` se conservan para poder crecer a una red de postas. Si la tabla está vacía, el programa avisa y no inicia.
4. **No** inserte usuarios: el primer administrador se crea desde el programa (ver "Primer uso").

La tabla **`Atencion`** debe tener además las columnas de la receta: `IdMedicamento INT NULL` (FK a `Medicamento`) y `CantidadRecetada INT NULL`, con la restricción *ambas NULL, o medicamento y cantidad > 0*.

Restricciones importantes que el programa da por hechas: `Paciente.DniHash` único; `HistoriaClinica.NumeroHistoria` e `IdPaciente` únicos; `Usuario.NombreUsuario` único; `Medicamento.StockActual >= 0`; `MovimientoInventario.Cantidad > 0` y `Tipo` = `'Ingreso'` o `'Salida'`.

## 2. Configurar la cadena de conexión

Se encuentra en `SIGEPOSTA/App.config` (al compilar se copia como `SIGEPOSTA.exe.config`):

```xml
<connectionStrings>
  <add name="SIGEPOSTA"
       connectionString="Server=localhost;Database=SIGEPOSTA;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
       providerName="System.Data.SqlClient" />
</connectionStrings>
```

- Cambie **`Server=`** si su SQL Server está en otra máquina o es una instancia con nombre (ej.: `Server=localhost\SQLEXPRESS`).
- Cambie **`Database=`** si su base se llama distinto.
- **No hay usuario ni contraseña** en la cadena: se usa la cuenta de Windows (`Integrated Security=True`). Nunca escriba contraseñas en este archivo.
- `TrustServerCertificate=True` es cómodo para un servidor local de pruebas; en un servidor real conviene usar un certificado válido.

## 3. Compilar y ejecutar

**Con Visual Studio:** abra `SIGEPOSTA.slnx`, elija **Debug** o **Release** y pulse **F5** (o *Ctrl + F5*).

**Con la línea de comandos (MSBuild):**

```powershell
& "C:\Program Files\Microsoft Visual Studio\<versión>\<edición>\MSBuild\Current\Bin\MSBuild.exe" `
    SIGEPOSTA\SIGEPOSTA.csproj -restore:false -p:Configuration=Release
.\SIGEPOSTA\bin\Release\SIGEPOSTA.exe
```

El proyecto no usa paquetes NuGet ni librerías externas (solo el .NET Framework; los gráficos usan `System.Windows.Forms.DataVisualization`, que ya viene con él).

## 4. Primer uso

1. Al abrir por **primera vez** (sin usuarios) aparece **"Administrador inicial"**: elija usuario, nombre y una contraseña (mínimo 8 caracteres, con mayúscula, minúscula y número).
2. Inicie sesión con ese administrador.
3. En **Usuarios** cree las cuentas de **Médico** y **Farmacia**.

Más detalle de uso en [`docs/Manual_Usuario.md`](docs/Manual_Usuario.md).

## Un solo establecimiento

SIGEPOSTA se diseñó para **un establecimiento**: la **Posta Jesús Nazareno**. Por eso las pantallas ya no piden elegir posta, los reportes filtran por **rango de fechas** y el respaldo cubre **todas las historias del establecimiento**. La estructura de la base de datos (tabla `Posta`, columna `IdPosta`) queda lista para crecer a una **red de postas** sin rediseñarla.

## Roles

| Rol | Pacientes | Inventario | Reportes | Respaldo | Usuarios |
|---|:---:|:---:|:---:|:---:|:---:|
| Administrador | ✔ | ✔ | todos | ✔ | ✔ |
| Médico | ✔ | — | pacientes | ✔ | — |
| Farmacia | — | ✔ | inventario | — | — |

Los permisos se validan en la **capa de Negocio** (no solo ocultando botones).

## Seguridad (resumen)

- Contraseñas: **PBKDF2-SHA256**, salt aleatorio de 16 bytes, 100 000 iteraciones; bloqueo de 5 minutos tras 5 intentos fallidos.
- DNI y diagnóstico: **AES-256** con IV aleatorio por dato; búsqueda y duplicados por **HMAC-SHA256** del DNI.
- La llave de cifrado **no está en el código**: se genera al azar y se guarda protegida con **DPAPI** (almacenamiento aislado del usuario de Windows).
- Solo consultas **parametrizadas** (sin concatenar texto en el SQL).
- Cierre de sesión tras **10 minutos** de inactividad; los errores inesperados muestran un mensaje amable con un código (el detalle técnico queda en `%LocalAppData%\SIGEPOSTA\errores.log`).
- **Respaldo (`.sigebak`)**: XML → GZip → AES + firma HMAC. Solo se puede abrir con el **mismo usuario de Windows en el mismo equipo**.

> ⚠️ **Respalde la llave y la base de datos.** Si se pierde el almacén de llaves del usuario de Windows (o se cambia de equipo sin migrarlo), los DNI y diagnósticos cifrados **no se pueden recuperar**.

## Documentación

| Documento | Contenido |
|---|---|
| [`docs/UML_Clases.md`](docs/UML_Clases.md) / [`.puml`](docs/UML_Clases.puml) | Diagrama de clases (Mermaid y PlantUML) |
| [`docs/Guia_Sustentacion.md`](docs/Guia_Sustentacion.md) | Guion de sustentación, relación con el sílabo, decisiones de diseño y preguntas |
| [`docs/Manual_Usuario.md`](docs/Manual_Usuario.md) | Manual breve de usuario |
| [`docs/Pruebas.md`](docs/Pruebas.md) | Tabla de pruebas manuales |
| [`docs/Esquema_BD_referencia.sql`](docs/Esquema_BD_referencia.sql) | Esquema de referencia de la base de datos |

## Notas

- `SIGEPOSTA/Form1.cs` es la plantilla original de Visual Studio; no se usa (el programa inicia en `Program.cs` → inicio de sesión). Puede quitarse del proyecto sin afectar nada.
- El proyecto usa solo **C# 7.3** y **.NET Framework 4.7.2**.
