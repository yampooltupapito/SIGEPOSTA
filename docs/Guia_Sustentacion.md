# Guía de sustentación oral — SIGEPOSTA

Sustentación **individual de 10 a 15 minutos**. Esta guía está escrita para que usted la use como apoyo: lo que aparece como *"Decir"* está en lenguaje simple y puede adaptarlo con sus propias palabras.
Todas las rutas son relativas a la carpeta del proyecto `SIGEPOSTA/` (donde está `SIGEPOSTA.csproj`).

**Alcance del proyecto (dígalo al inicio):** el sistema implementa **2 servicios** —*Pacientes con historia clínica* e *Inventario de medicamentos*— más **inicio de sesión con roles** (Administrador, Médico, Farmacia), y además usuarios, respaldo, reportes y seguridad final. El sistema se diseñó para **un solo establecimiento —la Posta Jesús Nazareno—**, con la **estructura lista para crecer a una red de postas** (se conservan la tabla `Posta` y la columna `IdPosta`). Al entrar, la **pantalla de inicio es un panel (dashboard)** con indicadores, gráficos y alertas según el rol. Los dos servicios están **conectados por la receta**: al registrar una atención el médico puede recetar un medicamento y el stock se descuenta automáticamente.

---

## 0. Antes de empezar (preparación, 10 minutos antes)

- [ ] SQL Server en ejecución y la base `SIGEPOSTA` con sus tablas (ver `README.md`).
- [ ] Tres usuarios creados (uno por rol): `admin`, `medico1`, `farmacia1`. Anote sus contraseñas aparte (no las escriba en pantalla).
- [ ] La tabla `Posta` con **una** fila (Posta Jesús Nazareno), 2–3 pacientes con alguna atención y 3–4 medicamentos: uno con **stock bajo**, uno **por vencer** (≤ 30 días) y uno **vencido**, para que se vean los colores.
- [ ] Para la demostración de la **receta**: un medicamento vigente y con stock (los vencidos o sin stock no aparecen en la lista).
- [ ] Para mostrar la **paginación y los tiempos**: datos de prueba abundantes (idealmente varios cientos de pacientes y de medicamentos) y las consultas de `sys.indexes` y de plan de ejecución de la sección 3 abiertas en SSMS.
- [ ] Un respaldo `.sigebak` ya hecho y **una copia dañada** (abra la copia en un editor y cambie o borre unos bytes).
- [ ] SSMS abierto con las consultas de `docs/Pruebas.md` (caso "DNI y diagnóstico ilegibles").
- [ ] Visual Studio con la solución abierta en `Negocio/Persona.cs`, y el diagrama `docs/UML_Clases.md` a mano.
- [ ] **Importante:** el respaldo solo se abre con **el mismo usuario de Windows en el mismo equipo** (la llave está protegida con DPAPI). Haga toda la demostración en su computadora.

**Plan B si algo falla:** si SQL Server no responde, muestre el código y las capturas del manual; el programa mostrará un mensaje amable de conexión (no se rompe).

---

## 1. Guion por minutos (≈ 14 minutos)

| Min | Qué mostrar | Qué decir |
|---|---|---|
| **0:00 – 1:00** | Pantalla de **login** (`FrmLogin`). | "SIGEPOSTA es un sistema de escritorio para la Posta Jesús Nazareno: un solo establecimiento, pero con la estructura lista para crecer a una red de postas. Resuelve dos servicios: pacientes con historia clínica e inventario de medicamentos, con login por roles. Está hecho en C#, Windows Forms, .NET Framework 4.7.2 y SQL Server." |
| **1:00 – 2:30** | Explorador de soluciones (4 carpetas) y el diagrama `docs/UML_Clases.md`. | "Organicé el código en **cuatro capas**: Presentación (ventanas), Negocio (reglas), Datos (acceso a SQL Server con ADO.NET) y Seguridad (cifrado, hash, sesión). La ventana nunca habla con la base de datos: pasa por un **servicio** de Negocio. Así cada capa se puede entender y cambiar por separado." |
| **2:30 – 4:00** | Código `Negocio/Persona.cs` y `Negocio/Paciente.cs`. **(U1)** | "`Persona` es **abstracta**: no existe una persona genérica, siempre es un paciente (o, a futuro, personal). `Paciente` **hereda** de ella. `MostrarDatos()` es **virtual** en `Persona` y la **sobrescribo** en `Paciente`: eso es **polimorfismo**. Los datos tienen **encapsulamiento**: las propiedades validan en el `set` (DNI de 8 dígitos, fecha no futura) usando `Validador.cs`." |
| **4:00 – 5:30** | Iniciar sesión como `admin`: se abre el **panel de inicio** (tarjetas, gráficos, alertas). Luego entrar como `medico1` y `farmacia1` y comparar qué ve cada uno. Menú por rol. Intentar con clave mala (mensaje genérico). **(U4)** | "Al entrar aparece el **dashboard**; carga en segundo plano con un 'Cargando…' (BackgroundWorker) y se actualiza solo cada 60 segundos. El Administrador ve todo; el Médico ve pacientes, atenciones y gráficos; Farmacia ve solo inventario. Las consultas del dashboard hacen el conteo en SQL (`COUNT`, `GROUP BY`) y solo se ejecutan si el rol tiene permiso. El menú cambia según el rol: Médico no ve Inventario y Farmacia no ve Pacientes. Pero **no me fío solo de ocultar botones**: cada servicio vuelve a validar el rol (`Negocio/Permisos.cs`). El mensaje de error del login es siempre el mismo —'Usuario o contraseña incorrectos'— para no revelar qué usuarios existen, y tras 5 intentos fallidos se bloquea 5 minutos." |
| **5:30 – 7:30** | **Pacientes** (`FrmPacientes`): escribir en el buscador (esperar 300 ms), cambiar de página y señalar la barra de estado *«Consulta en X ms»*; registrar uno; probar DNI de 5 dígitos y DNI duplicado; abrir su historia y agregar una atención (la receta se muestra en el minuto 9). **(U6)** | "Al guardar, se crean el **paciente y su historia clínica en una sola transacción**: o se guardan los dos o ninguno. El número de historia (HC-000001) se genera dentro de la misma transacción con un bloqueo, para que dos personas a la vez no reciban el mismo número." |
| **7:30 – 8:30** | SSMS: `SELECT` de `Paciente` y `Atencion` (bytes ilegibles). **(U4)** | "El DNI y el diagnóstico están **cifrados con AES**: en la base solo se ven bytes. Para poder **buscar** por DNI sin descifrar, guardo también un **HMAC** del DNI. Y la contraseña nunca se guarda: solo un hash **PBKDF2** con salt." |
| **8:30 – 9:00** | Código `Seguridad/GestorLlaves.cs` (solo mostrar). **(U4)** | "La llave de cifrado **no está escrita en el código**: se genera al azar la primera vez y se guarda protegida con **DPAPI** de Windows en el almacenamiento aislado del usuario." |
| **9:00 – 11:00** | **Inventario** (`FrmInventario`): colores de alerta; filtro 'Solo críticos'; registrar un ingreso y una **salida mayor al stock**. Luego, en la **historia clínica** de un paciente, una atención **con receta** y, en *Ver movimientos*, la salida "Receta HC-…" que se generó sola. **(U2, U6)** | "Las alertas —stock bajo, por vencer en 30 días o menos, vencido— se calculan con **LINQ** sobre una `List<Medicamento>` (`InventarioServicio.Criticos`). Cada movimiento actualiza el stock y guarda el registro **en una transacción**. La salida usa un `UPDATE` con condición `StockActual >= @cant`: si no alcanza, no cambia nada y se avisa 'Stock insuficiente'. **La receta conecta los dos servicios:** guardar la atención, descontar el stock y registrar el movimiento ocurren en **una sola transacción**; si el stock no alcanza, no se guarda ni la atención. El stock **nunca** queda negativo." |
| **11:00 – 12:00** | **Respaldo** (`FrmRespaldo`): respaldar todas las historias del establecimiento (barra de progreso), abrir el `.sigebak` en el Bloc de notas, abrir el respaldo bueno y el dañado. **(U3, U5)** | "El respaldo se **serializa a XML**, se **comprime con GZip** y se **cifra con AES**; el archivo termina en `.sigebak`. Se ejecuta en un **BackgroundWorker** con barra de progreso, así la ventana no se congela y se puede cancelar. Al abrirlo, primero se verifica una firma **HMAC**: por eso un archivo dañado o de otra llave se rechaza con un mensaje claro." |
| **12:00 – 13:00** | **Reportes** y exportar a CSV; mencionar cierre por inactividad y manejador global de errores. | "Los reportes agrupan con LINQ (`GroupBy`, `Distinct`, `Sum`) y cada uno solo lo ve quien tiene permiso. Para seguridad final, la sesión se **cierra sola a los 10 minutos sin actividad** y cualquier error inesperado muestra un mensaje amable con un código, nunca el texto técnico." |
| **13:00 – 14:00** | Diapositiva o `docs/Guia_Sustentacion.md` → sección "Limitaciones y mejoras". | "Quedaron fuera de los dos servicios la conexión receta→stock y las citas/cola de atención; y la 'restauración' solo verifica y muestra el respaldo, no reescribe la base. Como mejoras: …" (ver sección 5). |
| **14:00 – 15:00** | Preguntas. | Use la sección 4 como práctica. |

> **Consejo de tiempo:** si va largo, recorte los minutos 12:00–13:00 (reportes) a una frase. Si va corto, muestre el código de `RegistrarSalida` o de `ArchivoRespaldoSeguro.Guardar`.

---

## 2. Relación con las unidades del sílabo (dónde se aplica cada una)

| Unidad | Tema | Archivo y clase exactos | Qué se aplica ahí |
|---|---|---|---|
| **U1 — POO** | Clase abstracta, herencia, polimorfismo | `Negocio/Persona.cs` → `Persona` (abstract); `Negocio/Paciente.cs` → `Paciente : Persona`, `MostrarDatos()` | `Persona` abstracta; `MostrarDatos()` *virtual* en `Persona` y *override* en `Paciente`. |
| U1 | Encapsulamiento y validación | `Negocio/Persona.cs`, `Medicamento.cs`, `Usuario.cs` + `Negocio/Validador.cs` | Campos privados y propiedades que validan en el `set` (DNI 8 dígitos, stock no negativo, campos obligatorios). |
| U1 | Constructores sobrecargados | `Paciente`, `Medicamento`, `Usuario`, `Atencion`, `HistoriaClinica`, `MovimientoInventario` | 2 o 3 constructores por clase (vacío para la BD, completo para formularios). |
| U1 | Composición | `Negocio/HistoriaClinica.cs` → `Atenciones` (`IReadOnlyList<Atencion>`) y `AgregarAtencion` | La historia *contiene* sus atenciones. |
| U1 | Excepciones propias, interfaces, eventos | `Negocio/ValidacionException.cs`, `Negocio/Excepciones.cs`; `Presentacion/MonitorInactividad.cs` (`IMessageFilter`, `IDisposable`, evento `InactividadAgotada`) | Excepciones por tipo de error; evento que avisa la inactividad. |
| **U2 — Colecciones** | `List<T>`, `Dictionary`, LINQ | `Negocio/PacienteServicio.cs` → `BuscarEnMemoria`, `FiltrarPorPosta`, `OrdenarPorApellido`; `Negocio/InventarioServicio.cs` → `StockBajo`, `PorVencer`, `Vencidos`, `Criticos`; `Negocio/ReporteServicio.cs` → `PacientesAtendidosPorPosta` (`GroupBy`, `Distinct`), `MovimientosPorFecha` (`Sum`); `Negocio/PacienteServicio.cs` → `ListarMedicamentosRecetables` (`Where` por posta, vencimiento y stock; `OrderBy`) | Filtrar, ordenar, buscar y agrupar en memoria con LINQ. `Dictionary<string,string>` en `PacienteServicio.Validar` y `InventarioServicio.Validar` (campo → mensaje de error). |
| **U3 — Serialización y compresión** | XML, GZip, archivos | `Negocio/RespaldoModelos.cs` (`RespaldoArchivo`, `PacienteRespaldo`, `AtencionRespaldo`); `Seguridad/ArchivoRespaldoSeguro.cs` → `Guardar<T>`/`Leer<T>`; `Negocio/RespaldoServicio.cs` | `XmlSerializer` → `GZipStream` → cifrado → archivo `.sigebak`. |
| U3 | Archivos de texto | `Negocio/ReporteTabla.cs` → `GuardarCsv` | Exporta reportes a CSV (UTF-8) y evita fórmulas maliciosas en Excel. |
| **U4 — Seguridad** | Hash de contraseñas | `Seguridad/HashContrasena.cs` | PBKDF2 (`Rfc2898DeriveBytes`, SHA-256), salt de 16 bytes, hash de 32 bytes, 100 000 iteraciones, comparación en tiempo constante. |
| U4 | Cifrado simétrico | `Seguridad/CifradoAes.cs` (`CifrarABytes`, `DescifrarDeBytes`) | AES-256 con `CryptoStream` e IV aleatorio por dato (DNI y diagnóstico). |
| U4 | Integridad / búsqueda | `Seguridad/HmacDni.cs` → `Calcular`; `Seguridad/ArchivoRespaldoSeguro.cs` | HMAC-SHA256 del DNI (64 hex) y firma del respaldo. |
| U4 | Gestión de llaves | `Seguridad/GestorLlaves.cs` | DPAPI (`ProtectedData`) + almacenamiento aislado: la llave no está en el código. |
| U4 | Autenticación y autorización | `Negocio/AutenticacionServicio.cs`, `Seguridad/Sesion.cs`, `Negocio/Permisos.cs` | Login, bloqueo tras 5 intentos, mensaje genérico, roles validados en Negocio. |
| U4 | Seguridad de la aplicación | `Presentacion/ManejadorErrores.cs`, `Presentacion/MonitorInactividad.cs` | Errores sin texto técnico; cierre de sesión a los 10 minutos. |
| **U5 — Procesos asíncronos** | `BackgroundWorker` | `Presentacion/FrmRespaldo.cs` → `_worker`, `Worker_DoWork`, `Worker_ProgressChanged`, `Worker_Completed`, `CancelAsync` | Respaldo/verificación en otro hilo con barra de progreso y cancelación. |
| U5 | Temporizador y hilos de la interfaz | `Presentacion/MonitorInactividad.cs` (`Timer`, `Stopwatch`, `Pausar`/`Reanudar`); `Presentacion/ManejadorErrores.cs` (`ThreadException`, `UnhandledException`) | Vigilancia de inactividad y captura de errores de cualquier hilo. |
| **U6 — ADO.NET y transacciones** | Conexión, comandos, lectores | `Datos/Conexion.cs`; `Datos/PacienteDatos.cs`, `HistoriaDatos.cs`, `MedicamentoDatos.cs`, `UsuarioDatos.cs`, `ReporteDatos.cs` | `SqlConnection`, `SqlCommand`, `SqlDataReader`, `SqlParameter` — **solo consultas parametrizadas**. Cadena en `App.config`. |
| U6 | Transacción paciente + historia | `Negocio/PacienteServicio.cs` → `Registrar` (`BeginTransaction`, `Commit`, `Rollback`); `Datos/HistoriaDatos.cs` → `ObtenerSiguienteNumero` | Un paciente nunca queda sin historia; número HC-000001 sin repetirse. |
| U6 | Transacción stock + movimiento | `Negocio/InventarioServicio.cs` → `RegistrarIngreso`, `RegistrarSalida`, `Registrar`, `EjecutarEnTransaccion`; `Datos/MedicamentoDatos.cs` → `DescontarStock`, `IncrementarStock`, `InsertarMovimiento` | `UPDATE … WHERE StockActual >= @cant` y revisión de filas afectadas. |
| U1 | **Establecimiento único (estructura lista para una red)** | `Negocio/Establecimiento.cs` (`Nombre`, `IdPosta`, `Inicializar`), `Datos/EstablecimientoDatos.cs`, `Negocio/Posta.cs`; uso en `PacienteServicio.Registrar` e `InventarioServicio.Registrar` | Clase estática de configuración: el `IdPosta` se lee de la tabla `Posta` una sola vez (con `lock`) y se asigna a todo lo nuevo. |
| U5 | **Búsqueda mientras se escribe** | `Presentacion/ControladorBusqueda.cs` (`Timer` de 300 ms + `BackgroundWorker`, cancela la búsqueda anterior), usado por `FrmPacientes` y `FrmInventario` | Solo se consulta tras una pausa de 300 ms; la consulta corre en otro hilo; si el usuario sigue escribiendo, la anterior se cancela y su resultado se descarta. |
| U6 | **Paginación y consultas eficientes** | `Datos/PacienteDatos.cs` y `Datos/MedicamentoDatos.cs` (`Contar`, `ListarPagina`: `COUNT` aparte y `OFFSET … FETCH`), `Datos/PatronBusqueda.cs`, `Datos/Conexion.cs` (`Comando` con `SET NOCOUNT ON`), `Negocio/ResultadoPagina.cs`, `PacienteServicio.BuscarPagina`, `InventarioServicio.BuscarPagina` | Páginas de 50 filas, solo las columnas necesarias, parámetros con tipo y tamaño explícitos, sin `AddWithValue`; el servicio mide cada consulta con `Stopwatch` y la pantalla la muestra en la barra de estado. |
| U6 | **Receta: transacción que conecta Pacientes e Inventario** | `Negocio/PacienteServicio.cs` → `RegistrarAtencion(…, idMedicamento, cantidad)`, `ValidarReceta`, `ListarMedicamentosRecetables`, `EjecutarEnTransaccion`; `Datos/HistoriaDatos.cs` → `InsertarAtencion(a, cn, tx)`, `ObtenerNumeroYPosta`; `Datos/MedicamentoDatos.cs` → `ObtenerPorIdParaReceta` (fila bloqueada con `UPDLOCK`), `DescontarStock`, `InsertarMovimiento`; `Presentacion/FrmHistoriaClinica.cs` | Una sola `SqlTransaction`: (a) `INSERT` de la atención con `IdMedicamento`/`CantidadRecetada`, (b) `UPDATE … StockActual >= @cant` con revisión de filas, (c) movimiento *Salida* "Receta HC-xxxxxx". Rol Administrador o Médico validado en el servicio (`Permisos.ExigirRecetar`). |
| U6 | Regla protegida en la BD | `Datos/UsuarioDatos.cs` → `CambiarRol`, `CambiarActivo` | La condición "no dejar el sistema sin administrador" va dentro del `UPDATE`. |
| U2 | Dashboard: colecciones y LINQ | `Negocio/DashboardServicio.cs` → `CargarBloquePacientes` (`OrderByDescending`, `Take`, `Enumerable.Range` + `Select` para completar los 7 días con 0), `CargarBloqueInventario` (`OrderByDescending`/`ThenBy`); `Negocio/DatosDashboard.cs` (`List<PuntoDato>`, `List<Medicamento>`) | Los resultados resumidos que devuelve SQL se ordenan, recortan y completan en memoria con LINQ. |
| U4 | Permisos por bloque en el dashboard | `Negocio/DashboardServicio.cs` → `Cargar`, `CargarBloquePacientes`, `CargarBloqueInventario`; `Negocio/Permisos.cs` → `ExigirAccesoDashboard` | Cada bloque valida el rol en el servicio: un rol sin permiso no ejecuta esas consultas (aunque se llame al método directamente). |
| U5 | **Dashboard asíncrono** | `Presentacion/PanelDashboard.cs` → `_worker` (`BackgroundWorker`), `Actualizar`, `Worker_Completed`, `_reloj` (`Timer` de 60 s) | La ventana no se congela: muestra "Cargando…", carga en otro hilo y se actualiza sola cada 60 s o con el botón *Actualizar*. |
| U6 | **Dashboard: agregación en SQL** | `Datos/DashboardDatos.cs` → `ObtenerResumenPacientes` (3 `COUNT`), `ObtenerAtencionesPorDia` (`GROUP BY`; una sola consulta alimenta el gráfico del mes y el de 7 días), `ObtenerResumenInventario` (`SUM(CASE …)`), `ObtenerMedicamentosCriticos` (`TOP (@n)`) | Solo consultas de lectura y parametrizadas; a la aplicación llegan pocas filas ya resumidas, no miles para contarlas en memoria. |

---

## 3. Decisiones de diseño explicadas en lenguaje simple

**¿Por qué `Persona` es abstracta?** — Nunca se registra "una persona" sin más: siempre es un paciente (y mañana podría ser un médico). Al ser abstracta, nadie puede crear una `Persona` suelta por error, y los datos comunes (DNI, nombres, edad) y sus validaciones viven en un solo lugar.

**Herencia y polimorfismo (`MostrarDatos`).** — `Paciente` *es una* `Persona`, así que reutiliza todo lo común y solo agrega lo propio (posta, dirección, historia). `MostrarDatos()` existe en `Persona`; `Paciente` la sobrescribe para agregar la dirección. Quien llama `MostrarDatos()` sobre una `Persona` obtiene el comportamiento correcto según el tipo real: eso es polimorfismo. (De paso, el DNI se muestra enmascarado: `****` + últimos 4.)

**Encapsulamiento y validación en el `set`.** — Los campos son privados; lo único que se puede hacer es asignar por la propiedad, y la propiedad **rechaza datos inválidos** (por ejemplo un DNI que no tiene 8 dígitos o un stock negativo) lanzando una `ValidacionException`. Así es imposible construir un objeto en un estado inválido, venga de un formulario o de la base de datos.

**¿Por qué AES y no DES?** — DES usa una llave de solo 56 bits y hoy se puede romper con fuerza bruta; está obsoleto. **AES** es el estándar vigente. El sílabo menciona DES como ejemplo clásico, pero para datos de salud elegí AES-256, que es lo que se usa actualmente. Además cada dato lleva un **IV aleatorio**: el mismo DNI cifrado dos veces da resultados distintos.

**¿Por qué PBKDF2 con salt y no SHA1?** — SHA1 es un hash **rápido** (y ya tiene colisiones demostradas): justo lo que no se quiere para contraseñas, porque un atacante prueba millones por segundo. **PBKDF2** repite el cálculo 100 000 veces para volverlo **lento a propósito**, y el **salt** (16 bytes al azar por usuario) hace que dos usuarios con la misma contraseña tengan hashes distintos y evita las tablas precalculadas. El sílabo menciona SHA1; elegí una alternativa vigente.

**¿Para qué sirve el HMAC del DNI?** — El DNI se guarda cifrado con AES y, como el cifrado cambia cada vez (IV aleatorio), **no se puede buscar** con un `WHERE`. El HMAC-SHA256 del DNI es siempre igual para el mismo DNI, así que sirve de "índice": busco por el HMAC (columna `DniHash`, única) sin descifrar nada y evito duplicados. Se usa HMAC con una llave secreta (no un hash simple) porque un DNI tiene solo 100 millones de combinaciones: un hash sin llave se adivinaría en segundos.

**¿Por qué la transacción de paciente + historia?** — Son dos inserciones que deben ir juntas. Si se guardara el paciente y fallara la historia, quedaría un paciente "huérfano". Con `SqlTransaction`, si algo falla se hace `Rollback` y no queda nada a medias. El número de historia se calcula dentro de la misma transacción con bloqueo (`UPDLOCK, HOLDLOCK`) para que no se repita.

**¿Por qué la transacción de stock + movimiento?** — El stock es un número y el movimiento es su "comprobante" (kardex). Si uno se guarda sin el otro, el inventario deja de cuadrar. Además la salida usa `UPDATE … SET StockActual = StockActual - @cant WHERE … AND StockActual >= @cant` y mira cuántas filas cambiaron: si fue 0, no alcanzaba y se hace `Rollback`. Esa comprobación es atómica, así que ni dos usuarios a la vez pueden dejar el stock negativo (y la tabla además tiene un `CHECK StockActual >= 0`).

**¿Por qué la receta es una transacción atómica (y qué pasaría si el inventario quedara inconsistente)?** — Al guardar una atención con receta ocurren **tres cosas que dependen entre sí**: (1) se guarda la atención con el medicamento y la cantidad, (2) se descuenta el stock y (3) se registra el movimiento de salida con el motivo "Receta HC-xxxxxx". Si se hicieran por separado y una fallara, el inventario mentiría:
- *Atención guardada pero stock sin descontar:* el paciente "recibió" un medicamento que sigue figurando en el estante → se podría recetar de nuevo lo que ya no hay, o se entregaría a otro paciente.
- *Stock descontado pero atención no guardada:* desaparece medicamento sin que ninguna historia lo explique → faltante "fantasma".
- *Stock descontado sin su movimiento:* el kardex ya no cuadra con el stock y no se puede auditar quién se llevó qué.

Con una sola `SqlTransaction` el resultado es **todo o nada**: si el stock no alcanza (o falla la luz, la red o cualquier paso), se hace `Rollback` y **no queda nada**: ni atención, ni descuento, ni movimiento. Además el medicamento se lee dentro de la transacción con la fila bloqueada (`UPDLOCK`) y el descuento usa `UPDATE … WHERE StockActual >= @cant`, de modo que **dos médicos recetando el último frasco a la vez** no pueden dejar el stock negativo: uno gana y el otro recibe "Stock insuficiente para recetar". Las reglas (medicamento activo, no vencido, de la posta del paciente, cantidad mayor que cero, rol Administrador o Médico) se validan en el servicio, no solo en la ventana; el Médico puede recetar aunque no tenga acceso a la pantalla de Inventario.

**¿Por qué el dashboard cuenta y agrupa en SQL y no en memoria?** — Para saber "cuántas atenciones hubo este mes" o "cuántas por día", lo eficiente es que **SQL Server** haga el `COUNT` y el `GROUP BY` y devuelva unas pocas filas. Traer miles de atenciones a la aplicación solo para contarlas gastaría red y memoria y haría lento el panel. En memoria (con LINQ) solo hago lo que es barato y natural: ordenar, quedarme con las 8 postas más altas y **rellenar con 0 los días sin atenciones** para que el gráfico de 7 días siempre tenga 7 puntos.

**¿Cómo se controlan los permisos en el dashboard?** — La pantalla arma solo los bloques que el rol puede ver, pero la **decisión real está en `DashboardServicio`**: el bloque de pacientes y atenciones exige Administrador o Médico, y el de inventario exige Administrador o Farmacia. Si un Farmacia pidiera el bloque de pacientes, el servicio lanza `AccesoDenegadoException` **antes** de ejecutar ninguna consulta. Además, cada bloque se carga por separado: si falla uno (por ejemplo, la tabla de inventario), el otro igual se muestra y el usuario ve un mensaje amable, nunca el texto técnico.

**¿Por qué el sistema es para un solo establecimiento, pero la estructura queda lista para una red?** — El sistema lo usa una sola posta (la **Posta Jesús Nazareno**), así que pedirle al usuario "elija la posta" en cada formulario solo estorba y permite errores (registrar un paciente en otra posta por equivocación). Por eso `Establecimiento` lee **una sola vez** el `IdPosta` de la tabla `Posta` y los servicios lo asignan solos a todo paciente y medicamento nuevo; el nombre sale en el login, la ventana principal y el dashboard. Pero **no se borró** la tabla `Posta` ni la columna `IdPosta`: si mañana se suman más postas, la base de datos ya está preparada (bastaría volver a ofrecer la elección de posta en las pantallas y reponer los reportes por posta), sin rediseñar nada. Es el principio de *no cerrar puertas* sin complicar lo que se usa hoy. Al editar un registro, su posta **no se modifica**.

### Rendimiento: índices, paginación y tiempos (etapa 10)

**¿Qué es un índice y por qué ayuda?** — Es como el **índice alfabético de un libro**: en vez de leer todas las páginas (*table scan*) para encontrar "Quispe", SQL Server va directo a la zona de la "Q" (*index seek*). Con pocas filas no se nota; con cientos de miles, pasa de segundos a milisegundos. Tienen un costo: ocupan espacio y hacen un poco más lentas las altas y modificaciones (hay que mantener el índice), por eso solo se crean donde las consultas los usan.

**Índices que respaldan las consultas del programa.** *(Los índices ya están creados en la base; esta tabla explica para qué consulta sirve cada tipo de índice. Verifique los nombres y columnas reales con la consulta de abajo y ajuste la tabla a lo que usted creó.)*

| Tabla | Índice (columnas) | Consulta del programa a la que ayuda |
|---|---|---|
| `Paciente` | `(Activo, Apellidos, Nombres)` | Lista y búsqueda de pacientes ordenada por apellido (`ORDER BY` + `OFFSET/FETCH` sin ordenar toda la tabla). |
| `Paciente` | único en `DniHash` | Buscar por DNI y rechazar DNI duplicados sin descifrar nada. |
| `HistoriaClinica` | único en `NumeroHistoria`; único en `IdPaciente` | Búsqueda por N.º de historia y el `JOIN` paciente ↔ historia. |
| `Atencion` | `(IdHistoria, Fecha)` y `(Fecha)` | Atenciones de una historia; indicadores y gráficos del dashboard y reportes por rango de fechas. |
| `Medicamento` | `(Activo, Nombre)` y `(Activo, FechaVencimiento)` | Lista/búsqueda por nombre; filtro "solo críticos" y alertas de vencimiento. |
| `MovimientoInventario` | `(IdMedicamento, Fecha)` y `(Fecha)` | Historial de un medicamento y reporte de movimientos por fechas. |
| `Usuario` | único en `NombreUsuario` | Inicio de sesión. |

```sql
-- Ver los índices REALES creados en la base (para presentar y para ajustar la tabla de arriba):
SELECT t.name AS Tabla, i.name AS Indice, i.is_unique AS EsUnico,
       STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS Columnas
FROM sys.indexes i
JOIN sys.tables t ON t.object_id = i.object_id
JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE i.type > 0
GROUP BY t.name, i.name, i.is_unique ORDER BY t.name, i.name;

-- Para demostrar la mejora: activar "Incluir plan de ejecución real" (Ctrl+M) y comparar Index Seek vs Table Scan.
SET STATISTICS IO ON; SET STATISTICS TIME ON;
```

**Paginación (50 filas por página).** — Una lista de 10 000 pacientes no se puede traer entera a la pantalla: tardaría y gastaría memoria. Por eso cada consulta trae **solo una página**:
- Un **`COUNT` aparte** dice cuántas filas cumplen la búsqueda (para mostrar *"Página 2 de 10 · 478 resultados"*), sin traer ninguna fila.
- La **página** se pide con `ORDER BY … OFFSET @desde ROWS FETCH NEXT @cantidad ROWS ONLY`: "sáltate las primeras N filas y dame las 50 siguientes". El `ORDER BY` es obligatorio y se desempata con el `Id` para que el orden sea siempre el mismo y no se repitan ni se pierdan filas entre páginas.
- Si la página pedida ya no existe (por ejemplo, se dieron de baja pacientes), se muestra la última.
- Limitación: `OFFSET` tiene que "saltar" las filas anteriores, así que en páginas muy profundas de tablas gigantes se vuelve más lento; para ese caso existe la paginación por *clave* (*keyset*), que quedó como mejora futura.

**Búsqueda por el inicio, no "contiene".** — Se busca por el **inicio** del apellido, del nombre o del N.º de historia (`LIKE 'quis%'`). Un `LIKE '%quis%'` (contiene) obliga a SQL Server a leer toda la tabla aunque exista el índice; el que busca por el inicio sí puede usarlo. Es un equilibrio entre comodidad y velocidad. Los comodines que el usuario escriba (`%`, `_`, `[`) se escapan para buscarse como texto.

**Espera de 300 ms y cancelación (U5).** — Si cada tecla lanzara una consulta, escribir "quispe" haría 6 consultas, 5 de ellas inútiles. El `Timer` espera **300 ms de pausa** antes de consultar; la consulta corre en un `BackgroundWorker` (la ventana no se congela). Si el usuario sigue escribiendo mientras una consulta está en curso, esa consulta se **cancela** y su resultado se **descarta**, y se lanza la del texto más reciente: así nunca se muestra un resultado viejo.

**Consultas más livianas.** — (1) Se **seleccionan solo las columnas necesarias** (la lista de pacientes ya no trae el DNI cifrado ni su hash). (2) **`SET NOCOUNT ON`** evita que SQL Server envíe el mensaje "N filas afectadas" tras cada instrucción; **no** se usa en los `UPDATE` cuyo número de filas se revisa (descontar stock, cambiar rol), porque con `NOCOUNT ON` esa cifra no llega y se perdería la protección contra el stock negativo. (3) **Parámetros con tipo y tamaño explícitos** (`SqlDbType`, longitud), nunca `AddWithValue`: SQL Server reutiliza el plan de ejecución y no adivina mal el tipo (lo que podría impedir usar un índice). (4) Conexiones, comandos y lectores siempre dentro de `using`.

**"Consulta en X ms".** — La barra de estado de cada ventana muestra cuánto tardó la consulta, medido con `Stopwatch` alrededor de las llamadas a la base de datos (el `COUNT` y la página, más la lectura de resultados). **No** incluye el dibujo de la pantalla. Sirve para demostrar en vivo el efecto de los índices y de la paginación.

**¿Para qué sirve `BackgroundWorker`?** — Comprimir y cifrar muchas historias toma tiempo. Si se hiciera en el hilo de la ventana, esta se **congelaría**. El `BackgroundWorker` hace el trabajo en otro hilo y avisa el avance (`ProgressChanged`) para llenar la barra; al terminar (`RunWorkerCompleted`) la ventana se actualiza sola. También permite **cancelar**.

**¿Por qué consultas parametrizadas?** — Si el SQL se armara pegando texto (`"... WHERE Nombre = '" + texto + "'"`), alguien podría escribir `'; DROP TABLE ...` y ejecutar comandos (inyección SQL). Con parámetros (`@nombre`), el texto del usuario viaja **como dato**, nunca como parte de la instrucción. En todo el proyecto no hay SQL armado con texto del usuario.

**¿Por qué baja lógica y no borrar?** — Una historia clínica es un documento médico que debe conservarse, y otros registros dependen de ella (atenciones, movimientos). Por eso "Eliminar" solo pone `Activo = 0`: el paciente o medicamento deja de aparecer en las listas, pero su historial se conserva y se puede auditar.

**Validar el rol en Negocio y no solo en la ventana.** — Ocultar un botón no es seguridad: cualquier cambio en el código de la ventana lo saltaría. Cada método de los servicios llama a `Permisos.Exigir…`, que lanza `AccesoDenegadoException` si el rol no corresponde.

---

## 4. 20 preguntas probables del profesor (con respuesta corta)

1. **¿Por qué `Persona` es una clase abstracta?** Porque no tiene sentido crear una persona genérica; solo agrupa lo común (DNI, nombres, edad) y obliga a usar un tipo concreto como `Paciente`.
2. **¿Dónde ve polimorfismo en su proyecto?** En `MostrarDatos()`: es `virtual` en `Persona` y `Paciente` la sobrescribe (`override`) para agregar la dirección.
3. **¿Cómo aplicó encapsulamiento?** Campos privados y propiedades con validación en el `set` (usando `Validador`), de modo que un objeto no puede quedar con un DNI inválido o stock negativo.
4. **¿Por qué usó AES y no DES?** DES tiene llave de 56 bits y está obsoleto; AES-256 es el estándar actual. Uso modo CBC con un IV aleatorio distinto por cada dato.
5. **¿Por qué PBKDF2 y no SHA1 para las contraseñas?** SHA1 es rápido y está debilitado; PBKDF2 es lento a propósito (100 000 iteraciones) y usa salt aleatorio por usuario, lo que frena la fuerza bruta y las tablas precalculadas.
6. **¿Para qué sirve el salt?** Para que contraseñas iguales produzcan hashes distintos y no sirvan tablas precalculadas. Se guarda junto al hash (no es secreto).
7. **¿Para qué sirve el HMAC del DNI si ya está cifrado?** El cifrado cambia cada vez, así que no se puede buscar ni detectar duplicados; el HMAC es estable y permite buscar por `DniHash` sin descifrar. Lleva llave secreta para que no se pueda adivinar probando los 100 millones de DNI.
8. **¿Dónde está la llave de cifrado?** No está en el código: se genera al azar la primera vez y se guarda cifrada con DPAPI en el almacenamiento aislado del usuario de Windows. (Si se pierde, los datos cifrados no se recuperan; por eso hay que respaldarla.)
9. **¿Qué pasa si falla al guardar la historia después de guardar al paciente?** Nada se guarda: ambos van en una `SqlTransaction` y se hace `Rollback`.
10. **¿Cómo evita que el stock quede negativo?** El `UPDATE` incluye `AND StockActual >= @cant` y reviso las filas afectadas; si es 0 devuelvo "Stock insuficiente" y hago `Rollback`. Además la tabla tiene `CHECK StockActual >= 0`.
11. **¿Qué hace el `BackgroundWorker` y por qué no se congela la ventana?** Ejecuta el respaldo en otro hilo y reporta progreso con eventos que se procesan en el hilo de la interfaz; la ventana sigue atendiendo clics y se puede cancelar.
12. **¿Cómo evita la inyección SQL?** Todas las consultas usan `SqlParameter` (`@nombre`, `@id`…); el texto del usuario nunca se concatena al SQL.
13. **¿Por qué baja lógica y no `DELETE`?** Por la obligación de conservar la historia clínica y la integridad con atenciones y movimientos: solo se marca `Activo = 0`.
14. **¿Por qué el mensaje de login es siempre "Usuario o contraseña incorrectos"?** Para no revelar si el usuario existe; incluso si no existe se calcula un hash de mentira para que el tiempo de respuesta sea parecido. Tras 5 fallos se bloquea 5 minutos.
15. **¿Cómo funciona el respaldo `.sigebak` y cómo sabe si está dañado?** Serializo a XML (`XmlSerializer`), comprimo con `GZipStream`, cifro con AES y agrego una firma HMAC. Al abrir verifico primero la firma: si el archivo fue alterado, está incompleto o es de otra llave, lo rechazo con un mensaje claro.

16. **¿Cómo se conectan Pacientes e Inventario y por qué es una transacción?** Al recetar dentro de una atención se hacen tres pasos —guardar la atención con su receta, descontar el stock y registrar el movimiento "Receta HC-xxxxxx"— en **una sola transacción**. Si el stock no alcanza o algo falla, se hace `Rollback` y no queda nada; así el inventario nunca se desfasa de las historias clínicas. Solo se puede recetar un medicamento activo, no vencido, con stock y de la posta del paciente.
17. **¿Cómo se arma el dashboard y qué ve cada rol?** Al iniciar sesión se muestra un panel que carga en un `BackgroundWorker` y se refresca cada 60 segundos. Las cifras se calculan en SQL con `COUNT` y `GROUP BY`; el servicio decide por rol qué consultas ejecutar: Administrador todo, Médico pacientes/atenciones/gráficos, Farmacia solo inventario. Si una consulta falla se muestra un mensaje amable y el detalle técnico va al registro de errores.
18. **¿Por qué ya no se elige la posta si existe la tabla `Posta`?** Porque el sistema es de un solo establecimiento: `Establecimiento` lee su `IdPosta` una sola vez al iniciar y lo asigna a todo lo nuevo. Se conservaron la tabla y la columna para que la estructura quede lista para crecer a una red de postas sin rediseñar la base de datos.
19. **¿Para qué sirven los índices y cómo lo demuestra?** Evitan leer toda la tabla: SQL Server va directo a las filas buscadas (*index seek*). Lo demuestro con el plan de ejecución real (Seek en vez de Scan) y con el tiempo que muestra la barra de estado («Consulta en X ms»). Tienen costo en espacio y en altas/modificaciones, por eso solo se crean donde las consultas los usan.
20. **¿Cómo evita traer miles de filas y consultar en cada tecla?** Pagina de a 50 con `COUNT` aparte y `OFFSET/FETCH`, y espera 300 ms de pausa (`Timer`) antes de buscar; la consulta corre en un `BackgroundWorker` y, si el usuario sigue escribiendo, la anterior se cancela y su resultado se descarta.

21. **¿Qué se agregó al detalle clínico?** Signos vitales por atención (peso, talla, presión, temperatura, frecuencia, saturación) con IMC calculado, alergias del paciente (con gravedad) y antecedentes cifrados. Los rangos válidos los valida `SignosVitales` en Negocio y coinciden con los `CHECK` de la base: dos barreras.
22. **¿Los colores ámbar/rojo son un diagnóstico?** No. Son una ayuda visual orientativa con valores de referencia de adulto (ej. temperatura > 37.5 °C ámbar, ≥ 39 °C rojo); la pantalla lo aclara y no impide guardar. El criterio es del profesional.
23. **¿Cómo se avisa de una alergia al recetar?** Dentro de la misma transacción de la receta, `PacienteServicio` compara el nombre del medicamento con las alergias activas (`Alergia.Coincide`, sin tildes ni mayúsculas). Si coincide y no hubo confirmación, lanza `ValidacionException` con campo "Alergia" y **no guarda nada**; la ventana pregunta Sí/No (por defecto No) y, solo con Sí, reintenta con `confirmaAlergia = true`.
24. **¿Por qué las alergias no se borran?** Porque son parte del historial médico: se desactivan (`Activo = 0`) y dejan de avisar, pero siguen visibles y en el respaldo.
25. **¿Cómo se protegen los antecedentes?** Igual que el diagnóstico: AES con IV aleatorio, guardados como `VARBINARY`; se descifran solo al abrir la pestaña. Solo Administrador y Médico acceden (Farmacia recibe `AccesoDenegadoException`).

26. **¿Qué es la auditoría y qué registra?** Una bitácora de *quién hizo qué y cuándo*: inicios de sesión (correctos y fallidos), bloqueos, cierres de sesión (también por inactividad), altas/cambios de usuarios y roles, restablecimiento de contraseña, altas/ediciones/bajas de pacientes y medicamentos, atenciones, movimientos de stock, respaldos, restauraciones y accesos denegados. `AuditoriaServicio.Registrar(accion, entidad, idEntidad, detalle, resultado)` toma el usuario de la `Sesion` e inserta con una consulta parametrizada.
27. **¿Qué pasa si falla el registro de auditoría?** Nada para el usuario: `Registrar` captura cualquier excepción, la manda al archivo `errores.log` (solo tipo y traza) y la operación principal continúa. Es una decisión de diseño: la auditoría es importante, pero no debe impedir atender a un paciente. (Se llama *después* del `Commit`, nunca dentro de la transacción.)
28. **¿Qué datos NO se guardan en la auditoría y por qué?** Ni DNI, ni nombres, ni diagnósticos, ni antecedentes, ni contraseñas ni rutas de archivos: solo identificadores ("Paciente Id 12"). La bitácora la lee el Administrador y se exporta a CSV; si tuviera datos clínicos, sería una segunda copia sin cifrar. Incluso el usuario de un login fallido se omite si no existe, por si alguien escribió su contraseña en el campo equivocado.
29. **¿Cómo se garantiza que nadie edite o borre la auditoría?** Tres capas: (1) la tabla tiene un *trigger* que rechaza `UPDATE` y `DELETE`; (2) la aplicación no tiene ninguna instrucción `UPDATE`/`DELETE` sobre esa tabla (solo `INSERT` y `SELECT`); (3) la pantalla `FrmAuditoria` es de solo lectura y solo el Administrador entra. Se demuestra con las consultas 16–20 de `Pruebas.md`: el `UPDATE` y el `DELETE` fallan y la tabla queda igual. Límite honesto: el trigger no actúa con `TRUNCATE` y un administrador de SQL Server podría deshabilitarlo, por eso conviene además quitar esos permisos al inicio de sesión de la aplicación.
30. **¿Cómo se registran los accesos denegados?** En un solo lugar: `Permisos.Exigir…` guarda el intento (módulo y rol) justo antes de lanzar `AccesoDenegadoException`, así que ninguna pantalla ni servicio puede olvidarlo.

31. **¿Cómo valida el RUC?** En `Validador.MensajeRuc`: 11 dígitos, prefijo 10, 15, 17 o 20 y dígito verificador. Se multiplican los 10 primeros dígitos por los pesos 5,4,3,2,7,6,5,4,3,2 y se suman; el verificador es `11 - (suma mod 11)`, y si da 10 vale 0 y si da 11 vale 1; debe coincidir con el dígito 11. Ejemplo: `20100070970` es válido y `20100070971` no. Solo valida el formato matemático: no consulta a la SUNAT.

32. **¿Cómo evita RUC duplicados aunque dos usuarios guarden a la vez?** Dos defensas: el servicio pregunta primero (`ExisteRuc`, para dar un mensaje claro) y la columna `Ruc` es `UNIQUE` en SQL Server; si aun así se cuela uno, `ProveedorDatos` atrapa el error 2627/2601 y lo traduce al mismo mensaje *"Ya existe un proveedor con ese RUC"*.

33. **¿Por qué un ingreso exige proveedor y documento, y la salida no?** Un ingreso es una compra o donación y debe poder rastrearse (¿quién lo entregó y con qué guía o factura?); una salida es consumo interno. La regla está en `InventarioServicio.ValidarIngreso` y la pantalla solo desactiva los campos en las salidas. El proveedor se comprueba **dentro de la misma transacción** que suma el stock (`WITH (UPDLOCK)`): si alguien lo da de baja justo antes, el ingreso se rechaza y el stock no cambia.

34. **¿Qué pasa con los movimientos de un proveedor dado de baja?** La baja es lógica (`Activo = 0`), nunca `DELETE`: la clave foránea sigue válida y el historial hace `LEFT JOIN` sin filtrar por `Activo`, así que sigue mostrando su nombre. Solo deja de ofrecerse en nuevos ingresos.

35. **¿Cómo se guarda el diagnóstico CIE-10 de una atención y por qué cifrado?** El código es un dato de salud del paciente, así que se trata como el diagnóstico: `Cie10Servicio.CifrarCodigo` lo cifra con AES-256-CBC y un IV aleatorio y se guarda en `Atencion.Cie10Cifrado` (VARBINARY). Nunca va en claro a la base de datos, ni a la auditoría (solo "Atención Id …, historia Id …") ni a `errores.log`. Al mostrar la atención se descifra en memoria y se busca su descripción en el catálogo `Cie10`, que sí es información pública y va en claro.

36. **¿Por qué el reporte "Diagnósticos más frecuentes" no usa `GROUP BY` en SQL?** Porque el código está cifrado con un IV aleatorio: el mismo código produce bytes distintos en cada atención, así que SQL Server no puede agruparlas ni contarlas. `ReporteServicio.DiagnosticosMasFrecuentes` trae las atenciones del rango (solo fecha y bytes), descifra cada código en memoria y agrupa con LINQ (`GroupBy`, `Count`, `OrderByDescending`). Es el costo de proteger el dato; por eso el reporte lleva rango de fechas. Las atenciones antiguas sin código y los códigos ilegibles se cuentan aparte.

37. **¿Cómo busca ignorando tildes y mayúsculas?** En SQL con `COLLATE Latin1_General_CI_AI` (CI = sin mayúsculas, AI = sin tildes) sobre el código y la descripción. El código se busca por el inicio y cada palabra de la descripción por contenido (hasta 4 palabras, todas deben aparecer); cada palabra va en su propio parámetro, así la consulta es una sola cadena constante y sin concatenar texto del usuario. Un `LIKE '%x%'` no usa índice, pero el catálogo es pequeño.

38. **¿Qué pasa si el código guardado no se puede descifrar?** La lista de atenciones carga igual: `DescifrarCodigo` captura el error y marca la atención como "[Código CIE-10 ilegible]" (también si el resultado no tiene formato CIE-10). No se muestra texto técnico y no se escribe nada en el registro de errores, para que un dato dañado no deje rastro de su contenido.

39. **¿Cómo funciona el autocompletado sin congelar la ventana?** `CuadroCie10` reutiliza `ControladorBusqueda`: un `Timer` espera 300 ms de pausa y la consulta corre en un `BackgroundWorker`; si el usuario sigue escribiendo, la consulta anterior se cancela y su resultado se descarta. Solo vale un código **elegido** de la lista: si se edita el texto después, la elección se anula, así nunca se guarda un código distinto al que se ve.

40. **¿Por qué se separó "Presentación" en concentración, forma y unidad?** Porque un solo texto libre ("Paracetamol 500 mg tabletas") no se puede usar para nada más que leerlo. Con datos estructurados: (1) **se puede filtrar** (el filtro por forma farmacéutica es una condición `IdForma = @forma`, no una búsqueda de texto); (2) **se evitan errores de escritura** ("tableta", "Tabletas", "tab."): se elige de una lista y los nombres no se repiten gracias al `UNIQUE`; (3) **se puede reportar y mostrar bien**: el stock lleva su unidad ("10 tabletas", "1 tableta") y el nombre completo se arma igual en todas las pantallas (`Medicamento.NombreCompleto`). La columna `Presentacion` se conserva por compatibilidad: los medicamentos antiguos la siguen mostrando y los nuevos la llenan con "concentración - forma".

41. **¿Cómo se trata a los medicamentos antiguos?** Quedaron en "Sin definir" (valor del catálogo). `NombreCompleto` detecta esa forma y usa la `Presentacion` antigua, así nada deja de verse. Para guardarlos hay que elegir forma y unidad reales (son obligatorias en `InventarioServicio.Validar`, que además comprueba contra el catálogo que existan, estén activas y no sean "Sin definir"). "Sin definir" no se puede renombrar ni dar de baja.

42. **¿Qué pasa al dar de baja una forma que usan medicamentos?** La baja es lógica (`Activo = 0`). La pantalla cuenta los medicamentos activos que la usan y avisa antes; esos medicamentos **conservan** su `IdForma` (la clave foránea sigue válida), solo deja de ofrecerse en medicamentos nuevos y al editar. Las dos tablas tienen la misma forma, así que una sola clase (`CatalogoDatos`) y una sola ventana atienden a ambas; cada tabla tiene su SQL constante (sin concatenar nombres de tabla).

43. **¿Cómo se calcula el plural del stock?** `Medicamento.FormatoCantidad`: 1 → singular; si no, termina en vocal → +s (tableta → tabletas), en consonante → +es (inhalador → inhaladores), en "z" → "ces", en "ión" → "iones", y si ya termina en "s" queda igual. Las abreviaturas (hasta 3 letras o con mayúsculas dentro, como "mL") no cambian. Es una regla sencilla del español, no un diccionario: un nombre irregular habría que ajustarlo.

*(Si le sobra tiempo para practicar: ¿qué es LINQ y dónde lo usa? → `InventarioServicio.Criticos` filtra con `Where` y ordena con `OrderByDescending`; `ReporteServicio` agrupa con `GroupBy`.)*

---

## 5. Limitaciones conocidas y mejoras futuras

**Fuera del alcance de 2 servicios (no implementado):**
- **Citas y cola de atención:** no existen.
- *(La conexión **receta → stock** sí está implementada, con las limitaciones de abajo.)*

**Limitaciones del sistema actual:**
- **Receta:** una atención admite **un solo medicamento** (un `IdMedicamento` y una cantidad). Una receta guardada **no se puede anular ni devolver al stock** desde el programa (habría que registrar un *Ingreso* manual en Inventario). Solo se receta stock de **la posta del paciente**; no hay transferencias entre postas. El *tratamiento* (indicaciones) sigue sin guardarse.
- **"Restaurar" solo verifica y muestra** el contenido del `.sigebak`; no vuelve a escribir los datos en la base.
- El respaldo se abre **solo con el mismo usuario de Windows en el mismo equipo** (DPAPI). Cambiar de equipo exige migrar la llave.
- El respaldo del establecimiento incluye solo pacientes **activos** (los dados de baja no se listan).
- El *tratamiento* (indicaciones) sigue sin guardarse. Los **antecedentes** sí se guardan (cifrados) por historia.
- **Alergias:** el aviso al recetar compara solo el **nombre** (uno contiene al otro, mínimo 3 letras); no reconoce familias de fármacos (ej. alergia a "penicilinas" frente a "amoxicilina"). Es una ayuda, no sustituye la pregunta al paciente.
- **Auditoría:** solo se registra lo que pasa a través de la aplicación; los cambios hechos directamente en SSMS no pasan por ella. Los respaldos "restaurar" solo verifican el archivo, y así se registran. No hay (todavía) auditoría de *consultas* de historias clínicas, solo de cambios y accesos denegados.
- **Proveedores:** el RUC se valida solo por formato y dígito verificador (no se consulta a la SUNAT). Un proveedor dado de baja sigue ocupando su RUC (la restricción `UNIQUE` es de la base) y no hay pantalla para reactivarlo. El stock inicial de un medicamento nuevo se guarda como ingreso **sin** proveedor ni documento, y los movimientos anteriores a esta etapa también tienen esos campos vacíos.
- **CIE-10:** el catálogo es el que cargó el administrador (35 códigos de partida); no hay importación masiva ni reactivación de un código dado de baja desde el programa. El reporte de diagnósticos descifra en memoria, por lo que con millones de atenciones habría que acotar el rango. El **detalle del diagnóstico** (texto libre) sigue siendo obligatorio porque la columna cifrada ya existía; el código CIE-10 es obligatorio solo en atenciones nuevas y las antiguas muestran "Sin código CIE-10".
- **Formas y unidades:** el plural del stock usa una regla sencilla (ver pregunta 43) y no cambia nombres irregulares. Una forma o unidad dada de baja no se puede reactivar desde el programa ni reutilizar su nombre (el `UNIQUE` sigue aplicando). Al editar un medicamento cuya forma está dada de baja hay que elegir otra activa. No se cambió la columna `Presentacion` de los antiguos: solo se deja de usar al crear o editar.
- **Signos vitales:** los colores usan referencias de adulto; en niños los valores normales son distintos.
- El **DNI no se puede editar** una vez registrado (es la identidad del paciente), y la **dirección** no se captura en la pantalla (se guarda vacía).
- Las **100 000 iteraciones** de PBKDF2 son aceptables, pero las recomendaciones actuales son más altas. El número se guarda por usuario, así que se puede subir sin invalidar contraseñas existentes.
- El cifrado de DNI/diagnóstico en la BD (AES-CBC) no lleva firma por dato; solo el archivo de respaldo lleva HMAC.
- La búsqueda de pacientes y medicamentos es por el **inicio** del texto (no «contiene»), a cambio de poder usar índices. La paginación con `OFFSET` se vuelve más lenta en páginas muy profundas de tablas enormes (alternativa: paginación por clave).
- Los **índices** los creó el administrador de la base de datos; el programa no los crea ni los verifica: si faltaran, las consultas seguirían funcionando pero más lentas.
- El tiempo de inactividad (10 min) y el de actualización del dashboard (60 s) están fijos en el código; no son configurables.
- El dashboard y los reportes muestran los datos del establecimiento único: no hay selección de posta; los gráficos son fijos (este mes / últimos 7 días) y los reportes filtran solo por rango de fechas.
- **Red de postas:** hoy no hay reportes ni tableros por posta ni transferencia de stock entre postas; la estructura de la base de datos sí lo permite (ver mejoras).
- La sesión es **por proceso** (una persona por ejecución); no hay auditoría de accesos en la base.
- Las **pruebas son manuales** (`docs/Pruebas.md`); no hay pruebas automatizadas.
- `Form1.cs` es una plantilla de Visual Studio que quedó en el proyecto sin usarse.

**Mejoras futuras:**
1. **Recetas con varios medicamentos** (tabla de detalle de receta), **anulación de receta** con devolución de stock y receta con indicaciones/tratamiento.
2. **Citas y cola de atención** por día.
3. **Crecer a una red de postas:** volver a ofrecer la elección de posta, reportes y dashboard por posta, y transferencias de stock entre postas (la tabla `Posta` y la columna `IdPosta` ya existen).
4. **Restauración real** del respaldo (con validación de duplicados por `DniHash`).
5. Rotación de llaves y **cifrado autenticado** (por ejemplo AES-GCM en una versión más nueva de .NET).
6. **Auditoría** de accesos y cambios (quién vio o modificó qué historia).
7. **Pruebas unitarias** (separando interfaces para poder simular la capa de Datos).
8. Tiempo de inactividad e iteraciones de PBKDF2 **configurables**; instalador y copia de seguridad completa de SQL Server programada.
