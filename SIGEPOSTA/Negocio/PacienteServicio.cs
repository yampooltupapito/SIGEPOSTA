using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using SIGEPOSTA.Datos;
using SIGEPOSTA.Seguridad;

namespace SIGEPOSTA.Negocio
{
    /// <summary>
    /// Reglas de negocio de pacientes e historias clínicas.
    /// Decisiones de diseño:
    ///  - CADA método público vuelve a validar el rol (Permisos.ExigirAccesoPacientes): ocultar botones
    ///    en la pantalla no es seguridad.
    ///  - El DNI se cifra (AES) y se indexa con HMAC; el DNI en claro solo se obtiene con DescifrarDni.
    ///  - Paciente + historia clínica se guardan en UNA transacción: o quedan los dos o ninguno.
    ///  - Filtrar/ordenar/buscar en memoria con List&lt;T&gt; y LINQ evita ir a la BD en cada tecla.
    /// </summary>
    public class PacienteServicio
    {
        public const string MensajeDniDuplicado = "Ya existe un paciente con ese DNI";

        private const int ErrorLlaveDuplicada1 = 2627;
        private const int ErrorLlaveDuplicada2 = 2601;

        private readonly PacienteDatos _pacientes;
        private readonly HistoriaDatos _historias;
        private readonly MedicamentoDatos _medicamentos;
        private readonly AlergiaDatos _alergias;
        private readonly Cie10Datos _cie10;

        public PacienteServicio() : this(new PacienteDatos(), new HistoriaDatos(), new MedicamentoDatos(), new AlergiaDatos()) { }

        public PacienteServicio(PacienteDatos pacientes, HistoriaDatos historias)
            : this(pacientes, historias, new MedicamentoDatos()) { }

        public PacienteServicio(PacienteDatos pacientes, HistoriaDatos historias, MedicamentoDatos medicamentos)
            : this(pacientes, historias, medicamentos, new AlergiaDatos()) { }

        public PacienteServicio(PacienteDatos pacientes, HistoriaDatos historias, MedicamentoDatos medicamentos, AlergiaDatos alergias)
            : this(pacientes, historias, medicamentos, alergias, new Cie10Datos()) { }

        public PacienteServicio(PacienteDatos pacientes, HistoriaDatos historias, MedicamentoDatos medicamentos, AlergiaDatos alergias,
                                Cie10Datos cie10)
        {
            if (cie10 == null) throw new ArgumentNullException("cie10");
            _cie10 = cie10;
            if (alergias == null) throw new ArgumentNullException("alergias");
            _alergias = alergias;
            if (pacientes == null) throw new ArgumentNullException("pacientes");
            if (historias == null) throw new ArgumentNullException("historias");
            if (medicamentos == null) throw new ArgumentNullException("medicamentos");
            _pacientes = pacientes;
            _historias = historias;
            _medicamentos = medicamentos;
        }

        // ---------- Consultas ----------

        /// <summary>Pacientes activos ordenados por apellido. El DNI NO viene descifrado.</summary>
        public List<Paciente> Listar()
        {
            Permisos.ExigirAccesoPacientes();
            return OrdenarPorApellido(_pacientes.Listar());
        }

        /// <summary>
        /// Una PÁGINA de pacientes (50 filas) que coinciden con el texto (por el inicio del apellido, el nombre
        /// o el N.º de historia). Se hacen dos consultas: un COUNT (para "Página 2 de 10") y la página con
        /// OFFSET/FETCH; nunca se traen todas las filas. Mide el tiempo con Stopwatch.
        /// Si la página pedida ya no existe (se dieron de baja pacientes), se muestra la última.
        /// <paramref name="cancelado"/> permite abandonar la búsqueda entre las dos consultas (el usuario siguió escribiendo);
        /// en ese caso devuelve null.
        /// </summary>
        public ResultadoPagina<Paciente> BuscarPagina(string texto, int pagina, Func<bool> cancelado)
        {
            Permisos.ExigirAccesoPacientes();
            Stopwatch reloj = Stopwatch.StartNew();

            int total = _pacientes.Contar(texto);
            if (cancelado != null && cancelado()) return null;

            int paginas = Paginacion.CalcularTotalPaginas(total, Paginacion.TamanoPagina);
            int paginaFinal = Math.Max(1, Math.Min(pagina, paginas));
            List<Paciente> filas = _pacientes.ListarPagina(texto, paginaFinal, Paginacion.TamanoPagina);

            reloj.Stop();
            ResultadoPagina<Paciente> r = new ResultadoPagina<Paciente>();
            r.Items = filas;
            r.Total = total;
            r.Pagina = paginaFinal;
            r.Milisegundos = reloj.ElapsedMilliseconds;
            return r;
        }

        /// <summary>Busca por DNI sin descifrar nada: compara contra el HMAC. Null si no existe.</summary>
        public Paciente BuscarPorDni(string dni)
        {
            Permisos.ExigirAccesoPacientes();
            string dniLimpio = Validador.Dni(dni);
            return _pacientes.BuscarPorDniHash(HmacDni.Calcular(dniLimpio));
        }

        // Filtros en memoria (List<T> + LINQ). No tocan la BD, no requieren llamar a la capa de Datos.

        public List<Paciente> FiltrarPorPosta(IEnumerable<Paciente> pacientes, int idPosta)
        {
            Permisos.ExigirAccesoPacientes();
            return pacientes.Where(p => p.IdPosta == idPosta).ToList();
        }

        public List<Paciente> BuscarEnMemoria(IEnumerable<Paciente> pacientes, string texto)
        {
            Permisos.ExigirAccesoPacientes();
            if (string.IsNullOrWhiteSpace(texto)) return pacientes.ToList();

            string[] palabras = texto.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            // Cada palabra escrita debe aparecer en nombres, apellidos o número de historia.
            return pacientes.Where(p => palabras.All(w =>
                    p.Nombres.IndexOf(w, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                    p.Apellidos.IndexOf(w, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                    (p.NumeroHistoria != null && p.NumeroHistoria.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0)))
                .ToList();
        }

        public List<Paciente> OrdenarPorApellido(IEnumerable<Paciente> pacientes)
        {
            return pacientes.OrderBy(p => p.Apellidos, StringComparer.CurrentCultureIgnoreCase)
                            .ThenBy(p => p.Nombres, StringComparer.CurrentCultureIgnoreCase)
                            .ToList();
        }

        /// <summary>Descifra el DNI de un paciente SOLO cuando la pantalla lo necesita.</summary>
        public string DescifrarDni(int idPaciente)
        {
            Permisos.ExigirAccesoPacientes();
            byte[] cifrado = _pacientes.ObtenerDniCifrado(idPaciente);
            if (cifrado == null) return string.Empty;
            try
            {
                return CifradoAes.DescifrarDeBytes(cifrado);
            }
            catch (CryptographicException)
            {
                return "[No se pudo descifrar]";
            }
        }

        // ---------- Validación ----------

        /// <summary>
        /// Valida todos los campos y devuelve un diccionario campo → mensaje (vacío si todo está bien).
        /// Las claves son: Dni, Nombres, Apellidos, FechaNacimiento, Telefono, Direccion.
        /// Así la pantalla puede mostrar cada error debajo de su campo.
        /// </summary>
        public Dictionary<string, string> Validar(string dni, string nombres, string apellidos, DateTime fechaNacimiento,
                                                  string telefono, string direccion, bool validarDni)
        {
            Dictionary<string, string> errores = new Dictionary<string, string>();
            Paciente temporal = new Paciente();

            Action<string, Action> probar = (campo, asignar) =>
            {
                try { asignar(); }
                catch (ArgumentException ex) { errores[campo] = ex.Message; }
            };

            if (validarDni) probar("Dni", () => temporal.Dni = dni);
            probar("Nombres", () => temporal.Nombres = nombres);
            probar("Apellidos", () => temporal.Apellidos = apellidos);
            probar("FechaNacimiento", () => temporal.FechaNacimiento = fechaNacimiento);
            probar("Telefono", () => temporal.Telefono = telefono);
            probar("Direccion", () => temporal.Direccion = direccion);
            return errores;
        }

        // ---------- Registro ----------

        /// <summary>
        /// Registra el paciente y crea su historia clínica (HC-000001...) en UNA sola transacción.
        /// Lanza ValidacionException (con Campo) si algo no es válido o el DNI ya existe.
        /// </summary>
        public Paciente Registrar(string dni, string nombres, string apellidos, DateTime fechaNacimiento,
                                  string telefono, string direccion)
        {
            Permisos.ExigirAccesoPacientes();

            LanzarPrimerError(Validar(dni, nombres, apellidos, fechaNacimiento, telefono, direccion, true));

            Paciente p = new Paciente(dni, nombres, apellidos, fechaNacimiento, telefono, direccion);
            p.IdPosta = Establecimiento.IdPosta; // el paciente se registra siempre en el establecimiento (no se elige posta)
            p.DniHash = HmacDni.Calcular(p.Dni);
            p.DniCifrado = CifradoAes.CifrarABytes(p.Dni);

            // Verificación previa (mensaje amable). La restricción UNIQUE de la BD es la garantía final.
            if (_pacientes.BuscarPorDniHash(p.DniHash) != null)
                throw new ValidacionException(MensajeDniDuplicado, "Dni");

            try
            {
                using (SqlConnection cn = Conexion.Crear())
                {
                    cn.Open();
                    using (SqlTransaction tx = cn.BeginTransaction())
                    {
                        try
                        {
                            p.IdPaciente = _pacientes.Insertar(p, cn, tx);

                            string numero = _historias.ObtenerSiguienteNumero(cn, tx);
                            HistoriaClinica h = new HistoriaClinica(p.IdPaciente, numero, null);
                            h.IdHistoria = _historias.Insertar(h, cn, tx);

                            tx.Commit();

                            p.IdHistoria = h.IdHistoria;
                            p.NumeroHistoria = h.NumeroHistoria;
                        }
                        catch
                        {
                            try { tx.Rollback(); } catch (InvalidOperationException) { /* la transacción ya terminó */ }
                            throw;
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                if (EsLlaveDuplicada(ex))
                {
                    if (ex.Message.IndexOf("HistoriaClinica", StringComparison.OrdinalIgnoreCase) >= 0)
                        throw new ReglaNegocioException("No se pudo asignar el número de historia. Intente nuevamente.");
                    throw new ValidacionException(MensajeDniDuplicado, "Dni");
                }
                throw;
            }

            p.Activo = true;
            // Auditoría: solo identificadores (nunca DNI ni nombres).
            AuditoriaServicio.Registrar(AccionAuditoria.AltaPaciente, "Paciente", p.IdPaciente,
                "Paciente Id " + p.IdPaciente + ", historia Id " + p.IdHistoria, AuditoriaServicio.Ok);
            return p;
        }

        /// <summary>Actualiza los datos editables (el DNI no se cambia).</summary>
        public void Actualizar(int idPaciente, string nombres, string apellidos, DateTime fechaNacimiento,
                               string telefono, string direccion)
        {
            Permisos.ExigirAccesoPacientes();
            if (idPaciente <= 0) throw new ReglaNegocioException("No hay un paciente seleccionado.");

            LanzarPrimerError(Validar(null, nombres, apellidos, fechaNacimiento, telefono, direccion, false));

            Paciente p = new Paciente();
            p.IdPaciente = idPaciente;
            p.Nombres = nombres;
            p.Apellidos = apellidos;
            p.FechaNacimiento = fechaNacimiento;
            p.Telefono = telefono;
            p.Direccion = direccion;
            _pacientes.Actualizar(p); // la posta del paciente no se modifica al editar
            AuditoriaServicio.Registrar(AccionAuditoria.EdicionPaciente, "Paciente", idPaciente,
                "Paciente Id " + idPaciente, AuditoriaServicio.Ok);
        }

        /// <summary>Da de baja al paciente (Activo = 0). Nunca se elimina su información.</summary>
        public void DarDeBaja(int idPaciente)
        {
            Permisos.ExigirAccesoPacientes();
            if (idPaciente <= 0) throw new ReglaNegocioException("No hay un paciente seleccionado.");
            _pacientes.BajaLogica(idPaciente);
            AuditoriaServicio.Registrar(AccionAuditoria.BajaPaciente, "Paciente", idPaciente,
                "Paciente Id " + idPaciente, AuditoriaServicio.Ok);
        }

        // ---------- Historia clínica y atenciones ----------

        public HistoriaClinica ObtenerHistoria(int idPaciente)
        {
            Permisos.ExigirAccesoPacientes();
            return _historias.ObtenerPorPaciente(idPaciente);
        }

        /// <summary>
        /// Atenciones de la historia con el diagnóstico y el código CIE-10 ya descifrados (solo en memoria), más recientes primero.
        /// Para cada código se busca su descripción en el catálogo (una consulta por código distinto de esta historia).
        /// Un código ilegible no rompe la lista: queda marcado (Cie10Ilegible) y no se escribe nada en errores.log.
        /// </summary>
        public List<Atencion> ListarAtenciones(int idHistoria)
        {
            Permisos.ExigirAccesoPacientes();
            List<Atencion> lista = _historias.ListarAtenciones(idHistoria);
            Dictionary<string, Cie10> descripciones = new Dictionary<string, Cie10>();
            foreach (Atencion a in lista)
            {
                try
                {
                    a.Diagnostico = CifradoAes.DescifrarDeBytes(a.DiagnosticoCifrado);
                }
                catch (CryptographicException)
                {
                    a.Diagnostico = "[No se pudo descifrar]";
                }
                CompletarCie10(a, descripciones);
            }
            return lista.OrderByDescending(a => a.Fecha).ToList();
        }

        /// <summary>Descifra el código CIE-10 de la atención y le pone la descripción del catálogo (usa un caché de esta llamada).</summary>
        private void CompletarCie10(Atencion a, Dictionary<string, Cie10> cache)
        {
            bool ilegible;
            a.Cie10Codigo = Cie10Servicio.DescifrarCodigo(a.Cie10Cifrado, out ilegible);
            a.Cie10Ilegible = ilegible;
            a.Cie10Descripcion = string.Empty;
            if (a.Cie10Codigo == null) return;

            Cie10 c;
            if (!cache.TryGetValue(a.Cie10Codigo, out c))
            {
                try { c = _cie10.ObtenerPorCodigo(a.Cie10Codigo); }
                catch (SqlException) { c = null; }   // sin catálogo disponible se muestra solo el código
                cache[a.Cie10Codigo] = c;
            }
            if (c != null) a.Cie10Descripcion = c.Descripcion;
        }

        /// <summary>
        /// Valida el código CIE-10 de una atención NUEVA: obligatorio y con formato válido (no consulta la base).
        /// Devuelve el mensaje de error o null. El mensaje nunca incluye el código escrito.
        /// </summary>
        public string ValidarCie10(string codigo)
        {
            try { Validador.Cie10Codigo(codigo, "Cie10"); return null; }
            catch (ArgumentException ex) { return ex.Message; }
        }
        /// <summary>Valida motivo y diagnóstico; devuelve campo → mensaje (claves Motivo y Diagnostico).</summary>
        public Dictionary<string, string> ValidarAtencion(string motivo, string diagnostico)
        {
            Dictionary<string, string> errores = new Dictionary<string, string>();
            Atencion temporal = new Atencion();
            try { temporal.Motivo = motivo; }
            catch (ArgumentException ex) { errores["Motivo"] = ex.Message; }
            try { temporal.Diagnostico = diagnostico; }
            catch (ArgumentException ex) { errores["Diagnostico"] = ex.Message; }
            return errores;
        }

        /// <summary>
        /// Medicamentos que se pueden recetar a un paciente de esa posta: activos, NO vencidos, con stock
        /// mayor que cero y de la misma posta. Se consulta con el permiso de pacientes (Administrador o Médico),
        /// así el Médico puede ver qué recetar SIN tener acceso a la pantalla de Inventario.
        /// </summary>
        public List<Medicamento> ListarMedicamentosRecetables(int idPosta)
        {
            Permisos.ExigirRecetar();
            if (idPosta <= 0) return new List<Medicamento>();

            return _medicamentos.Listar()
                .Where(m => m.IdPosta == idPosta && !m.EstaVencido && m.Stock > 0)
                .OrderBy(m => m.Nombre, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Valida la receta opcional. Claves de error: Medicamento y Cantidad.
        /// Sin medicamento y sin cantidad = atención sin receta (válido).
        /// </summary>
        public Dictionary<string, string> ValidarReceta(int? idMedicamento, int? cantidad)
        {
            Dictionary<string, string> errores = new Dictionary<string, string>();
            bool hayMedicamento = idMedicamento.HasValue && idMedicamento.Value > 0;

            if (!hayMedicamento && !cantidad.HasValue) return errores; // sin receta

            if (!hayMedicamento) errores["Medicamento"] = "Seleccione el medicamento a recetar.";
            if (!cantidad.HasValue) errores["Cantidad"] = "Ingrese la cantidad a recetar.";
            else if (cantidad.Value <= 0) errores["Cantidad"] = "La cantidad debe ser mayor que cero.";
            else if (cantidad.Value > InventarioServicio.CantidadMaximaPorMovimiento)
                errores["Cantidad"] = "La cantidad no puede superar " + InventarioServicio.CantidadMaximaPorMovimiento.ToString("N0") + ".";
            return errores;
        }

        /// <summary>
        /// Registra una atención con signos vitales y receta opcionales. Cifra el diagnóstico y guarda el usuario de la sesión.
        /// Con receta, TODO ocurre en UNA sola transacción (Commit / Rollback):
        ///   a) se inserta la Atencion (con signos vitales, IdMedicamento y CantidadRecetada);
        ///   b) se descuenta el stock con UPDATE ... WHERE StockActual >= @cant y se revisa las filas afectadas;
        ///   c) se inserta el MovimientoInventario 'Salida' con motivo "Receta HC-xxxxxx".
        /// Si el medicamento coincide con una ALERGIA ACTIVA del paciente, se rechaza (ValidacionException con Campo
        /// "Alergia") salvo que <paramref name="confirmaAlergia"/> sea true: así la confirmación explícita no depende
        /// solo de la pantalla. Si falta stock o algo falla, no queda NADA.
        /// </summary>
        public Atencion RegistrarAtencion(int idHistoria, string motivo, string diagnostico, int? idMedicamento, int? cantidad,
                                          SignosVitales signos, bool confirmaAlergia, string codigoCie10)
        {
            try
            {
                return RegistrarAtencionInterno(idHistoria, motivo, diagnostico, idMedicamento, cantidad, signos, confirmaAlergia, codigoCie10);
            }
            catch (SqlException ex) when (EsViolacionDeCheck(ex))
            {
                // La base de datos rechazó un valor por sus restricciones CHECK (rangos de los signos vitales).
                throw new ValidacionException(
                    "Algún valor está fuera del rango que acepta la base de datos. Revise los signos vitales.", "Signos");
            }
        }

        private Atencion RegistrarAtencionInterno(int idHistoria, string motivo, string diagnostico, int? idMedicamento, int? cantidad,
                                                  SignosVitales signos, bool confirmaAlergia, string codigoCie10)
        {
            Permisos.ExigirAccesoPacientes();
            if (idHistoria <= 0) throw new ReglaNegocioException("La historia clínica no es válida.");

            // Código CIE-10: OBLIGATORIO en las atenciones nuevas, con formato válido y existente (y activo) en el catálogo.
            string cie = Validador.Cie10Codigo(codigoCie10, "Cie10");
            Cie10 enCatalogo = _cie10.ObtenerPorCodigo(cie);
            if (enCatalogo == null || !enCatalogo.Activo)
                throw new ValidacionException("El código CIE-10 no existe en el catálogo o está dado de baja.", "Cie10");

            // Validaciones previas (no tocan la base): atención, signos vitales y, si hay, receta.
            Usuario usuario = Sesion.UsuarioActual; // ExigirAcceso ya garantizó que hay sesión
            Atencion a = new Atencion(idHistoria, usuario.IdUsuario, motivo, diagnostico, null);
            a.DiagnosticoCifrado = CifradoAes.CifrarABytes(a.Diagnostico);
            a.Cie10Cifrado = Cie10Servicio.CifrarCodigo(cie);   // el código viaja SOLO cifrado hacia la base
            a.Cie10Codigo = cie;                                // en memoria, para mostrarlo en la respuesta
            a.Cie10Descripcion = enCatalogo.Descripcion;
            a.NombreMedico = usuario.NombreCompleto;

            LanzarPrimerError(ValidarSignos(signos));
            a.Signos = (signos != null && signos.TieneDatos) ? signos : null;

            Dictionary<string, string> erroresReceta = ValidarReceta(idMedicamento, cantidad);
            bool hayReceta = erroresReceta.Count == 0 && idMedicamento.HasValue && idMedicamento.Value > 0 && cantidad.HasValue;
            if (erroresReceta.Count > 0) LanzarPrimerError(erroresReceta);

            if (!hayReceta)
            {
                // Sin receta: un solo INSERT, como antes.
                a.IdAtencion = _historias.InsertarAtencion(a);
                AuditarAtencion(a, idHistoria, false);
                return a;
            }

            Permisos.ExigirRecetar(); // recetar exige Administrador o Médico (se valida aquí, no en la pantalla)
            int idMed = idMedicamento.Value;
            int cant = cantidad.Value;
            a.IdMedicamento = idMed;
            a.CantidadRecetada = cant;

            EjecutarEnTransaccion(delegate (SqlConnection cn, SqlTransaction tx)
            {
                string numeroHistoria;
                int idPostaPaciente;
                if (!_historias.ObtenerNumeroYPosta(idHistoria, cn, tx, out numeroHistoria, out idPostaPaciente))
                    throw new ReglaNegocioException("La historia clínica ya no existe.");

                // El medicamento se lee DENTRO de la transacción y con la fila bloqueada.
                Medicamento m = _medicamentos.ObtenerPorIdParaReceta(idMed, cn, tx);
                if (m == null || !m.Activo)
                    throw new ValidacionException("El medicamento no existe o está dado de baja.", "Medicamento");
                if (m.IdPosta != idPostaPaciente)
                    throw new ValidacionException("El medicamento no pertenece a la posta del paciente.", "Medicamento");
                if (m.EstaVencido)
                    throw new ValidacionException("El medicamento está vencido y no se puede recetar.", "Medicamento");
                if (m.Stock < cant)
                    throw new ValidacionException(
                        "Stock insuficiente para recetar. Disponible: " + m.Stock + ", solicitado: " + cant + ".", "Cantidad");

                // Alergias activas del paciente: si coinciden con el medicamento, hace falta confirmación explícita.
                if (!confirmaAlergia)
                {
                    List<Alergia> coinciden = _alergias.ListarActivasPorHistoria(idHistoria, cn, tx)
                        .Where(x => Alergia.Coincide(x.Sustancia, m.Nombre)).ToList();
                    if (coinciden.Count > 0)
                    {
                        string detalle = string.Join(", ", coinciden.Select(x => x.Sustancia + " (" + x.Gravedad + ")"));
                        throw new ValidacionException(
                            "El paciente tiene una ALERGIA activa que coincide con \"" + m.Nombre + "\": " + detalle + ".", "Alergia");
                    }
                }

                // a) la atención con sus signos vitales y su receta
                a.IdAtencion = _historias.InsertarAtencion(a, cn, tx);

                // b) descuento de stock: UPDATE condicional + revisión de filas afectadas
                if (_medicamentos.DescontarStock(idMed, cant, cn, tx) == 0)
                    throw new ValidacionException("Stock insuficiente para recetar.", "Cantidad");

                // c) el movimiento de salida, con el N.º de historia y el usuario de la sesión
                MovimientoInventario mov = new MovimientoInventario(
                    idMed, usuario.IdUsuario, TipoMovimiento.Salida, cant, "Receta " + numeroHistoria);
                _medicamentos.InsertarMovimiento(mov, cn, tx);

                a.RecetaTexto = m.NombreCompleto + " x " + Medicamento.FormatoCantidad(cant, m.UnidadNombre);
            });

            // Auditoría FUERA de la transacción (ya confirmada): si fallara, no deshace la atención.
            AuditarAtencion(a, idHistoria, true);
            AuditoriaServicio.Registrar(AccionAuditoria.MovimientoStock, "Medicamento", idMed,
                "Salida por receta de " + cant + " unidad(es), atención Id " + a.IdAtencion, AuditoriaServicio.Ok);
            return a;
        }

        /// <summary>Registra la atención en la auditoría con identificadores solamente (nunca motivo ni diagnóstico).</summary>
        private static void AuditarAtencion(Atencion a, int idHistoria, bool conReceta)
        {
            string detalle = "Atención Id " + a.IdAtencion + ", historia Id " + idHistoria +
                             (conReceta ? ", con receta (medicamento Id " + a.IdMedicamento + ")" : string.Empty) +
                             (a.Signos != null ? ", con signos vitales" : string.Empty);
            AuditoriaServicio.Registrar(AccionAuditoria.Atencion, "Atencion", a.IdAtencion, detalle, AuditoriaServicio.Ok);
        }

        /// <summary>Valida los rangos de los signos vitales (los mismos que la base). Vacío si no hay signos o todo está bien.</summary>
        public Dictionary<string, string> ValidarSignos(SignosVitales signos)
        {
            if (signos == null) return new Dictionary<string, string>();
            return signos.Validar();
        }

        // ---------- Alergias ----------

        /// <summary>Todas las alergias del paciente (activas primero, las más graves antes).</summary>
        public List<Alergia> ListarAlergias(int idPaciente)
        {
            Permisos.ExigirAccesoPacientes();
            return _alergias.ListarPorPaciente(idPaciente);
        }

        /// <summary>Valida los datos de una alergia nueva. Claves: Sustancia, Gravedad, Observacion.</summary>
        public Dictionary<string, string> ValidarAlergia(string sustancia, string gravedad, string observacion)
        {
            Dictionary<string, string> errores = new Dictionary<string, string>();
            Alergia temporal = new Alergia();
            try { temporal.Sustancia = sustancia; } catch (ArgumentException ex) { errores["Sustancia"] = ex.Message; }
            try { temporal.Gravedad = gravedad; } catch (ArgumentException ex) { errores["Gravedad"] = ex.Message; }
            try { temporal.Observacion = observacion; } catch (ArgumentException ex) { errores["Observacion"] = ex.Message; }
            return errores;
        }

        /// <summary>Agrega una alergia activa. No permite repetir una sustancia que ya está activa.</summary>
        public Alergia AgregarAlergia(int idPaciente, string sustancia, string gravedad, string observacion)
        {
            Permisos.ExigirAccesoPacientes();
            if (idPaciente <= 0) throw new ReglaNegocioException("No hay un paciente seleccionado.");

            Alergia a = new Alergia(idPaciente, sustancia, gravedad, observacion);   // valida los campos

            string nueva = Alergia.Normalizar(a.Sustancia);
            bool repetida = _alergias.ListarPorPaciente(idPaciente)
                .Any(x => x.Activo && Alergia.Normalizar(x.Sustancia) == nueva);
            if (repetida)
                throw new ValidacionException("Esa alergia ya está registrada y activa.", "Sustancia");

            a.IdAlergia = _alergias.Insertar(a);
            return a;
        }

        /// <summary>Desactiva una alergia (nunca se borra: queda en el historial).</summary>
        public void DesactivarAlergia(int idAlergia)
        {
            Permisos.ExigirAccesoPacientes();
            if (idAlergia <= 0) throw new ReglaNegocioException("No hay una alergia seleccionada.");
            _alergias.Desactivar(idAlergia);
        }

        /// <summary>Texto para el aviso rojo: las sustancias de las alergias GRAVES activas (vacío si no hay).</summary>
        public static string TextoAlergiasGraves(IEnumerable<Alergia> alergias)
        {
            return string.Join(", ", alergias.Where(x => x.Activo && x.EsGrave).Select(x => x.Sustancia));
        }

        // ---------- Antecedentes (cifrados con AES, igual que el diagnóstico) ----------

        /// <summary>Descifra los antecedentes de la historia SOLO cuando la pantalla los necesita.</summary>
        public string ObtenerAntecedentes(HistoriaClinica historia)
        {
            Permisos.ExigirAccesoPacientes();
            if (historia == null || historia.AntecedentesCifrado == null || historia.AntecedentesCifrado.Length == 0)
                return string.Empty;
            try
            {
                return CifradoAes.DescifrarDeBytes(historia.AntecedentesCifrado);
            }
            catch (CryptographicException)
            {
                return "[No se pudo descifrar]";
            }
        }

        /// <summary>Cifra y guarda los antecedentes (texto vacío = sin antecedentes). Máximo 1000 caracteres.</summary>
        public void GuardarAntecedentes(int idHistoria, string texto)
        {
            Permisos.ExigirAccesoPacientes();
            if (idHistoria <= 0) throw new ReglaNegocioException("La historia clínica no es válida.");

            string limpio = Validador.TextoOpcional(texto, "Antecedentes", 1000);   // ValidacionException si es muy largo
            byte[] cifrado = limpio.Length == 0 ? null : CifradoAes.CifrarABytes(limpio);
            _historias.ActualizarAntecedentes(idHistoria, cifrado);
        }

        // ---------- Auxiliares ----------

        /// <summary>Abre conexión y transacción, ejecuta la acción y confirma; si algo falla, Rollback y se relanza el error.</summary>
        private static void EjecutarEnTransaccion(Action<SqlConnection, SqlTransaction> accion)
        {
            using (SqlConnection cn = Conexion.Crear())
            {
                cn.Open();
                using (SqlTransaction tx = cn.BeginTransaction())
                {
                    try
                    {
                        accion(cn, tx);
                        tx.Commit();
                    }
                    catch
                    {
                        try { tx.Rollback(); }
                        catch (InvalidOperationException) { /* la transacción ya había terminado */ }
                        throw;
                    }
                }
            }
        }

        private static void LanzarPrimerError(Dictionary<string, string> errores)
        {
            if (errores.Count == 0) return;
            KeyValuePair<string, string> primero = errores.First();
            throw new ValidacionException(primero.Value, primero.Key);
        }

        /// <summary>Error 547 (restricción violada) que menciona una restricción CHECK.</summary>
        private static bool EsViolacionDeCheck(SqlException ex)
        {
            return ex.Number == 547 && ex.Message.IndexOf("CHECK", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool EsLlaveDuplicada(SqlException ex)
        {
            return ex.Number == ErrorLlaveDuplicada1 || ex.Number == ErrorLlaveDuplicada2;
        }
    }
}
