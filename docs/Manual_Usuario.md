# Manual de usuario — SIGEPOSTA

SIGEPOSTA permite registrar **pacientes con su historia clínica** y controlar el **inventario de medicamentos** de la **Posta Jesús Nazareno**. El nombre del establecimiento aparece en el inicio de sesión, en la barra superior y en el panel de inicio.
Cada persona entra con su usuario y ve solo lo que su **rol** permite:

| Rol | Puede usar |
|---|---|
| **Administrador** | Todo: Pacientes, Inventario, Reportes, Respaldo y Usuarios |
| **Médico** | Pacientes, Reportes de pacientes y Respaldo |
| **Farmacia** | Inventario y Reportes de inventario |

---

## 1. Iniciar sesión y crear el administrador (primera vez)

**Primera vez que se abre el programa:** como todavía no hay usuarios, aparece la ventana **Administrador inicial**.

[CAPTURA: creación del administrador inicial]

1. Escriba el **usuario**, el **nombre completo** y la **contraseña** (dos veces).
2. Pulse **Crear administrador**. Luego inicie sesión con ese usuario.

> **Contraseña válida:** mínimo 8 caracteres, con una mayúscula, una minúscula y un número (ej.: `Posta2026`).

**Las demás veces** aparece la ventana de **inicio de sesión**:

[CAPTURA: ventana de login]

1. Escriba su usuario y contraseña y pulse **Ingresar** (o la tecla Enter).
2. Si se equivoca, el mensaje siempre es *"Usuario o contraseña incorrectos"*. **Tras 5 intentos fallidos la cuenta se bloquea 5 minutos.**

[CAPTURA: menú principal]

**Panel de inicio:** al entrar se abre un resumen con su saludo y la fecha, **tarjetas** con cifras, **gráficos** (atenciones por día del mes y de los últimos 7 días) y las **alertas de inventario**. Lo que ve depende de su rol: el Administrador ve todo; el Médico, pacientes, atenciones y gráficos; Farmacia, solo el inventario. Las tarjetas de medicamentos se ponen **ámbar** (stock bajo o por vencer) o **rojas** (vencidos) cuando el número es mayor que cero. Se actualiza solo cada minuto, o con el botón **Actualizar**; al hacer clic en una alerta se abre **Inventario**.

[CAPTURA: panel de inicio (dashboard)]

Para salir de su cuenta use el botón **Cerrar sesión** (arriba a la derecha). Por seguridad, **la sesión se cierra sola tras 10 minutos sin actividad**.

---

## 2. Registrar un paciente y una atención

Menú **Pacientes**.

[CAPTURA: registro de paciente]

**Registrar un paciente**
1. En *Datos personales* complete: **DNI** (8 dígitos), **Nombres**, **Apellidos**, **Fecha de nacimiento**, **Teléfono** (opcional, 6 a 9 dígitos) y **Dirección** (opcional). El paciente se registra automáticamente en la Posta Jesús Nazareno.
2. Pulse **Guardar**. El sistema crea al paciente **y su historia clínica** (número `HC-000001`, `HC-000002`…) y lo informa.
3. Los errores aparecen **en rojo** debajo del campo (por ejemplo, un DNI de 5 dígitos o un DNI ya registrado).

**Editar o dar de baja:** haga clic en una fila de la *Lista de pacientes*; sus datos se cargan arriba (el DNI no se puede cambiar). Cambie lo necesario y pulse **Guardar**. **Eliminar** da de baja al paciente (deja de aparecer en la lista, pero su historia se conserva). **Limpiar** vacía el formulario. El cuadro **Buscar** filtra por nombre o apellido.

**Registrar una atención:** elija al paciente y pulse **Ver historia clínica** (o haga doble clic en la fila).

[CAPTURA: historia clínica con nueva atención]

Escriba el **Motivo de consulta**, elija el **Diagnóstico CIE-10** y, si desea, un **Detalle del diagnóstico** (texto libre); luego pulse **Guardar atención**. El código CIE-10 y el detalle se guardan **cifrados** y la atención queda con su fecha y el usuario que la registró.

**Diagnóstico CIE-10 (obligatorio en las atenciones nuevas):** escriba el código (*J06.9*) o parte del nombre (*infección respiratoria*; no importan tildes ni mayúsculas). Tras una breve pausa aparecen hasta 15 sugerencias como *"J06.9 - Infección aguda de las vías respiratorias superiores"*. Elíjala con el ratón, o con **↓ ↑** y **Enter** (**Esc** cierra la lista). Si escribe algo y no elige ninguna sugerencia, el sistema lo pide antes de guardar. En la pestaña *Atenciones* verá la columna **Diagnóstico CIE-10** con *"Código - descripción"* (las atenciones antiguas muestran *"Sin código CIE-10"*).

**Recetar (opcional):** en *Nueva atención* elija un **Medicamento (opcional)** y escriba la **Cantidad**. La lista muestra solo los medicamentos **vigentes y con stock**, junto con su stock disponible. Al guardar, el sistema **descuenta el stock automáticamente** y registra la salida *"Receta HC-…"* en el inventario; en la tabla de atenciones aparece en la columna **Receta** (ej.: *Paracetamol x 10*). Si la cantidad supera el stock, aparece *"Stock insuficiente para recetar"* y **no se guarda nada** (ni la atención). Deje *"(Sin receta)"* para una atención sin medicamento. El Médico puede recetar aunque no tenga acceso a la pantalla de Inventario.

[CAPTURA: nueva atención con receta]

**Pestañas de la historia clínica** (solo Administrador y Médico): *Atenciones*, *Nueva atención*, *Alergias* y *Antecedentes*.

- **Signos vitales** (en *Nueva atención*, todos opcionales): peso (kg), talla (cm), temperatura (°C), presión sistólica/diastólica (mmHg), frecuencia cardíaca (lpm) y saturación de O₂ (%). El **IMC** se calcula solo. Un valor fuera de rango (ej. temperatura 50) se rechaza con un mensaje en rojo bajo el campo. Los cuadros se pintan de **ámbar** o **rojo** cuando el valor se aleja de lo habitual: es solo una **ayuda visual, no un diagnóstico**. Los signos aparecen en la columna *Signos vitales* de la tabla de atenciones.
- **Alergias:** escriba la **Sustancia**, elija la **Gravedad** (Leve, Moderada o Grave) y pulse **Agregar alergia**. Para quitar una, selecciónela y pulse **Desactivar seleccionada** (no se borra, queda en el historial). Si el paciente tiene alguna alergia **grave activa**, aparece un **aviso rojo** en la parte superior de la historia.
- **Aviso al recetar:** si el medicamento coincide con una alergia activa, el sistema avisa y pide confirmación (*Sí / No*, por defecto *No*). Si elige *No*, no se guarda nada.
- **Antecedentes:** texto libre (máx. 1000 caracteres) que se guarda **cifrado**; pulse **Guardar antecedentes**.

[CAPTURA: pestaña Signos vitales con valores en ámbar y aviso de alergia grave]

---

## 3. Inventario de medicamentos

Menú **Inventario**.

[CAPTURA: inventario con alertas]

**Registrar un medicamento:** complete **Nombre**, **Concentración** (opcional, ej. *500 mg*, máx. 40 caracteres), **Forma farmacéutica** y **Unidad** (listas obligatorias), **Stock mínimo**, **Fecha de vencimiento** (marque la casilla para activarla; es obligatoria) y **Stock inicial**; pulse **Guardar**. Para cambiar datos, elija la fila, edite y pulse **Guardar** (el stock solo cambia con movimientos).

**Ingreso y salida de stock:** elija el medicamento en la tabla, vaya a *Movimiento de stock*, seleccione **Tipo** (*Ingreso* o *Salida*), escriba la **Cantidad** (mayor que cero) y, si desea, el **Motivo**. Si es un **Ingreso**, también debe elegir el **Proveedor** (la lista muestra solo proveedores activos) y escribir el **N.º de guía o factura** (hasta 30 caracteres); en una **Salida** esos dos campos se desactivan. Pulse **Registrar movimiento**.

[CAPTURA: movimiento de stock]

Si una salida es mayor al stock disponible, el sistema la **rechaza** con el aviso *"Stock insuficiente"* y no cambia nada: el stock nunca queda en negativo.

**Alertas (colores de la tabla):**

| Color | Significa |
|---|---|
| **Ámbar** | **Stock bajo** (stock ≤ mínimo) o **por vencer** (vence en 30 días o menos) |
| **Rojo** | **Vencido** |

La tabla muestra el nombre completo (*"Paracetamol 500 mg - Tableta"*) y el stock con su unidad (*"10 tabletas"*, *"1 tableta"*). Use el filtro **Forma farmacéutica** (junto a *Solo críticos*) para ver solo una forma; *Sin definir* muestra los medicamentos antiguos. Los medicamentos creados antes de esta versión aparecen sin forma ni unidad: al editarlos, elija su **Forma farmacéutica** y **Unidad** para poder guardarlos.

Marque **Solo críticos** para ver únicamente los medicamentos con alerta. **Ver movimientos** (o doble clic) muestra el historial de ingresos y salidas del medicamento; las salidas por receta aparecen con el motivo **"Receta HC-xxxxxx"**. Los ingresos muestran además el **proveedor** y la **guía / factura**.

---

## 4. Respaldo y restauración

Menú **Respaldo** (Administrador y Médico).

[CAPTURA: respaldo en progreso]

**Hacer un respaldo**
1. En *Crear respaldo* elija **Un paciente** (y luego el paciente) o **Todas las historias del establecimiento** (no hay nada más que elegir).
2. Pulse **Respaldar…**, elija dónde guardar el archivo (`.sigebak`) y confirme.
3. Una **barra de progreso** muestra el avance; puede seguir usando la ventana y pulsar **Cancelar**.

El archivo está **comprimido y cifrado**: no se puede leer con otro programa.

**Restaurar / verificar:** en *Verificar o restaurar un respaldo* pulse **Abrir respaldo…** y elija un `.sigebak`. El sistema comprueba que no esté dañado y **muestra su contenido** (pacientes y atenciones).

[CAPTURA: verificación de un respaldo]

> Si el archivo está dañado, fue modificado o se creó con otra llave, aparece un mensaje claro y no se muestra nada. **Importante:** un respaldo solo se abre con **el mismo usuario de Windows en el mismo equipo** donde se creó. Esta opción **no modifica la base de datos**.

---

## 5. Reportes

Menú **Reportes**. Solo aparecen los reportes de su rol.

[CAPTURA: reportes]

1. Elija el **Reporte**: *Pacientes atendidos por día*, *Diagnósticos más frecuentes (CIE-10)* (Administrador y Médico), *Medicamentos con stock crítico o por vencer* o *Movimientos de inventario por fecha*.
2. Si el reporte usa fechas, indique **Desde** y **Hasta** (ambas incluidas).
3. Pulse **Generar**. Para guardarlo, pulse **Exportar a CSV** (se abre con Excel).

---

## 6. Gestión de usuarios (solo Administrador)

Menú **Usuarios**.

[CAPTURA: gestión de usuarios]

- **Crear un usuario:** con la tabla sin selección, complete **Usuario**, **Nombre completo**, **Rol** y la **Contraseña** (dos veces) y pulse **Guardar**.
- **Cambiar rol o restablecer contraseña:** elija al usuario en la tabla, cambie el **Rol** y/o escriba una **contraseña nueva** (dos veces) y pulse **Guardar**.
- **Desactivar / Activar:** elija al usuario y pulse el botón (queda gris en la tabla y no puede iniciar sesión).

Reglas de seguridad: **no puede desactivar su propia cuenta ni cambiarse su propio rol**, y el sistema **siempre debe tener al menos un administrador activo**.

---

## 7. Auditoría (solo Administrador)

Menú **Auditoría**. Muestra, de la más reciente a la más antigua, *quién hizo qué y cuándo*: inicios y cierres de sesión, cambios de usuarios, altas/bajas de pacientes y medicamentos, atenciones, movimientos de stock, respaldos y accesos denegados.

[CAPTURA: pantalla de auditoría con filtros]

1. Use los filtros: **Desde** / **Hasta** (marque la casilla para activarlos), **Usuario** (inicio del nombre) y **Acción**. Pulse **Buscar** (o Enter).
2. Navegue con **◀ Anterior** y **Siguiente ▶** (50 registros por página). Los registros con resultado distinto de `OK` (fallos, denegados, cancelados) se ven en rojo.
3. **Exportar a CSV** guarda los registros que cumplen los filtros (hasta 50 000).

La auditoría es **solo de lectura**: no se puede editar ni borrar. Por privacidad no contiene DNI, nombres, diagnósticos ni contraseñas, solo identificadores (ej. *"Paciente Id 12"*).

---

## 8. Proveedores (Administrador y Farmacia)

Menú **Proveedores**. Aquí se registran las empresas que abastecen medicamentos; luego se eligen al registrar un **ingreso** de stock.

[CAPTURA: pantalla de proveedores]

1. Complete **RUC** (11 dígitos), **Nombre o razón social** y, si desea, **Teléfono** (6 a 9 dígitos), **Correo** y **Dirección**; pulse **Guardar**.
2. El RUC debe empezar con **10, 15, 17 o 20** y tener un dígito verificador correcto; si no, el programa lo indica bajo el campo. Si el RUC ya está registrado, muestra *"Ya existe un proveedor con ese RUC"*.
3. Para cambiar datos, elija la fila de la lista, edite y pulse **Guardar**. **Limpiar** vacía el formulario.
4. **Buscar:** escriba el inicio del nombre o del RUC; la lista se actualiza sola y se divide en páginas de 50.
5. **Eliminar** da de **baja** al proveedor: deja de ofrecerse en nuevos ingresos, pero sus ingresos anteriores se conservan y siguen mostrando su nombre. No se borra nunca. Un proveedor dado de baja sigue ocupando su RUC (no se puede registrar de nuevo con ese número).

El Médico no ve este menú.

---

## 9. Catálogo CIE-10 (solo Administrador)

Menú **CIE-10**. Es la lista de diagnósticos que el Médico elige al registrar una atención.

[CAPTURA: catálogo CIE-10]

1. **Código** (una letra, dos dígitos y, si lleva, un punto con 1 o 2 caracteres: *J06*, *J06.9*, *S72.00*) y **Descripción**; pulse **Guardar**. El código se guarda en mayúsculas. Si ya existe, el sistema lo indica.
2. **Buscar:** escriba un código (*j06*) o parte del nombre (*infeccion*); no importan tildes ni mayúsculas. La lista se divide en páginas de 50.
3. Para cambiar la descripción, elija la fila y pulse **Guardar** (el código no se puede cambiar).
4. **Eliminar** da de **baja** el código: deja de ofrecerse en nuevas atenciones, pero las atenciones antiguas lo siguen mostrando. No se borra nunca. Un código dado de baja no se puede volver a registrar desde el programa.

---
## 10. Formas farmacéuticas y unidades de medida (Administrador y Farmacia)

Menú **Formas y unidades**. Son las listas que se eligen al registrar un medicamento (*Tableta*, *Cápsula*, *Jarabe*…, y *Tableta*, *Frasco*, *mL*…).

[CAPTURA: formas y unidades]

1. Arriba, elija qué desea administrar: **Formas farmacéuticas** o **Unidades de medida**; la lista de abajo cambia.
2. Escriba el **Nombre** y pulse **Guardar**. Si ya existe, el sistema lo indica.
3. Para cambiar un nombre, elija la fila, edítelo y pulse **Guardar**.
4. La columna **Medicamentos que lo usan** indica cuántos medicamentos activos tienen ese valor.
5. **Eliminar** da de **baja**: deja de ofrecerse en medicamentos nuevos. Si hay medicamentos que lo usan, el sistema avisa cuántos antes de confirmar; ellos conservan su valor. No se borra nunca.
6. **Sin definir** es el valor de los medicamentos antiguos: se ve en la lista, pero no se puede modificar ni dar de baja.

---
## Ayuda rápida

| Situación | Qué hacer |
|---|---|
| *"No se pudo conectar con la base de datos"* | Verifique que SQL Server esté en ejecución y avise al administrador. |
| La sesión se cerró sola | Se cumplieron 10 minutos sin actividad: inicie sesión de nuevo. |
| Mensaje con un **código** de 8 caracteres | Ocurrió un error inesperado; anote el código y avise al administrador (queda registrado en `%LocalAppData%\SIGEPOSTA\errores.log`). |
| Tecla **Enter** / **Esc** | Enter ejecuta la acción principal de la ventana; Esc la cierra. |
