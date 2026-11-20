# Pruebas manuales — SIGEPOSTA

Cómo usar este documento: ejecute cada caso en orden, compare con **Resultado esperado** y anote en **Resultado** `OK` o `FALLÓ` (con una nota breve y, si puede, una captura).
Los mensajes entre comillas son **los textos reales** que muestra el programa.

**Preparación previa**
- Base `SIGEPOSTA` con sus tablas y la tabla `Posta` con UNA fila, la de la Posta Jesús Nazareno (ver `README.md`).
- Tres usuarios: `admin` (Administrador), `medico1` (Médico), `farmacia1` (Farmacia).
- Al menos un medicamento con stock conocido (ej.: *Paracetamol*, stock 10).
- Fecha de la prueba: ____ / ____ / ________  Probado por: ______________________

---

## A. Pruebas principales

| N.º | Caso | Pasos | Resultado esperado | Resultado |
|---|---|---|---|---|
| 1 | **DNI de 5 dígitos** | Entrar como `admin` o `medico1` → *Pacientes*. Escribir DNI `12345`, completar nombres y apellidos y pulsar **Guardar**. | No se guarda. Bajo el campo DNI, en rojo: *"El DNI debe tener exactamente 8 dígitos numéricos."* (El cuadro solo admite números, máximo 8.) | |
| 2 | **DNI duplicado** | Registrar un paciente con DNI `45678912`. Volver a registrar otro con el **mismo** DNI. | El segundo no se guarda. Bajo el DNI: *"Ya existe un paciente con ese DNI"*. En la base queda un solo registro con ese `DniHash`. | |
| 3 | **Fecha de nacimiento futura** | En *Pacientes*, abrir el calendario de *Fecha de nacimiento* e intentar elegir un día posterior a hoy; también intentar escribirlo con el teclado. | El selector **no permite** fechas futuras (el máximo es la fecha de hoy). Si una fecha futura llegara al servicio, se rechaza con *"…no puede ser una fecha futura."* | |
| 4 | **Salida mayor al stock** | Como `admin` o `farmacia1` → *Inventario*. Elegir *Paracetamol* (stock 10). Tipo **Salida**, cantidad `11`, **Registrar movimiento**. | Bajo *Cantidad*, en rojo: *"Stock insuficiente. Disponible: 10, solicitado: 11."* El stock **sigue en 10** y **no** aparece movimiento nuevo en *Ver movimientos*. | |
| 5 | **Stock negativo** | (a) En *Stock inicial* o *Stock mínimo* intentar escribir `-5`. (b) Registrar una salida con cantidad `0`. (c) Sacar todo el stock con una salida exacta (cantidad = stock) y luego intentar otra salida de `1`. | (a) El signo `-` no se acepta. (b) *"La cantidad debe ser mayor que cero."* (c) La primera salida deja el stock en `0`; la segunda se rechaza con *"Stock insuficiente…"*. El stock **nunca** es negativo. *(Prueba opcional en una base de PRUEBAS: ver consulta 3 al final; la tabla rechaza un valor negativo por su `CHECK`.)* | |
| 6 | **Contraseña débil** | Como `admin` → *Usuarios*. Crear un usuario con contraseña `abc` y luego `abcdefgh` y luego `Abcdefgh` (repetida igual). | Cada intento muestra bajo *Contraseña* el primer requisito que falla: *"La contraseña debe tener al menos 8 caracteres."*, *"…incluir al menos una letra mayúscula."*, *"…incluir al menos un número."* No se crea el usuario. Con `Abcdefg1` sí se crea. | |
| 7 | **5 intentos fallidos de login (bloqueo)** | En el login escribir 5 veces seguidas un usuario **válido** (`farmacia1`) con contraseña incorrecta. Luego intentar con la contraseña **correcta**. Esperar 5 minutos y volver a intentar. | Los 5 intentos muestran *"Usuario o contraseña incorrectos"* (siempre el mismo texto). Con la clave correcta **dentro de los 5 minutos también** se rechaza (cuenta bloqueada, sin avisar el motivo). Pasados 5 minutos, el ingreso funciona. En la base: `IntentosFallidos` y `BloqueadoHasta` cambian (consulta 4). | |
| 8 | **Acceso de Médico a Inventario** | Entrar como `medico1`. Revisar el menú. | El menú muestra *Pacientes, Reportes, Respaldo, Sesión*; **no** aparece *Inventario* ni *Usuarios*. En *Reportes* solo está *"Pacientes atendidos por día"*. | |
| 9 | **Acceso de Farmacia a Pacientes** | Entrar como `farmacia1`. Revisar el menú. | El menú muestra *Inventario, Reportes, Sesión*; **no** aparece *Pacientes*, *Respaldo* ni *Usuarios*. En *Reportes* solo están los 2 de inventario. | |
| 10 | **DNI y diagnóstico ilegibles en la base** | Registrar un paciente (DNI `45678912`) y una atención con diagnóstico `Hipertensión arterial leve`. En SSMS ejecutar la consulta 1 y la consulta 2 (al final). | `DniCifrado` y `DiagnosticoCifrado` aparecen como `0x…` (solo bytes), **sin** el DNI ni el texto del diagnóstico. `DniHash` tiene **64 caracteres hexadecimales**. Ninguna columna muestra el DNI en claro. | |
| 11 | **Respaldo `.sigebak` ilegible** | Como `admin` o `medico1` → *Respaldo* → respaldar un paciente o todas las historias del establecimiento. Abrir el archivo `.sigebak` con el Bloc de notas (o un editor hexadecimal) y buscar el DNI o parte del diagnóstico. | El archivo se ve como **caracteres ilegibles**; empieza con la marca `SIGEBAK1`; **no** se encuentra el DNI, ni el diagnóstico, ni texto XML (`<Pacientes`). | |
| 12 | **Restauración (verificación) de un archivo dañado** | (a) En *Respaldo* → **Abrir respaldo…** elegir el `.sigebak` correcto. (b) Hacer una copia y cambiar o borrar unos bytes con un editor hexadecimal; abrirla. (c) Truncar otra copia (dejarla en pocos bytes); abrirla. (d) Abrir un archivo cualquiera renombrado a `.sigebak`. | (a) Se muestra el contenido (pacientes y atenciones). (b) *"No se pudo verificar el respaldo: el archivo está dañado o fue modificado, o fue creado con otra llave (otro usuario de Windows u otro equipo)."* (c) *"El archivo está incompleto o no es un respaldo de SIGEPOSTA (.sigebak)."* (d) *"El archivo no es un respaldo de SIGEPOSTA (.sigebak) o es de una versión no compatible."* En (b), (c) y (d) no se muestra contenido y el programa no se cierra. | |
| 13 | **Cierre de sesión por inactividad** | Iniciar sesión (ej. `admin`), abrir *Pacientes* y **no tocar** el teclado ni el mouse durante **10 minutos**. | Pasados 10 minutos se cierran las ventanas abiertas y aparece el login con el aviso: *"Su sesión se cerró por 10 minutos sin actividad. Inicie sesión de nuevo."* Sin iniciar sesión no se puede acceder a nada. *(Mientras corre un respaldo la cuenta regresiva se pausa.)* | |

---

## B. Pruebas adicionales recomendadas

| N.º | Caso | Pasos | Resultado esperado | Resultado |
|---|---|---|---|---|
| 14 | **Primera vez: crear administrador** | Con la tabla `Usuario` vacía, abrir el programa. | Aparece *"Administrador inicial"*. Tras crearlo, se pide iniciar sesión. No existe ninguna contraseña predeterminada. | |
| 15 | **Administrador no puede quedarse sin administradores** | Como `admin` (único administrador) intentar **desactivarse** y luego cambiarse el rol. Crear un segundo administrador y, entrando con él, desactivar al primero; luego intentar que el segundo se desactive a sí mismo. | Desactivarse o cambiarse el rol a sí mismo: *"No puede desactivar su propia cuenta."* / *"No puede cambiar su propio rol. Pídaselo a otro administrador."* Por eso siempre queda activo al menos el administrador que hace la acción. *(El mensaje "Debe existir al menos un administrador activo." es una protección adicional, también dentro del `UPDATE` de la base, ante cambios simultáneos; desde la pantalla normalmente no se llega a verlo.)* | |
| 16 | **Alertas de inventario** | Registrar tres medicamentos: uno con stock ≤ mínimo, uno que venza en ≤ 30 días y uno con fecha ya pasada. | Filas **ámbar** (stock bajo / por vencer) y **roja** (vencido); la columna *Alerta* lo indica. *Solo críticos* oculta los demás. | |
| 17 | **Baja lógica** | Dar de baja (*Eliminar*) a un paciente y a un medicamento. Revisar la base. | Dejan de aparecer en las listas, pero las filas siguen en la base con `Activo = 0` (consulta 5). Su historia clínica / movimientos se conservan. | |
| 18 | **Reporte con rango inválido** | En *Reportes* poner *Desde* posterior a *Hasta* y **Generar**. | Mensaje bajo *Desde*: *"'Desde' no puede ser posterior a 'Hasta'."* | |
| 19 | **CSV seguro** | Registrar un motivo de movimiento que empiece con `=` (ej. `=1+1`), generar *Movimientos de inventario* y **Exportar a CSV**; abrir en Excel. | La celda se ve como texto (`'=1+1`), **no** se calcula como fórmula. | |
| 20 | **Error de conexión sin texto técnico** | Detener el servicio de SQL Server y abrir el programa (o hacer una operación). | Mensaje amable de conexión (con un código de referencia). **Nunca** se muestra texto técnico, nombre del servidor ni detalles de SQL. | |

---

## C. Consultas de apoyo (SSMS)

Son consultas de **solo lectura**, salvo la 3, que es opcional y solo para una base de **pruebas**.

```sql
-- 1) DNI cifrado: debe verse como bytes (0x...), nunca el DNI en claro.
SELECT TOP 5 IdPaciente, DniCifrado, DniHash, LEN(DniHash) AS LargoDniHash, Nombres, Apellidos
FROM Paciente ORDER BY IdPaciente DESC;

-- 2) Diagnóstico cifrado: debe verse como bytes (0x...).
SELECT TOP 5 IdAtencion, Motivo, DiagnosticoCifrado
FROM Atencion ORDER BY IdAtencion DESC;

-- 3) (OPCIONAL, SOLO EN BASE DE PRUEBAS) La tabla debe rechazar stock negativo por su CHECK.
--    Se hace dentro de una transacción que se revierte.
BEGIN TRAN;
    UPDATE Medicamento SET StockActual = -1 WHERE IdMedicamento = 1;   -- debe fallar por el CHECK
ROLLBACK;

-- 4) Intentos fallidos y bloqueo de un usuario.
SELECT NombreUsuario, IntentosFallidos, BloqueadoHasta, Activo
FROM Usuario WHERE NombreUsuario = 'farmacia1';

-- 5) Bajas lógicas (siguen en la base con Activo = 0).
SELECT IdPaciente, Nombres, Apellidos, Activo FROM Paciente WHERE Activo = 0;
SELECT IdMedicamento, Nombre, Activo FROM Medicamento WHERE Activo = 0;

-- 6) Kardex de un medicamento: el stock debe coincidir con ingresos menos salidas
--    (si todo el stock entró por movimientos, incluido el "Stock inicial").
SELECT m.Nombre, m.StockActual,
       SUM(CASE WHEN mv.Tipo = 'Ingreso' THEN mv.Cantidad ELSE -mv.Cantidad END) AS StockSegunMovimientos
FROM Medicamento m LEFT JOIN MovimientoInventario mv ON mv.IdMedicamento = m.IdMedicamento
GROUP BY m.Nombre, m.StockActual;
```

---

## D. Pruebas de la receta (conexión Pacientes ↔ Inventario)

**Preparación:** un paciente y un medicamento **Paracetamol 500 mg** (ambos de la Posta Jesús Nazareno), vigente y con **stock 10**. Anote el stock antes de cada caso. Entre como `medico1` (Pacientes → *Ver historia clínica*) salvo que se indique otra cosa.

| N.º | Caso | Pasos | Resultado esperado | Resultado |
|---|---|---|---|---|
| 21 | **Receta con stock suficiente** | En *Nueva atención*: motivo `Control`, diagnóstico `Cefalea`, **Medicamento** `Paracetamol 500 mg — stock 10`, **Cantidad** `4` → **Guardar atención**. Luego entre como `farmacia1` → *Inventario* y revise el medicamento y *Ver movimientos*. | Mensaje *"Atención registrada. Receta: Paracetamol x 4 (stock actualizado)."* En la tabla de atenciones, columna **Receta**: *"Paracetamol x 4"*. La lista de medicamentos ahora dice *"— stock 6"*. En Inventario el stock es **6** y aparece un movimiento **Salida** de 4 con motivo **"Receta HC-xxxxxx"** hecho por `medico1`. *(Consulta 7: atención y movimiento existen.)* | |
| 22 | **Receta mayor al stock (debe rechazarse y NO crear la atención)** | Con stock 6: en una atención nueva elegir *Paracetamol* y **Cantidad** `20` → **Guardar atención**. | Bajo *Cantidad*, en rojo: *"Stock insuficiente para recetar. Disponible: 6, solicitado: 20."* **No** se crea la atención (no aparece en la tabla; la consulta 8 devuelve el mismo número de filas que antes), el stock **sigue en 6** y **no** hay movimiento nuevo. Los datos escritos en motivo y diagnóstico se conservan para corregir la cantidad. | |
| 23 | **Receta de medicamento vencido** | (a) Con un medicamento cuya **fecha de vencimiento ya pasó** (cree uno en Inventario o cambie la fecha), abrir una historia y desplegar *Medicamento*. (b) Prueba límite, **solo en una base de PRUEBAS**: con la ventana de historia abierta y un medicamento vigente seleccionado, cambie en SSMS su `FechaVencimiento` a ayer y pulse **Guardar atención** (el Inventario no se puede abrir a la vez, porque las ventanas son modales). | (a) El medicamento vencido **no aparece** en la lista (tampoco los dados de baja ni los sin stock). (b) Se rechaza con *"El medicamento está vencido y no se puede recetar."* bajo *Medicamento*; **no** se crea atención, no cambia el stock y no hay movimiento. | |
| 24 | **Atención sin receta** | Dejar *Medicamento* en **"(Sin receta)"** y *Cantidad* vacía; motivo y diagnóstico completos → **Guardar atención**. | Mensaje *"Atención registrada."* (sin línea de receta). Columna **Receta** vacía. El stock de todos los medicamentos **no cambia** y **no** se crea ningún movimiento. En la base, `IdMedicamento` y `CantidadRecetada` quedan **NULL** (consulta 7). | |
| 25 | **Receta incompleta** | (a) Elegir un medicamento y dejar *Cantidad* vacía. (b) Escribir *Cantidad* sin elegir medicamento. (c) *Cantidad* `0`. | (a) *"Ingrese la cantidad a recetar."* (b) *"Seleccione el medicamento a recetar."* (c) *"La cantidad debe ser mayor que cero."* Todos bajo su campo, en rojo; no se guarda nada. | |
| 26 | **Permisos de la receta** | (a) Entre como `medico1`: no ve *Inventario* en el menú, pero en la historia sí puede recetar. (b) Entre como `farmacia1`. | (a) La receta funciona (caso 21) aunque el Médico no tenga acceso a Inventario. (b) Farmacia **no** ve *Pacientes* y por lo tanto no puede abrir historias ni recetar. | |

```sql
-- 7) Última(s) atención(es): la receta debe verse con IdMedicamento y CantidadRecetada (o ambos NULL si no hubo),
--    y el movimiento de salida correspondiente.
SELECT TOP 5 a.IdAtencion, a.Fecha, a.Motivo, a.IdMedicamento, a.CantidadRecetada, a.DiagnosticoCifrado
FROM Atencion a ORDER BY a.IdAtencion DESC;

SELECT TOP 5 mv.IdMovimiento, mv.Fecha, m.Nombre, mv.Tipo, mv.Cantidad, mv.Motivo, mv.IdUsuario
FROM MovimientoInventario mv INNER JOIN Medicamento m ON m.IdMedicamento = mv.IdMedicamento
WHERE mv.Motivo LIKE N'Receta HC-%' ORDER BY mv.IdMovimiento DESC;

-- 8) Conteo de atenciones de una historia (antes y después del caso 22 debe ser IGUAL).
SELECT COUNT(*) AS Atenciones FROM Atencion WHERE IdHistoria = 1;   -- cambie 1 por el IdHistoria probado

-- 9) Coherencia: el stock debe seguir cuadrando con el kardex (ver consulta 6 de la sección C).
```

---

## E. Panel de inicio (dashboard) — pruebas por rol

**Preparación:** datos de prueba con: varios pacientes activos; atenciones registradas **hoy**, en días anteriores de la semana y en otros días del mes, y medicamentos con las tres situaciones (**stock bajo**, **por vencer** en 30 días o menos y **vencido**). Los usuarios `admin`, `medico1` y `farmacia1` ya existen. Compare cada cifra con la **consulta 10** del final de esta sección.

| N.º | Caso | Pasos | Resultado esperado | Resultado |
|---|---|---|---|---|
| 27 | **Administrador: ve todo** | Iniciar sesión como `admin`. | Es la **primera pantalla** (con el menú arriba). Saludo *"Bienvenido, <nombre> — Administrador"* y la fecha de hoy. **6 tarjetas** (Pacientes registrados, Atenciones de hoy, Atenciones del mes, Stock bajo, Por vencer, Vencidos), los **2 gráficos** (barras por día del mes y línea de 7 días) y la lista **Alertas de inventario**. Las cifras coinciden con la consulta 10. | |
| 28 | **Médico: pacientes y atenciones, sin inventario** | Iniciar sesión como `medico1`. | Saludo *"… — Médico"*. Solo **3 tarjetas** (pacientes, atenciones de hoy y del mes) y los **2 gráficos**. **No** aparecen tarjetas de medicamentos ni la lista de alertas, y el menú no tiene *Inventario*. | |
| 29 | **Farmacia: inventario, sin pacientes** | Iniciar sesión como `farmacia1`. | Saludo *"… — Farmacia"*. Solo las **3 tarjetas de medicamentos** y la lista de alertas. **No** aparecen tarjetas de pacientes/atenciones ni gráficos, y el menú no tiene *Pacientes*. | |
| 30 | **Las consultas solo se ejecutan si el rol tiene permiso** | Abrir **SQL Server Profiler** (o una sesión de *Extended Events*) filtrando la base `SIGEPOSTA`. Iniciar sesión como `farmacia1` y observar; repetir con `medico1`. | Con **Farmacia** no se ve ninguna consulta sobre `Paciente`, `Atencion` ni `HistoriaClinica`; solo sobre `Medicamento`. Con **Médico** no se ve ninguna sobre `Medicamento`. Todas son consultas parametrizadas (`@hoy`, `@desde`, …). | |
| 31 | **Colores de las tarjetas de inventario** | (a) Con stock bajo, por vencer y vencidos mayores que 0. (b) Dejar los tres en 0 (corregir stock y fechas, o dar de baja esos medicamentos de prueba). | (a) *Stock bajo* y *Por vencer* se ven en **ámbar**; *Vencidos* en **rojo**. (b) Las tres tarjetas vuelven a **blanco** con el número en morado. Las tarjetas de pacientes siempre son blancas. | |
| 32 | **Lista de alertas y clic** | Como `admin` o `farmacia1`, revisar la lista. Hacer clic en una fila. | Máximo **5** medicamentos, del más grave al menos grave: **vencidos** (rojo) primero, luego **por vencer** (ámbar), luego **stock bajo** (ámbar). Cada fila muestra nombre, stock y la alerta. Al hacer clic se abre **Inventario**; al cerrarlo, el panel **se actualiza solo**. Si no hay alertas, aparece *"Sin alertas: el inventario está en orden."* | |
| 33 | **Gráficos** | Como `admin` o `medico1`, revisar ambos gráficos y compararlos con la consulta 10 (c y d). | Barras: una por **día del mes** (del 1 hasta hoy), con 0 en los días sin atenciones; con pocos días muestran su valor encima y con muchos se lee al pasar el cursor. Línea: **siempre 7 puntos** (los últimos 7 días, incluido hoy), con **0** en los días sin atenciones. El eje Y muestra números enteros. Sin atenciones: aviso *"Sin atenciones en este período"*. | |
| 34 | **Carga sin congelar la ventana** | Al iniciar sesión (o pulsar **Actualizar**), intentar mover la ventana, abrir el menú o cambiar de tamaño mientras aparece *"Cargando…"*. | Aparece *"Cargando…"* y el botón *Actualizar* se desactiva mientras carga. La ventana **responde** (no se congela). Al terminar dice *"Actualizado a las HH:mm:ss"*. | |
| 35 | **Actualización manual y automática (60 s)** | Con el panel abierto, registrar (desde otra sesión, SSMS o el propio programa) una atención o un movimiento de stock. (a) Pulsar **Actualizar**. (b) Repetir y esperar **hasta 60 segundos** sin tocar nada. | (a) Las cifras cambian de inmediato. (b) Se actualizan **solas** en el siguiente ciclo (cambia la hora de *"Actualizado a las…"*). Al volver de otra ventana (Pacientes, Inventario, Respaldo…) el panel también se actualiza. | |
| 36 | **Error de conexión: mensaje amable** | Detener el servicio de SQL Server y pulsar **Actualizar**. Luego reiniciarlo y volver a pulsar. | Aparece, en el propio panel y en rojo, *"No se pudieron cargar los datos de … Verifique la conexión con la base de datos y pulse Actualizar."* con un **código de referencia**; las tarjetas muestran **"—"**. **Nunca** se ve texto técnico (servidor, tablas, SQL) ni ventanas de error repetidas cada minuto. Al volver el servicio, se recupera solo y el mensaje desaparece. | |
| 37 | **Cambio de tamaño** | Reducir la ventana al mínimo y luego maximizarla. | Las tarjetas se reparten en **columnas iguales con 12 px entre ellas**, los gráficos se estiran sin deformarse y, si la ventana es muy baja, aparece una **barra de desplazamiento** en lugar de aplastar los controles. | |
| 38 | **El refresco automático no evita el cierre por inactividad** | Dejar el panel abierto, sin tocar teclado ni mouse, durante **10 minutos**. | Aunque el panel se actualiza cada 60 s, la sesión **sí se cierra** a los 10 minutos (las actualizaciones automáticas no cuentan como actividad del usuario) y se vuelve al login con el aviso de inactividad. | |

```sql
-- 10) Valores de referencia para comparar con el dashboard (solo lectura).
-- a) Pacientes registrados (activos)
SELECT COUNT(*) AS PacientesActivos FROM Paciente WHERE Activo = 1;

-- b) Atenciones de hoy y del mes
SELECT
  (SELECT COUNT(*) FROM Atencion WHERE Fecha >= CAST(GETDATE() AS DATE)
                                  AND Fecha <  DATEADD(DAY, 1, CAST(GETDATE() AS DATE)))   AS AtencionesHoy,
  (SELECT COUNT(*) FROM Atencion WHERE Fecha >= DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1)
                                  AND Fecha <  DATEADD(DAY, 1, CAST(GETDATE() AS DATE)))   AS AtencionesMes;

-- c) Atenciones por día del mes (los días sin atenciones no aparecen aquí: el programa los muestra con 0)
SELECT CAST(Fecha AS DATE) AS Dia, COUNT(*) AS Total
FROM Atencion
WHERE Fecha >= DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1)
  AND Fecha <  DATEADD(DAY, 1, CAST(GETDATE() AS DATE))
GROUP BY CAST(Fecha AS DATE) ORDER BY Dia;

-- d) Atenciones por día, últimos 7 días (los días sin atenciones no aparecen aquí: el programa los muestra con 0)
SELECT CAST(Fecha AS DATE) AS Dia, COUNT(*) AS Total
FROM Atencion
WHERE Fecha >= DATEADD(DAY, -6, CAST(GETDATE() AS DATE)) AND Fecha < DATEADD(DAY, 1, CAST(GETDATE() AS DATE))
GROUP BY CAST(Fecha AS DATE) ORDER BY Dia;

-- e) Indicadores de inventario (solo medicamentos activos)
SELECT
  SUM(CASE WHEN StockActual <= StockMinimo THEN 1 ELSE 0 END)                                      AS StockBajo,
  SUM(CASE WHEN FechaVencimiento >= CAST(GETDATE() AS DATE)
            AND FechaVencimiento <= DATEADD(DAY, 30, CAST(GETDATE() AS DATE)) THEN 1 ELSE 0 END)  AS PorVencer,
  SUM(CASE WHEN FechaVencimiento < CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END)                      AS Vencidos
FROM Medicamento WHERE Activo = 1;

-- f) Los 5 más críticos (mismo orden que la lista del panel)
SELECT TOP (5) m.Nombre, m.StockActual, m.StockMinimo, m.FechaVencimiento
FROM Medicamento m
WHERE m.Activo = 1 AND (m.StockActual <= m.StockMinimo OR m.FechaVencimiento <= DATEADD(DAY, 30, CAST(GETDATE() AS DATE)))
ORDER BY CASE WHEN m.FechaVencimiento < CAST(GETDATE() AS DATE) THEN 0
              WHEN m.FechaVencimiento <= DATEADD(DAY, 30, CAST(GETDATE() AS DATE)) THEN 1 ELSE 2 END,
         m.FechaVencimiento, m.StockActual, m.Nombre;
```

---

## G. Establecimiento único (Posta Jesús Nazareno)

**Preparación:** la tabla `Posta` tiene **una sola fila**. Entre con el usuario indicado en cada caso.

| N.º | Caso | Pasos | Resultado esperado | Resultado |
|---|---|---|---|---|
| 39 | **Nombre del establecimiento visible** | Abrir el programa; iniciar sesión como `admin`. | La ventana de **inicio de sesión** muestra *"SIGEPOSTA — Posta Jesús Nazareno"* (título y barra roja). La **ventana principal** tiene el título *"SIGEPOSTA — Posta Jesús Nazareno"* y en su barra roja *"Posta Jesús Nazareno | <nombre> · Administrador"*. El **panel de inicio** muestra *"Posta Jesús Nazareno · <fecha>"* bajo el saludo. | |
| 40 | **Sin lista de postas en Pacientes** | Abrir *Pacientes*. | El recuadro *Datos personales* tiene 6 campos en **3 columnas del mismo ancho, sin huecos**: DNI, Nombres, Apellidos / Fecha de nacimiento, Teléfono, Dirección (opcional). **No** hay lista "Posta" y la tabla muestra solo *N.º historia, Paciente, Teléfono*. | |
| 41 | **Paciente nuevo se guarda con la posta del establecimiento** | Registrar un paciente (con y sin dirección). Ejecutar la consulta 11. | El paciente se guarda sin pedir posta, y su `IdPosta` es el de la **Posta Jesús Nazareno**. La dirección escrita se conserva; al seleccionar al paciente en la tabla aparece en el campo *Dirección*. | |
| 42 | **Sin lista de postas en Inventario** | Abrir *Inventario* (como `admin` o `farmacia1`). | *Datos del medicamento* tiene **Nombre** (ocupa 2 columnas) y *Presentación* en la primera fila, y *Stock mínimo*, *Fecha de vencimiento* y *Stock inicial* en la segunda: sin huecos y con columnas iguales. **No** hay lista "Posta" y la tabla no tiene columna *Posta*. | |
| 43 | **Medicamento nuevo se guarda con la posta del establecimiento** | Registrar un medicamento. Ejecutar la consulta 11. Luego **editarlo** (cambiar el stock mínimo) y volver a ejecutarla. | Se guarda con el `IdPosta` de la Posta Jesús Nazareno sin pedirlo. Al editar, su `IdPosta` **no cambia**. | |
| 44 | **Historia clínica sin campo Posta** | Abrir la historia de un paciente. | En *Datos del paciente* el sexto campo es **Dirección** (en lugar de *Posta*). La receta sigue ofreciendo los medicamentos vigentes y con stock. | |
| 45 | **Reportes sin posta** | Abrir *Reportes*. Generar *Pacientes atendidos por día* (rango del mes), *Medicamentos con stock crítico* y *Movimientos de inventario*. Exportar a CSV. | *Pacientes atendidos por día*: columnas **Fecha / Pacientes atendidos / Atenciones**, una fila por día con atenciones (orden cronológico) y una fila **TOTAL**. Los otros dos reportes ya **no** tienen columna *Posta* (ni en pantalla ni en el CSV). Todos filtran por rango de fechas. | |
| 46 | **Respaldo de todas las historias del establecimiento** | *Respaldo* → *Qué respaldar*: **Todas las historias del establecimiento** → **Respaldar…**. Luego **Abrir respaldo…** con ese archivo. | La lista de pacientes queda **deshabilitada** (no hay nada que elegir) y el archivo sugerido se llama `Historias_Establecimiento_<fecha>.sigebak` (sin datos personales). Barra de progreso y botón Cancelar funcionan. Al abrirlo se lee *"…· Establecimiento: Posta Jesús Nazareno · N historia(s) · generado por …"* con todos los pacientes activos. | |
| 47 | **Tabla Posta vacía** | **Solo en una base de PRUEBAS:** con la tabla `Posta` vacía, abrir el programa. | El programa muestra un mensaje claro (*"No se encontró el establecimiento en la base de datos (la tabla Posta está vacía). Avise al administrador."*) y **no inicia**; no se muestra ningún texto técnico. Al restaurar la fila, inicia con normalidad. | |

```sql
-- 11) El IdPosta de pacientes y medicamentos recién registrados debe ser el de la Posta Jesús Nazareno.
SELECT IdPosta, Nombre FROM Posta;                                              -- debe haber UNA fila
SELECT TOP 5 IdPaciente, Nombres, Apellidos, Direccion, IdPosta FROM Paciente ORDER BY IdPaciente DESC;
SELECT TOP 5 IdMedicamento, Nombre, IdPosta FROM Medicamento ORDER BY IdMedicamento DESC;
```

---

## H. Detalle clínico: signos vitales, alergias y antecedentes

**Preparación:** entrar como `admin` o médico. Abrir la historia de un paciente de prueba.

| N.º | Caso | Pasos | Resultado esperado | Resultado |
|---|---|---|---|---|
| 48 | **Signos fuera de rango** | En *Nueva atención* escribir Temperatura 50, Saturación 120, Peso 0 y Guardar. | No se guarda; cada campo muestra su error en rojo (temperatura 30–45, saturación 50–100, peso 0.5–500). | |
| 49 | **Sistólica menor que diastólica** | Sistólica 80, diastólica 90. Guardar. | Error *"La sistólica debe ser mayor que la diastólica"*; no se guarda. | |
| 50 | **IMC calculado** | Peso 70, talla 170. | El campo IMC muestra `24.2 (normal)` al escribir, sin pulsar nada. Sin talla o peso queda vacío. | |
| 51 | **Colores de ayuda** | Temperatura 38 → ámbar; 39 → rojo; saturación 91 → ámbar; 89 → rojo. | Los cuadros cambian de color y el recuadro dice que **no es un diagnóstico**. Guardar sigue permitido. | |
| 52 | **Atención con signos** | Completar motivo, diagnóstico y signos válidos. Guardar. | Mensaje de éxito; la tabla muestra `PA 120/80 · T 36.5 °C · FC 72 lpm · …`. Atención sin signos muestra «—». Ejecutar consulta 12. | |
| 53 | **Alergia grave y aviso rojo** | Pestaña *Alergias*: Penicilina, gravedad Grave, Agregar. | Aparece en la tabla (fila roja) y un **aviso rojo** arriba: *"ALERGIA GRAVE: Penicilina"*. Al reabrir la historia sigue apareciendo. | |
| 54 | **Alergia repetida** | Agregar otra vez Penicilina. | Error *"Esa alergia ya está registrada y activa"*. | |
| 55 | **Desactivar alergia** | Seleccionar la fila → *Desactivar seleccionada* → Sí. | La fila queda gris *Desactivada*, el aviso rojo desaparece (si no hay otra grave) y la fila **sigue existiendo** en la base (consulta 13). | |
| 56 | **Receta con alergia: No** | Con la alergia activa, recetar un medicamento con ese nombre → Guardar → responder **No**. | Advertencia de posible alergia (botón *No* por defecto). No se guarda la atención ni baja el stock. | |
| 57 | **Receta con alergia: Sí** | Repetir y responder **Sí**. | Se guarda la atención con la receta y el stock baja (transacción normal). | |
| 58 | **Antecedentes cifrados** | Pestaña *Antecedentes*: escribir texto, Guardar; cerrar y reabrir. Ejecutar consulta 14. | El texto reaparece. En la base `AntecedentesCifrado` son bytes ilegibles (no texto). Más de 1000 caracteres se rechaza. | |
| 59 | **Permisos** | Iniciar como `farmacia1`. | No puede abrir la historia clínica (mensaje de acceso denegado). | |
| 60 | **Respaldo con signos** | *Respaldo* de un paciente con atenciones con signos y alergias → abrir el `.sigebak`. | La tabla de atenciones tiene la columna *Signos vitales*; el respaldo incluye alergias y antecedentes (solo visibles dentro del archivo cifrado). | |

```sql
-- 12) Signos guardados en las últimas atenciones.
SELECT TOP 5 IdAtencion, PesoKg, TallaCm, PresionSistolica, PresionDiastolica, Temperatura, FrecuenciaCardiaca, SaturacionO2
FROM Atencion ORDER BY IdAtencion DESC;
-- 13) Las alergias desactivadas no se borran.
SELECT IdAlergia, IdPaciente, Sustancia, Gravedad, Activo, Fecha FROM AlergiaPaciente ORDER BY IdAlergia DESC;
-- 14) Los antecedentes deben verse como bytes, no como texto.
SELECT TOP 5 IdHistoria, AntecedentesCifrado FROM HistoriaClinica WHERE AntecedentesCifrado IS NOT NULL;
-- 15) Rangos reales de los CHECK (deben coincidir con SignosVitales.cs).
SELECT name, definition FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('Atencion');
```

---

## I. Auditoría

**Preparación:** entrar como `admin`. Las consultas de la sección *Verificación en SQL* se ejecutan **a mano en SSMS** (preferiblemente en una base de pruebas).

| N.º | Caso | Pasos | Resultado esperado | Resultado |
|---|---|---|---|---|
| 61 | **Inicio de sesión correcto y fallido** | Iniciar sesión con una contraseña mala y luego con la correcta. Menú *Auditoría* → Buscar. | Dos filas: *Inicio de sesión fallido* (resultado `FALLO`, detalle *"Contraseña incorrecta (intento 1 de 5)"*) e *Inicio de sesión* (`OK`). **Ninguna** muestra la contraseña escrita. | |
| 62 | **Bloqueo** | Con un usuario de prueba, fallar 5 veces seguidas. | 5 filas de *Inicio de sesión fallido* y una de *Bloqueo de usuario*. Un sexto intento (ya bloqueado) queda como *"Cuenta bloqueada temporalmente"*. | |
| 63 | **Usuario inexistente** | Intentar entrar con un usuario que no existe. | Fila *Inicio de sesión fallido* con detalle *"Usuario inexistente"* y **sin nombre de usuario** (no se guarda lo escrito). | |
| 64 | **Cierre de sesión** | Cerrar sesión con el botón; en otra sesión, dejar 10 minutos inactivo. | Filas *Cierre de sesión* con detalle *"Cierre voluntario"* y *"Cierre por inactividad"*. Cerrar con *Salir* registra *"Salida del programa"*. | |
| 65 | **Usuarios** | Crear un usuario, cambiarle el rol, desactivarlo y restablecer su contraseña. | Filas *Creación de usuario* (detalle *"Rol: …"*), *Cambio de rol* (*"Rol: A -> B"*), *Cambio de estado de usuario* y *Restablecimiento de contraseña* — todas con `Usuario` + Id y **sin contraseñas**. | |
| 66 | **Pacientes y atenciones** | Registrar, editar y dar de baja un paciente; registrar una atención con y sin receta. | Filas *Alta/Edición/Baja de paciente* (*"Paciente Id 12"*), *Registro de atención* (*"Atención Id …, historia Id …"*) y, con receta, también *Movimiento de stock*. **Ninguna** contiene DNI, nombre, motivo ni diagnóstico. | |
| 67 | **Medicamentos y stock** | Registrar (con stock inicial), editar y dar de baja un medicamento; registrar un ingreso y una salida. | Filas *Alta/Edición/Baja de medicamento* y *Movimiento de stock* (*"Ingreso de 20 unidad(es)"*); el motivo escrito no se copia. | |
| 68 | **Respaldo y restauración** | Respaldar un paciente y el establecimiento; cancelar otro respaldo; abrir un `.sigebak` válido y uno alterado. | *Respaldo* `OK`, uno `CANCELADO`; *Restauración* `OK` (*"Verificación de respaldo (N historia(s))"*) y otra `FALLO`. Sin rutas ni datos de pacientes. | |
| 69 | **Acceso denegado** | Entrar como `farmacia1` e intentar abrir *Pacientes* (o como Médico, *Inventario*/*Usuarios*). | Fila *Acceso denegado* (`DENEGADO`) con *"Módulo: … · Rol: …"*. Aparece en rojo en la tabla. | |
| 70 | **Solo Administrador** | Entrar como `medico1` y `farmacia1`. | El menú *Auditoría* **no aparece**; además, si se intentara abrir la pantalla, `Permisos.ExigirAdministrador` la rechaza y queda auditado. | |
| 71 | **Filtros y paginación** | Filtrar por fecha, por usuario (*"med"*) y por acción; generar más de 50 registros. | Solo salen filas que cumplen **todos** los filtros; el paginador muestra *Página 1 de N* y *Siguiente ▶* funciona; la barra de estado muestra *"Consulta en X ms"*. *Desde* posterior a *Hasta* muestra un error en rojo. | |
| 72 | **Exportar a CSV** | *Exportar a CSV* con y sin filtros; abrir el archivo en Excel. | Columnas Fecha, Usuario, Acción, Entidad, Id entidad, Detalle, Resultado; tildes correctas. La exportación queda auditada (*Exportación de auditoría*). | |
| 73 | **La auditoría no rompe la operación** | **Solo en una base de PRUEBAS:** simular el fallo (p. ej. `DENY INSERT ON Auditoria TO <login>`), registrar un paciente. | El paciente **se guarda con normalidad** y no se ve ningún error; el detalle técnico queda en `%LocalAppData%\SIGEPOSTA\errores.log`. Después `REVOKE INSERT ON Auditoria FROM <login>` (o `GRANT`). | |
| 74 | **Auditoría inmutable** | Ejecutar la *Verificación en SQL* de abajo. | `UPDATE` y `DELETE` fallan con el mensaje del trigger; la tabla no cambia. | |

### Verificación en SQL: la auditoría no se puede editar ni borrar

Ejecutar en SSMS (**ninguna** de estas instrucciones modifica datos si el trigger funciona; se envuelven en una transacción que se revierte):

```sql
-- 16) El trigger existe, está activo y es de tipo INSTEAD OF o AFTER sobre UPDATE/DELETE.
SELECT t.name, t.is_disabled, t.is_instead_of_trigger,
       OBJECTPROPERTY(t.object_id, 'ExecIsUpdateTrigger') AS EsUpdate,
       OBJECTPROPERTY(t.object_id, 'ExecIsDeleteTrigger') AS EsDelete
FROM sys.triggers t WHERE t.parent_id = OBJECT_ID('Auditoria');           -- is_disabled debe ser 0

-- 17) Intentar modificar y borrar: ambas deben FALLAR con el mensaje del trigger.
BEGIN TRAN;
    UPDATE Auditoria SET Detalle = 'manipulado' WHERE IdAuditoria = (SELECT MIN(IdAuditoria) FROM Auditoria);
ROLLBACK;                                                                  -- esperado: error del trigger
BEGIN TRAN;
    DELETE FROM Auditoria WHERE IdAuditoria = (SELECT MIN(IdAuditoria) FROM Auditoria);
ROLLBACK;                                                                  -- esperado: error del trigger

-- 18) Comprobar que nada cambió.
SELECT TOP 3 IdAuditoria, Fecha, Accion, Detalle FROM Auditoria ORDER BY IdAuditoria;

-- 19) Ver el código del trigger (para explicarlo en la sustentación).
SELECT OBJECT_DEFINITION(t.object_id) FROM sys.triggers t WHERE t.parent_id = OBJECT_ID('Auditoria');
```

> **Límites que conviene saber:** un trigger **no se dispara con `TRUNCATE TABLE`**, y quien tenga permisos de dueño o administrador puede deshabilitarlo (`DISABLE TRIGGER`). Por eso la protección completa combina: (1) el trigger, (2) que la aplicación **no tiene código** de `UPDATE`/`DELETE` sobre `Auditoria`, y (3) permisos mínimos en la base (`DENY UPDATE, DELETE, ALTER` al inicio de sesión de la aplicación). La consulta 20 muestra los permisos efectivos.

```sql
-- 20) Permisos efectivos del usuario actual sobre la tabla (el INSERT sí, el resto no debería verse).
SELECT permission_name FROM fn_my_permissions('Auditoria', 'OBJECT');
```

---

## J. Proveedores de medicamentos

**Preparación:** tabla `Proveedor` creada en SQL Server (con `Ruc` único) y columnas `IdProveedor` y `NumeroDocumento` en `MovimientoInventario`. Entrar como `farmacia1` (o `admin`). RUC de ejemplo **válido**: `20100070970`. RUC con dígito verificador **malo**: `20100070971`.

| N.º | Caso | Pasos | Resultado esperado | Resultado |
|---|---|---|---|---|
| 75 | **Alta correcta** | Menú *Proveedores* → RUC `20100070970`, nombre *Droguería Perú SAC*, sin teléfono/correo/dirección → **Guardar**. | Mensaje *"Proveedor registrado."*, aparece en la lista, el formulario se limpia. Se puede repetir con teléfono (6–9 dígitos), correo válido y dirección. | |
| 76 | **RUC inválido: formato** | RUC de 10 dígitos (`2010007097`); luego uno de 11 con letra (la casilla solo deja escribir dígitos, pegar texto con letras debe rechazarse). | Error rojo bajo el RUC: *"El RUC debe tener exactamente 11 dígitos numéricos."* No se guarda nada. | |
| 77 | **RUC inválido: prefijo** | RUC `30100070970`. | *"El RUC debe empezar con 10, 15, 17 o 20."* | |
| 78 | **RUC inválido: dígito verificador** | RUC `20100070971`. | *"El RUC no es válido (el dígito verificador no coincide)."* | |
| 79 | **RUC duplicado** | Con `20100070970` ya registrado, intentar registrar otro proveedor con el mismo RUC. | Error bajo el RUC: *"Ya existe un proveedor con ese RUC"*. Tampoco permite editar otro proveedor para dejarle ese RUC. Un proveedor **dado de baja** también bloquea su RUC (la restricción es de la base). | |
| 80 | **Nombre y correo** | Nombre vacío; luego correo `ventas@` o `ventas empresa.com`. | *"El campo 'Nombre' es obligatorio."* / *"Ingrese un correo válido (ejemplo: ventas@empresa.com)."* El correo vacío **sí** se acepta. | |
| 81 | **Edición** | Elegir la fila, cambiar nombre y teléfono → **Guardar**. | *"Datos del proveedor actualizados."* La lista muestra los cambios. | |
| 82 | **Búsqueda y paginación** | Escribir el inicio del nombre (*"Drog"*), luego los primeros dígitos del RUC (*"2010"*). Con más de 50 proveedores, usar *Siguiente ▶*. | La lista se filtra tras ~300 ms de pausa; la barra de estado muestra *"Consulta en X ms"*; el paginador muestra *Página 1 de N*. | |
| 83 | **Baja de proveedor SIN movimientos** | Elegir un proveedor nuevo → **Eliminar** → *Sí*. | *"Proveedor dado de baja."*; desaparece de la lista; en SSMS la fila sigue con `Activo = 0` (no se borra). | |
| 84 | **Baja de proveedor CON movimientos** | Registrar un ingreso con ese proveedor (caso 86) y luego darlo de baja. | La confirmación avisa *"Este proveedor figura en N movimiento(s) de inventario…"*. Tras confirmar se da de baja, **ya no aparece** en la lista desplegable de nuevos ingresos, pero el historial de movimientos **sigue mostrando su nombre**. | |
| 85 | **Permisos** | Entrar como `medico1`. | El menú *Proveedores* **no aparece**; si se intentara usar `ProveedorServicio`, lanza *"Su rol no tiene permiso para acceder a Proveedores."* y queda en la auditoría como *Acceso denegado*. `farmacia1` y `admin` sí entran. | |
| 86 | **Ingreso con proveedor y documento** | *Inventario* → elegir un medicamento → Tipo *Ingreso*, cantidad 10, **Proveedor** de la lista y **N.º de guía o factura** `F001-000123` → **Registrar movimiento**. | *"Ingreso registrado."* El stock sube 10. En *Ver movimientos* la fila muestra el proveedor y el documento. | |
| 87 | **Ingreso SIN proveedor** | Tipo *Ingreso*, cantidad 5, dejar el proveedor sin elegir y escribir un documento. | Error bajo Proveedor: *"Seleccione el proveedor del ingreso."* El **stock no cambia** y no se crea movimiento. | |
| 88 | **Ingreso SIN documento** | Elegir proveedor, dejar el documento vacío (o de 31 caracteres). | *"Ingrese el número de guía o factura."* / *"El número de guía o factura no puede superar 30 caracteres."* No cambia el stock. | |
| 89 | **Salida no pide proveedor** | Cambiar el Tipo a *Salida*. | Los campos *Proveedor* y *N.º de guía o factura* quedan **deshabilitados y vacíos**; la salida se registra sin ellos. En el historial esas columnas quedan vacías. | |
| 90 | **Proveedor dado de baja durante el ingreso** | Abrir *Inventario* y dejar elegido un proveedor; en otra ventana darlo de baja; volver y registrar el ingreso. | *"El proveedor elegido no existe o está dado de baja."* y el stock **no cambia** (la validación va dentro de la misma transacción del ingreso). | |
| 91 | **Atomicidad** | Solo en base de PRUEBAS: provocar un fallo al insertar el movimiento (p. ej. `DENY INSERT ON MovimientoInventario`) y registrar un ingreso. | Mensaje amable de error y el **stock queda igual** (Rollback: stock y movimiento se guardan juntos o no se guardan). Luego `REVOKE`. | |
| 92 | **Auditoría de proveedores** | Registrar, editar y dar de baja un proveedor; registrar un ingreso. Menú *Auditoría*. | Filas *Alta/Edición/Baja de proveedor* con detalle *"Proveedor Id N"* y *Movimiento de stock* (*"Ingreso de 10 unidad(es), proveedor Id N"*). **Sin** RUC, nombre, teléfono, correo ni número de documento. | |

### Verificación en SQL (solo lectura)

```sql
-- 21) Proveedores y su estado (la baja es lógica: Activo = 0, la fila sigue existiendo).
SELECT IdProveedor, Nombre, Ruc, Activo FROM Proveedor ORDER BY IdProveedor;

-- 22) Ingresos con su proveedor y documento (los de "Stock inicial" y los anteriores a esta etapa pueden tener NULL).
SELECT TOP 20 mv.IdMovimiento, mv.Fecha, mv.Tipo, mv.Cantidad, p.Nombre AS Proveedor, mv.NumeroDocumento
FROM MovimientoInventario mv LEFT JOIN Proveedor p ON p.IdProveedor = mv.IdProveedor
ORDER BY mv.IdMovimiento DESC;

-- 23) Proveedores dados de baja que conservan movimientos (deben seguir apareciendo con su nombre).
SELECT p.IdProveedor, p.Nombre, COUNT(*) AS Movimientos
FROM Proveedor p JOIN MovimientoInventario mv ON mv.IdProveedor = p.IdProveedor
WHERE p.Activo = 0 GROUP BY p.IdProveedor, p.Nombre;
```

> **Tipos supuestos:** el programa asume `Nombre NVARCHAR(150)`, `Telefono NVARCHAR(20)`, `Correo NVARCHAR(100)`, `Direccion NVARCHAR(150)` y `NumeroDocumento NVARCHAR(30)`. Si en SQL Server son más cortos, ajustar las constantes de `Proveedor.cs` / `MovimientoInventario.cs`.

---

## K. Catálogo de diagnósticos CIE-10

**Preparación:** tabla `Cie10` con los 35 códigos cargados y columna `Atencion.Cie10Cifrado`. Entrar como `admin` (catálogo) y como `medico1` (atenciones). Ejemplo de código: `J06.9` = *Infección aguda de las vías respiratorias superiores*.

| N.º | Caso | Pasos | Resultado esperado | Resultado |
|---|---|---|---|---|
| 93 | **Búsqueda con tildes y mayúsculas** | Menú *CIE-10* → buscar `infeccion`, luego `INFECCIÓN`, luego `Infección`. | Las tres búsquedas devuelven **las mismas filas** (sin importar tildes ni mayúsculas). Con `vias respiratorias` salen los códigos que contienen ambas palabras en cualquier orden. | |
| 94 | **Búsqueda por código** | Buscar `j06`, `J06.9` y `j0`. | Salen los códigos que **empiezan** con ese texto, sin importar mayúsculas. | |
| 95 | **Paginación del catálogo** | Con más de 50 códigos, usar *Siguiente ▶*; la barra de estado muestra *"Consulta en X ms"*. | Páginas de 50, *Página 1 de N*. | |
| 96 | **Alta correcta** | Código `Z00.0`, descripción *Examen médico general* → **Guardar**. Escribirlo en minúsculas (`z00.0`) también. | *"Código CIE-10 registrado."* y se guarda en mayúsculas. | |
| 97 | **Formato de código inválido** | Probar `J6`, `JJ06`, `06.9`, `J06.`, `J06.999`, `J06,9`. | Error rojo bajo el código: *"El código CIE-10 debe tener una letra, dos dígitos y, si lleva, un punto con 1 o 2 caracteres (ejemplo: J06.9)."* Se aceptan `J06`, `J06.9` y `S72.00`. | |
| 98 | **Código duplicado** | Registrar `J06.9` otra vez (o `j06.9`). Repetir con un código dado de baja. | *"Ese código CIE-10 ya existe en el catálogo."* / *"…ya existe en el catálogo pero está dado de baja."* No se crea nada. | |
| 99 | **Editar y dar de baja** | Elegir una fila: el código queda bloqueado; cambiar la descripción → **Guardar**. Luego **Eliminar** → *Sí*. | *"Descripción actualizada."* / *"Código dado de baja."* En SSMS la fila sigue con `Activo = 0`. Ya no sale en las sugerencias, pero las atenciones antiguas que lo usaron lo siguen mostrando. | |
| 100 | **Solo Administrador** | Entrar como `medico1` y `farmacia1`. | El menú *CIE-10* **no aparece**; `Cie10Servicio` rechaza el mantenimiento con *"Su rol no tiene permiso para acceder a Administración."* y queda auditado como *Acceso denegado*. | |
| 101 | **Autocompletado** | *Historia clínica* → *Nueva atención* → en *Diagnóstico CIE-10* escribir `infec`. | Tras ~300 ms aparece una lista de hasta 15 sugerencias como *"J06.9 - Infección aguda de las vías respiratorias superiores"*; la ventana **no se congela** al escribir. ↓ ↑ Enter o clic eligen; Esc cierra. | |
| 102 | **Atención SIN código** | Llenar motivo y detalle pero dejar el CIE-10 vacío → **Guardar atención**. | Error rojo bajo el CIE-10: *"El código CIE-10 es obligatorio."* No se guarda la atención. | |
| 103 | **Texto sin elegir** | Escribir `infec` y pulsar **Guardar atención** sin elegir ninguna sugerencia (o elegir una y luego editar el texto). | *"Elija un diagnóstico de la lista de sugerencias."* (nunca se guarda un código distinto del que se ve). | |
| 104 | **Atención con código** | Elegir `J06.9`, escribir el detalle, guardar. Ver la pestaña *Atenciones*. | La columna *Diagnóstico CIE-10* muestra *"J06.9 - Infección aguda…"* y *Detalle del diagnóstico* el texto libre. | |
| 105 | **El código va cifrado** | Consulta SQL 24 (abajo). | `Cie10Cifrado` son bytes (≥ 32) y **no** contienen el texto `J06.9`; dos atenciones con el mismo código dan bytes **distintos** (IV aleatorio). | |
| 106 | **No se escribe en auditoría ni en errores** | Guardar atenciones con CIE-10; abrir *Auditoría* y revisar `%LocalAppData%\SIGEPOSTA\errores.log`. | La fila *Registro de atención* solo dice *"Atención Id …, historia Id …"*; ninguna fila ni el log contienen un código CIE-10. | |
| 107 | **Código ilegible en la base** | **Solo en base de PRUEBAS:** consulta SQL 25 (abajo) para dañar los bytes de UNA atención de prueba; abrir su historia. | La lista **carga normalmente**; esa atención muestra *"[Código CIE-10 ilegible]"*. No aparece mensaje técnico y no se escribe nada en `errores.log`. El respaldo también muestra *"[Código CIE-10 ilegible]"*. | |
| 108 | **Atención antigua sin código** | Abrir la historia de una atención anterior a esta etapa (`Cie10Cifrado` NULL). | Muestra *"Sin código CIE-10"*; el resto se ve igual. | |
| 109 | **Código dado de baja en una atención antigua** | Dar de baja un código usado por una atención y reabrir la historia. | La atención sigue mostrando *"Código - descripción"*. En una atención **nueva** ese código ya no se puede elegir (y si se forzara, el servicio responde *"…no existe en el catálogo o está dado de baja."*). | |
| 110 | **Respaldo** | *Respaldo* → respaldar un paciente con atenciones con CIE-10 → *Abrir respaldo*. | La tabla de atenciones tiene la columna *CIE-10* con *"Código - descripción"* (y vacía en atenciones antiguas). El `.sigebak` sigue cifrado (no se lee el XML a simple vista). | |
| 111 | **Reporte: diagnósticos más frecuentes** | *Reportes* → *Diagnósticos más frecuentes (CIE-10)* → rango de fechas → **Generar** → **Exportar a CSV**. | Tabla N.º, Código, Descripción, Atenciones y %, ordenada de mayor a menor. El resumen indica cuántas atenciones no tienen código y cuántas son ilegibles. El CSV se abre en Excel con tildes correctas. | |
| 112 | **Reporte: permisos** | Entrar como `farmacia1`. | La opción **no** aparece (solo Administrador y Médico); `ReporteServicio` la rechaza igualmente. | |

### Verificación en SQL (solo lectura, y una prueba destructiva SOLO en base de pruebas)

```sql
-- 24) El código está cifrado: son bytes, nunca texto legible. (VARBINARY(128): el dato mide IV 16 + bloque 16 = 32 bytes.)
SELECT TOP 5 a.IdAtencion, DATALENGTH(a.Cie10Cifrado) AS Bytes, a.Cie10Cifrado FROM Atencion a WHERE a.Cie10Cifrado IS NOT NULL ORDER BY a.IdAtencion DESC;

-- 25) SOLO BASE DE PRUEBAS: dañar los bytes de UNA atención para probar el "código ilegible".
BEGIN TRAN;
    UPDATE Atencion SET Cie10Cifrado = 0x00112233445566778899AABBCCDDEEFF00112233445566778899AABBCCDDEEFF WHERE IdAtencion = <Id de una atención de prueba>;
-- Probar el programa y luego:  ROLLBACK;   (o COMMIT si se desea dejarla así)

-- 26) El catálogo: activos y dados de baja (la baja es lógica).
SELECT Codigo, Descripcion, Activo FROM Cie10 ORDER BY Codigo;
```

> **Por qué el reporte no usa `GROUP BY`:** como el código está cifrado con IV aleatorio, SQL no puede contar cuántas atenciones tienen el mismo diagnóstico; el programa los descifra y cuenta en memoria. En una base con cientos de miles de atenciones convendría limitar el rango de fechas.

---
## L. Forma farmacéutica, unidad de medida y concentración

**Preparación:** tablas `FormaFarmaceutica` (14 formas) y `UnidadMedida` (9 unidades), ambas con "Sin definir", y las columnas `IdForma`, `IdUnidad` y `Concentracion` en `Medicamento`. Entrar como `farmacia1` o `admin`. Los medicamentos creados antes de esta etapa están en "Sin definir".

| N.º | Caso | Pasos | Resultado esperado | Resultado |
|---|---|---|---|---|
| 113 | **Alta de forma** | Menú *Formas y unidades* → *Formas farmacéuticas* → Nombre `Supositorio` → **Guardar**. | *"Forma farmacéutica registrada."*; aparece en la lista con **0** medicamentos que la usan. Repetir en *Unidades de medida*. | |
| 114 | **Nombre obligatorio** | Dejar el nombre vacío → **Guardar**. | Error rojo: *"El campo 'Nombre' es obligatorio."* | |
| 115 | **Forma duplicada** | Registrar otra vez `Tableta` (o `tableta`). Repetir en unidades. | *"Ya existe una forma farmacéutica con ese nombre."* / *"Ya existe una unidad de medida con ese nombre."* No se crea nada (también rechaza el nombre de una forma dada de baja). | |
| 116 | **Editar** | Elegir una fila, cambiar el nombre → **Guardar**. | *"Nombre actualizado."* Cambiar a un nombre ya existente muestra el error de duplicado. | |
| 117 | **"Sin definir" protegido** | Elegir la fila *Sin definir*. | Nombre, **Guardar** y **Eliminar** quedan deshabilitados y un aviso rojo explica que no se puede modificar ni dar de baja. | |
| 118 | **Baja SIN medicamentos** | Elegir `Supositorio` → **Eliminar** → *Sí*. | Pregunta simple; *"Dado de baja."*; desaparece de la lista. En SSMS la fila sigue con `Activo = 0`. | |
| 119 | **Baja CON medicamentos** | Elegir `Tableta` (la usan varios medicamentos) → **Eliminar**. | El aviso dice *"Esta forma farmacéutica la usan N medicamento(s) activo(s)…"*. Si confirma, se da de baja; los medicamentos **conservan** su forma, pero ya no se ofrece en medicamentos nuevos ni al editar. | |
| 120 | **Lista con conteo** | Mirar la columna *Medicamentos que lo usan* y comparar con la consulta SQL 27. | Los números coinciden con los medicamentos **activos** de cada forma / unidad. | |
| 121 | **Permisos** | Entrar como `medico1`. | El menú *Formas y unidades* **no aparece**; `CatalogoServicio` rechaza cualquier operación con *"Su rol no tiene permiso para acceder a Formas y unidades."* (queda auditado). `farmacia1` y `admin` sí entran. | |
| 122 | **Alta de medicamento nuevo** | *Inventario* → Nombre `Paracetamol`, Concentración `500 mg`, Forma `Tableta`, Unidad `Tableta`, stock mínimo 10, vencimiento, stock inicial 10 → **Guardar**. | Se guarda. En la tabla aparece **"Paracetamol 500 mg - Tableta"** y el stock **"10 tabletas"**. | |
| 123 | **Forma y unidad obligatorias** | Intentar guardar sin elegir forma, sin unidad. | Errores rojos bajo cada lista: *"Seleccione la forma farmacéutica."* / *"Seleccione la unidad."* No se guarda. | |
| 124 | **Concentración máxima** | Escribir 41 caracteres en *Concentración*. | *"La concentración no puede superar 40 caracteres."* (el cuadro además limita a 40). Vacía es válida. | |
| 125 | **Medicamento antiguo "Sin definir"** | Elegir un medicamento creado antes de esta etapa. | La tabla lo muestra como antes (*nombre + presentación*, p. ej. "Paracetamol 500 mg tabletas") y el stock **sin unidad** ("10"). Al elegirlo, *Forma* y *Unidad* aparecen **sin elegir**; **Guardar** sin elegirlas da los errores del caso 123; al elegirlas y guardar, pasa al formato nuevo. | |
| 126 | **Plural y singular del stock** | Dejar un medicamento con unidad `Tableta` en stock **1**, luego **2**, luego **0**. Probar con `Frasco`, `Inhalador` y `Unidad`. | **"1 tableta"**, **"2 tabletas"**, **"0 tabletas"**; **"2 frascos"**, **"2 inhaladores"**, **"2 unidades"**. Una unidad abreviada como `mL` no cambia (**"2 mL"**). | |
| 127 | **Inactivas no se ofrecen** | Dar de baja una forma (caso 119) y abrir *Inventario*. | Esa forma ya **no sale** en la lista *Forma farmacéutica* del medicamento, pero el filtro de la lista tampoco la muestra. *Sin definir* no sale en el medicamento pero **sí** en el filtro. | |
| 128 | **Filtro por forma** | En *Inventario* elegir una forma en el filtro junto a *Solo críticos*; combinarlo con la búsqueda y *Solo críticos*. | Solo salen medicamentos de esa forma; la paginación y *"Consulta en X ms"* se actualizan. *(Todas las formas)* vuelve a mostrar todo. Elegir *Sin definir* lista los antiguos. | |
| 129 | **Ver movimientos** | *Ver movimientos* de un medicamento. | El título muestra el nombre completo y el stock con unidad; la columna *Cantidad* muestra "10 tabletas" / "1 tableta". | |
| 130 | **Receta** | *Pacientes* → historia → *Nueva atención* → abrir la lista *Medicamento*. | Cada opción dice *"Paracetamol 500 mg - Tableta — stock 10 tabletas"*. Al guardar, la receta de la lista de atenciones dice *"Paracetamol 500 mg - Tableta x 2 tabletas"*. | |
| 131 | **Reportes y CSV** | *Reportes* → *Medicamentos con stock crítico* y *Movimientos de inventario*; **Exportar a CSV**. | Medicamento con nombre completo, stock/cantidad con unidad. El CSV lo conserva (abrirlo en Excel). | |
| 132 | **Panel de inicio** | Entrar como `admin`/`farmacia1` con medicamentos críticos. | La tabla de alertas muestra el nombre completo y el stock con unidad. | |
| 133 | **Respaldo** | *Respaldo* → respaldar un paciente con receta → *Abrir respaldo*. | La columna *Receta* muestra el nombre completo y la cantidad con unidad. | |
| 134 | **Auditoría** | Registrar, editar y dar de baja una forma y una unidad; abrir *Auditoría*. | Filas *Alta/Edición/Baja de forma farmacéutica* y *… de unidad de medida* con detalle *"Elemento Id N"* (sin más datos). | |

### Verificación en SQL (solo lectura)

```sql
-- 27) Medicamentos activos por forma (debe coincidir con la columna de la pantalla).
SELECT f.IdForma, f.Nombre, f.Activo, COUNT(m.IdMedicamento) AS Medicamentos
FROM FormaFarmaceutica f LEFT JOIN Medicamento m ON m.IdForma = f.IdForma AND m.Activo = 1
GROUP BY f.IdForma, f.Nombre, f.Activo ORDER BY f.Nombre;

-- 28) Lo mismo por unidad.
SELECT u.IdUnidad, u.Nombre, u.Activo, COUNT(m.IdMedicamento) AS Medicamentos
FROM UnidadMedida u LEFT JOIN Medicamento m ON m.IdUnidad = u.IdUnidad AND m.Activo = 1
GROUP BY u.IdUnidad, u.Nombre, u.Activo ORDER BY u.Nombre;

-- 29) Medicamentos que siguen "Sin definir" (antiguos o con forma/unidad NULL).
SELECT m.IdMedicamento, m.Nombre, m.Presentacion, f.Nombre AS Forma, u.Nombre AS Unidad
FROM Medicamento m LEFT JOIN FormaFarmaceutica f ON f.IdForma = m.IdForma LEFT JOIN UnidadMedida u ON u.IdUnidad = m.IdUnidad
WHERE m.Activo = 1 AND (f.Nombre IS NULL OR f.Nombre = N'Sin definir' OR u.Nombre IS NULL OR u.Nombre = N'Sin definir');
```

> **Supuesto:** el programa asume `Nombre NVARCHAR(60)` en las dos tablas de catálogo; si es más corto en SQL Server, ajustar `ItemCatalogo.MaxNombre`.

---
## F. Verificaciones ya realizadas durante el desarrollo (sin base de datos)

Estas comprobaciones se hicieron cargando el programa compilado y ejercitando solo la lógica en memoria; **no sustituyen** las pruebas de la sección A, que necesitan la base de datos:

- Respaldo `.sigebak`: ida y vuelta de 200 historias; el archivo no contiene DNI, diagnósticos ni XML legibles; archivo alterado, truncado o ajeno rechazado con el mensaje correspondiente.
- Cifrado: DNI y diagnóstico salen como bytes; descifran correctamente; el mismo DNI cifrado dos veces da bytes distintos; el HMAC mide 64 caracteres y es estable.
- Cierre por inactividad: se dispara a los límites esperados, se reinicia con actividad y se pausa durante un respaldo.
- Permisos por rol (Farmacia no entra a Pacientes/Respaldo/Usuarios; Médico no entra a Inventario/Usuarios), reglas de usuarios (no desactivarse, no cambiarse el rol) y política de contraseña.
- Entidad `Medicamento`: no permite retirar más del stock ni asignar stock negativo.
- Receta (etapa 7): las validaciones previas (`ValidarReceta`: cantidad 0, negativa, sin medicamento, sin cantidad, exceso de 1 000 000) y los permisos (`PuedeRecetar`: Médico y Administrador sí, Farmacia no; Farmacia no puede ni abrir la historia) responden como se espera. **La transacción contra SQL Server (casos 21 a 24) todavía no se ha ejecutado**: depende de la base de datos.
- Dashboard (etapa 8): con un doble de la capa de datos se comprobó que cada rol ejecuta **solo sus consultas** (Administrador 5, Médico 3, Farmacia 2), que un rol sin permiso lanza `AccesoDenegadoException` **sin ejecutar ninguna consulta**, que un bloque que falla no tumba al otro y no expone el texto técnico, y que el gráfico de 7 días se completa con ceros. También se revisó el aspecto de cada rol y de los estados de error y vacío. **Las consultas reales contra SQL Server (casos 27 a 38) todavía no se han ejecutado.**
- Establecimiento único (etapa 9): con un doble de la capa de datos se comprobó que `Establecimiento` consulta la tabla `Posta` **una sola vez** (7 usos, 1 consulta), que no se queda inicializado si la tabla está vacía (se reintenta y avisa con un mensaje claro), que `Validar` de pacientes e inventario ya no pide posta, que la dirección se valida (máximo 150), que el dashboard ejecuta las mismas consultas por rol con **una sola** consulta `GROUP BY` por día para los dos gráficos (13 puntos del mes = día 13) y que el reporte por día agrupa y totaliza bien. También se revisó el aspecto de Pacientes, Inventario, Login, ventana principal y Respaldo. **Los casos 39 a 47 contra la base real todavía no se han ejecutado.**
- Compilación en **Debug y Release**: 0 errores y 0 advertencias.
