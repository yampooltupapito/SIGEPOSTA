using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using SIGEPOSTA.Datos;

namespace SIGEPOSTA.Negocio
{
    /// <summary>
    /// Reglas de negocio del inventario de medicamentos.
    /// Decisiones de diseño:
    ///  - CADA método público vuelve a validar el rol (solo Administrador y Farmacia): ocultar
    ///    botones en la pantalla no es seguridad.
    ///  - Cada movimiento actualiza el stock Y guarda su registro en UNA transacción: o quedan
    ///    los dos o ninguno, así el kardex siempre cuadra con el stock.
    ///  - La salida usa UPDATE condicional (StockActual >= @cant) y mira las filas afectadas:
    ///    el stock nunca queda negativo, ni siquiera con dos usuarios descontando a la vez.
    ///  - Las alertas (stock bajo, por vencer, vencido) se calculan en memoria con LINQ.
    /// </summary>
    public class InventarioServicio
    {
        public const int CantidadMaximaPorMovimiento = 1000000;
        public const int StockMaximo = 100000000;

        private readonly MedicamentoDatos _datos;
        private readonly ProveedorDatos _proveedores;
        private readonly CatalogoDatos _catalogos;

        public InventarioServicio() : this(new MedicamentoDatos()) { }

        public InventarioServicio(MedicamentoDatos datos) : this(datos, new ProveedorDatos()) { }

        public InventarioServicio(MedicamentoDatos datos, ProveedorDatos proveedores) : this(datos, proveedores, new CatalogoDatos()) { }

        public InventarioServicio(MedicamentoDatos datos, ProveedorDatos proveedores, CatalogoDatos catalogos)
        {
            if (catalogos == null) throw new ArgumentNullException("catalogos");
            _catalogos = catalogos;
            if (datos == null) throw new ArgumentNullException("datos");
            if (proveedores == null) throw new ArgumentNullException("proveedores");
            _datos = datos;
            _proveedores = proveedores;
        }

        /// <summary>
        /// Formas farmacéuticas ACTIVAS para elegir en un medicamento. Incluye "Sin definir" (la pantalla lo usa solo
        /// para filtrar los antiguos; no se ofrece al guardar).
        /// </summary>
        public List<ItemCatalogo> ListarFormasActivas()
        {
            Permisos.ExigirAccesoInventario();
            return _catalogos.Listar(TipoCatalogo.Forma).Where(i => i.Activo).ToList();
        }

        /// <summary>Unidades de medida ACTIVAS para elegir en un medicamento (incluye "Sin definir").</summary>
        public List<ItemCatalogo> ListarUnidadesActivas()
        {
            Permisos.ExigirAccesoInventario();
            return _catalogos.Listar(TipoCatalogo.Unidad).Where(i => i.Activo).ToList();
        }

        /// <summary>Proveedores activos, para elegir en un ingreso de stock.</summary>
        public List<Proveedor> ListarProveedoresActivos()
        {
            Permisos.ExigirAccesoInventario();
            return _proveedores.ListarActivos();
        }

        // ---------- Consultas ----------

        public List<Medicamento> Listar()
        {
            Permisos.ExigirAccesoInventario();
            return Ordenar(_datos.Listar());
        }

        /// <summary>
        /// Una PÁGINA de medicamentos (50 filas) que coinciden con el texto (por el inicio del nombre) y, si se
        /// pide, solo los críticos (stock bajo, por vencer o vencidos; el filtro lo aplica SQL). Se hacen dos
        /// consultas: un COUNT y la página con OFFSET/FETCH. Mide el tiempo con Stopwatch.
        /// <paramref name="cancelado"/> permite abandonar la búsqueda entre las dos consultas; en ese caso devuelve null.
        /// </summary>
        public ResultadoPagina<Medicamento> BuscarPagina(string texto, bool soloCriticos, int idForma, int pagina, Func<bool> cancelado)
        {
            Permisos.ExigirAccesoInventario();
            Stopwatch reloj = Stopwatch.StartNew();
            DateTime limite = DateTime.Today.AddDays(Medicamento.DiasAlertaVencimiento);

            int total = _datos.Contar(texto, soloCriticos, limite, idForma);
            if (cancelado != null && cancelado()) return null;

            int paginas = Paginacion.CalcularTotalPaginas(total, Paginacion.TamanoPagina);
            int paginaFinal = Math.Max(1, Math.Min(pagina, paginas));
            List<Medicamento> filas = _datos.ListarPagina(texto, soloCriticos, limite, idForma, paginaFinal, Paginacion.TamanoPagina);

            reloj.Stop();
            ResultadoPagina<Medicamento> r = new ResultadoPagina<Medicamento>();
            r.Items = filas;
            r.Total = total;
            r.Pagina = paginaFinal;
            r.Milisegundos = reloj.ElapsedMilliseconds;
            return r;
        }

        /// <summary>Filtra en memoria (LINQ) por nombre completo (nombre, concentración, forma), sin ir a la base de datos.</summary>
        public List<Medicamento> BuscarEnMemoria(IEnumerable<Medicamento> medicamentos, string texto)
        {
            Permisos.ExigirAccesoInventario();
            if (string.IsNullOrWhiteSpace(texto)) return medicamentos.ToList();

            string[] palabras = texto.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return medicamentos.Where(m => palabras.All(w =>
                    m.NombreCompleto.IndexOf(w, StringComparison.CurrentCultureIgnoreCase) >= 0))
                .ToList();
        }

        public List<MovimientoInventario> ListarMovimientos(int idMedicamento)
        {
            Permisos.ExigirAccesoInventario();
            return _datos.ListarMovimientos(idMedicamento).OrderByDescending(x => x.Fecha).ToList();
        }

        // ---------- Alertas (LINQ sobre List<T>) ----------

        /// <summary>STOCK BAJO: StockActual &lt;= StockMinimo.</summary>
        public List<Medicamento> StockBajo(IEnumerable<Medicamento> medicamentos)
        {
            Permisos.ExigirAccesoInventario();
            return medicamentos.Where(m => m.EstaBajoStock).ToList();
        }

        /// <summary>POR VENCER: vence en 30 días o menos (y todavía no venció).</summary>
        public List<Medicamento> PorVencer(IEnumerable<Medicamento> medicamentos)
        {
            Permisos.ExigirAccesoInventario();
            return medicamentos.Where(m => m.EstaPorVencer).ToList();
        }

        /// <summary>VENCIDO: la fecha de vencimiento ya pasó.</summary>
        public List<Medicamento> Vencidos(IEnumerable<Medicamento> medicamentos)
        {
            Permisos.ExigirAccesoInventario();
            return medicamentos.Where(m => m.EstaVencido).ToList();
        }

        /// <summary>Críticos (cualquier alerta), con los vencidos primero y luego los que vencen antes.</summary>
        public List<Medicamento> Criticos(IEnumerable<Medicamento> medicamentos)
        {
            Permisos.ExigirAccesoInventario();
            return medicamentos
                .Where(m => m.EstaBajoStock || m.EstaPorVencer || m.EstaVencido)
                .OrderByDescending(m => m.EstaVencido)
                .ThenByDescending(m => m.EstaPorVencer)
                .ThenBy(m => m.Nombre, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        /// <summary>Carga el inventario y devuelve solo los medicamentos críticos.</summary>
        public List<Medicamento> ObtenerCriticos()
        {
            Permisos.ExigirAccesoInventario();
            return Criticos(_datos.Listar());
        }

        /// <summary>Texto de la columna Alerta (vacío si no hay ninguna).</summary>
        public static string TextoAlerta(Medicamento m)
        {
            List<string> partes = new List<string>();
            if (m.EstaVencido) partes.Add("VENCIDO");
            else if (m.EstaPorVencer) partes.Add("POR VENCER");
            if (m.EstaBajoStock) partes.Add("STOCK BAJO");
            return string.Join(" · ", partes);
        }

        // ---------- Validación ----------

        /// <summary>
        /// Valida los datos del medicamento. Devuelve campo → mensaje (vacío si todo está bien).
        /// Claves: Nombre, Concentracion, Forma, Unidad, StockMinimo, FechaVencimiento, StockInicial.
        /// Forma y unidad son OBLIGATORIAS (idForma / idUnidad mayores que cero): un medicamento antiguo en "Sin definir"
        /// solo se puede guardar cuando se le eligen forma y unidad reales.
        /// </summary>
        public Dictionary<string, string> Validar(string nombre, string concentracion, int idForma, int idUnidad, int stockMinimo,
                                                  DateTime? fechaVencimiento, int stockInicial, bool validarStockInicial)
        {
            Dictionary<string, string> errores = new Dictionary<string, string>();
            Medicamento temporal = new Medicamento();

            Action<string, Action> probar = (campo, asignar) =>
            {
                try { asignar(); }
                catch (ArgumentException ex) { errores[campo] = ex.Message; }
            };

            probar("Nombre", () => temporal.Nombre = nombre);
            if (!string.IsNullOrWhiteSpace(concentracion) && concentracion.Trim().Length > Medicamento.MaxConcentracion)
                errores["Concentracion"] = "La concentración no puede superar " + Medicamento.MaxConcentracion + " caracteres.";
            if (idForma <= 0) errores["Forma"] = "Seleccione la forma farmacéutica.";
            if (idUnidad <= 0) errores["Unidad"] = "Seleccione la unidad.";
            // Mensajes propios (los de la entidad hablan de "StockMinimo"/"Stock", nombres internos).
            if (stockMinimo < 0) errores["StockMinimo"] = "El stock mínimo no puede ser negativo.";
            if (validarStockInicial && stockInicial < 0) errores["StockInicial"] = "El stock inicial no puede ser negativo.";

            if (!fechaVencimiento.HasValue) errores["FechaVencimiento"] = "La fecha de vencimiento es obligatoria.";
            if (stockMinimo > StockMaximo) errores["StockMinimo"] = "El stock mínimo es demasiado grande.";
            if (validarStockInicial && stockInicial > StockMaximo) errores["StockInicial"] = "El stock inicial es demasiado grande.";
            return errores;
        }

        /// <summary>
        /// Comprueba contra el catálogo que la forma y la unidad existan, estén ACTIVAS y no sean "Sin definir"
        /// (las inactivas no se ofrecen en medicamentos nuevos). Devuelve el nombre de la forma para componer Presentacion.
        /// </summary>
        private string VerificarFormaYUnidad(int idForma, int idUnidad)
        {
            ItemCatalogo forma = _catalogos.Obtener(TipoCatalogo.Forma, idForma);
            if (forma == null || !forma.Activo || forma.EsSinDefinir)
                throw new ValidacionException("La forma farmacéutica no está disponible. Elija otra.", "Forma");
            ItemCatalogo unidad = _catalogos.Obtener(TipoCatalogo.Unidad, idUnidad);
            if (unidad == null || !unidad.Activo || unidad.EsSinDefinir)
                throw new ValidacionException("La unidad no está disponible. Elija otra.", "Unidad");
            return forma.Nombre;
        }
        /// <summary>Valida cantidad (mayor que cero) y motivo. Claves: Cantidad, Motivo.</summary>
        public Dictionary<string, string> ValidarMovimiento(int cantidad, string motivo)
        {
            Dictionary<string, string> errores = new Dictionary<string, string>();
            if (cantidad <= 0) errores["Cantidad"] = "La cantidad debe ser mayor que cero.";
            else if (cantidad > CantidadMaximaPorMovimiento)
                errores["Cantidad"] = "La cantidad no puede superar " + CantidadMaximaPorMovimiento.ToString("N0") + ".";
            if (!string.IsNullOrWhiteSpace(motivo) && motivo.Trim().Length > 200)
                errores["Motivo"] = "El motivo no puede superar 200 caracteres.";
            return errores;
        }

        /// <summary>
        /// Valida un INGRESO: cantidad y motivo como siempre, y además proveedor elegido (idProveedor mayor que cero)
        /// y N.º de guía o factura obligatorio (máx. 30). Claves: Cantidad, Motivo, Proveedor, NumeroDocumento.
        /// </summary>
        public Dictionary<string, string> ValidarIngreso(int cantidad, string motivo, int idProveedor, string numeroDocumento)
        {
            Dictionary<string, string> errores = ValidarMovimiento(cantidad, motivo);
            if (idProveedor <= 0) errores["Proveedor"] = "Seleccione el proveedor del ingreso.";
            if (string.IsNullOrWhiteSpace(numeroDocumento))
                errores["NumeroDocumento"] = "Ingrese el número de guía o factura.";
            else if (numeroDocumento.Trim().Length > MovimientoInventario.MaxNumeroDocumento)
                errores["NumeroDocumento"] = "El número de guía o factura no puede superar " + MovimientoInventario.MaxNumeroDocumento + " caracteres.";
            return errores;
        }

        // ---------- Altas, cambios y bajas ----------

        /// <summary>
        /// Registra el medicamento. Si el stock inicial es mayor que cero, también guarda un movimiento
        /// "Ingreso" (motivo "Stock inicial") en la misma transacción para que el historial cuadre.
        /// </summary>
        public Medicamento Registrar(string nombre, string concentracion, int idForma, int idUnidad, int stockMinimo,
                                     DateTime? fechaVencimiento, int stockInicial)
        {
            Permisos.ExigirAccesoInventario();
            LanzarPrimerError(Validar(nombre, concentracion, idForma, idUnidad, stockMinimo, fechaVencimiento, stockInicial, true));
            string formaNombre = VerificarFormaYUnidad(idForma, idUnidad);

            // Presentacion se conserva por compatibilidad: se llena con "concentración - forma" (máx. 60 caracteres).
            Medicamento m = new Medicamento(nombre, ComponerPresentacion(concentracion, formaNombre), stockInicial, stockMinimo, fechaVencimiento);
            m.Concentracion = concentracion;
            m.IdForma = idForma;
            m.IdUnidad = idUnidad;
            m.IdPosta = Establecimiento.IdPosta; // el medicamento se registra siempre en el establecimiento (no se elige posta)
            int idUsuario = SeguridadUsuarioActual();

            EjecutarEnTransaccion(delegate (SqlConnection cn, SqlTransaction tx)
            {
                m.IdMedicamento = _datos.Insertar(m, cn, tx);
                if (stockInicial > 0)
                {
                    MovimientoInventario mov = new MovimientoInventario(
                        m.IdMedicamento, idUsuario, TipoMovimiento.Ingreso, stockInicial, "Stock inicial");
                    _datos.InsertarMovimiento(mov, cn, tx);
                }
            });
            AuditoriaServicio.Registrar(AccionAuditoria.AltaMedicamento, "Medicamento", m.IdMedicamento,
                "Medicamento Id " + m.IdMedicamento + ", stock inicial " + stockInicial, AuditoriaServicio.Ok);
            if (stockInicial > 0)
                AuditoriaServicio.Registrar(AccionAuditoria.MovimientoStock, "Medicamento", m.IdMedicamento,
                    "Ingreso de " + stockInicial + " unidad(es) (stock inicial)", AuditoriaServicio.Ok);
            return m;
        }

        /// <summary>Actualiza los datos editables (el stock solo cambia con movimientos).</summary>
        public void Actualizar(int idMedicamento, string nombre, string concentracion, int idForma, int idUnidad, int stockMinimo,
                               DateTime? fechaVencimiento)
        {
            Permisos.ExigirAccesoInventario();
            if (idMedicamento <= 0) throw new ReglaNegocioException("No hay un medicamento seleccionado.");
            LanzarPrimerError(Validar(nombre, concentracion, idForma, idUnidad, stockMinimo, fechaVencimiento, 0, false));
            string formaNombre = VerificarFormaYUnidad(idForma, idUnidad);

            Medicamento m = new Medicamento(nombre, ComponerPresentacion(concentracion, formaNombre), 0, stockMinimo, fechaVencimiento);
            m.Concentracion = concentracion;
            m.IdForma = idForma;
            m.IdUnidad = idUnidad;
            m.IdMedicamento = idMedicamento;
            _datos.Actualizar(m); // la posta del medicamento no se modifica al editar
            AuditoriaServicio.Registrar(AccionAuditoria.EdicionMedicamento, "Medicamento", idMedicamento,
                "Medicamento Id " + idMedicamento, AuditoriaServicio.Ok);
        }

        /// <summary>Da de baja el medicamento (Activo = 0). Su historial de movimientos se conserva.</summary>
        public void DarDeBaja(int idMedicamento)
        {
            Permisos.ExigirAccesoInventario();
            if (idMedicamento <= 0) throw new ReglaNegocioException("No hay un medicamento seleccionado.");
            _datos.BajaLogica(idMedicamento);
            AuditoriaServicio.Registrar(AccionAuditoria.BajaMedicamento, "Medicamento", idMedicamento,
                "Medicamento Id " + idMedicamento, AuditoriaServicio.Ok);
        }

        // ---------- Movimientos de stock ----------

        /// <summary>
        /// Suma stock y registra el movimiento "Ingreso" (con su proveedor y N.º de guía o factura) en UNA transacción.
        /// El proveedor debe estar activo; se comprueba dentro de la misma transacción.
        /// </summary>
        public void RegistrarIngreso(int idMedicamento, int cantidad, string motivo, int idProveedor, string numeroDocumento)
        {
            Permisos.ExigirAccesoInventario();
            LanzarPrimerError(ValidarIngreso(cantidad, motivo, idProveedor, numeroDocumento));
            int idUsuario = SeguridadUsuarioActual();
            MovimientoInventario mov = new MovimientoInventario(idMedicamento, idUsuario, TipoMovimiento.Ingreso, cantidad, motivo);
            mov.IdProveedor = idProveedor;
            mov.NumeroDocumento = numeroDocumento;

            EjecutarEnTransaccion(delegate (SqlConnection cn, SqlTransaction tx)
            {
                if (!_proveedores.ExisteActivo(idProveedor, cn, tx))
                    throw new ValidacionException("El proveedor elegido no existe o está dado de baja.", "Proveedor");
                int filas = _datos.IncrementarStock(idMedicamento, cantidad, cn, tx);
                if (filas == 0)
                    throw new ReglaNegocioException("El medicamento no existe o está dado de baja.");
                _datos.InsertarMovimiento(mov, cn, tx);
            });
            // El motivo y el documento son texto libre: no se copian a la auditoría (solo tipo, cantidad e Id de proveedor).
            AuditoriaServicio.Registrar(AccionAuditoria.MovimientoStock, "Medicamento", idMedicamento,
                "Ingreso de " + cantidad + " unidad(es), proveedor Id " + idProveedor, AuditoriaServicio.Ok);
        }

        /// <summary>
        /// Resta stock y registra el movimiento "Salida" en una sola transacción.
        /// Si no alcanza el stock, se hace Rollback y se lanza "Stock insuficiente" (el stock no cambia).
        /// </summary>
        public void RegistrarSalida(int idMedicamento, int cantidad, string motivo)
        {
            Permisos.ExigirAccesoInventario();
            LanzarPrimerError(ValidarMovimiento(cantidad, motivo));
            int idUsuario = SeguridadUsuarioActual();
            MovimientoInventario mov = new MovimientoInventario(idMedicamento, idUsuario, TipoMovimiento.Salida, cantidad, motivo);

            EjecutarEnTransaccion(delegate (SqlConnection cn, SqlTransaction tx)
            {
                // UPDATE ... SET StockActual = StockActual - @cant WHERE ... AND StockActual >= @cant
                int filas = _datos.DescontarStock(idMedicamento, cantidad, cn, tx);
                if (filas == 0)
                {
                    // Ninguna fila cambió: se explica el motivo. Al lanzar, EjecutarEnTransaccion hace Rollback.
                    int? disponible = _datos.ObtenerStock(idMedicamento, cn, tx);
                    if (!disponible.HasValue)
                        throw new ReglaNegocioException("El medicamento no existe o está dado de baja.");
                    throw new ValidacionException(
                        "Stock insuficiente. Disponible: " + disponible.Value + ", solicitado: " + cantidad + ".", "Cantidad");
                }
                _datos.InsertarMovimiento(mov, cn, tx);
            });
            AuditoriaServicio.Registrar(AccionAuditoria.MovimientoStock, "Medicamento", idMedicamento,
                "Salida de " + cantidad + " unidad(es)", AuditoriaServicio.Ok);
        }

        // ---------- Auxiliares ----------

        /// <summary>Texto de la columna Presentacion (compatibilidad): "500 mg - Tableta", recortado a 60 caracteres.</summary>
        private static string ComponerPresentacion(string concentracion, string forma)
        {
            string t = (string.IsNullOrWhiteSpace(concentracion) ? string.Empty : concentracion.Trim() + " - ") + forma;
            return t.Length > 60 ? t.Substring(0, 60) : t;
        }

        private static int SeguridadUsuarioActual()
        {
            // ExigirAcceso ya garantizó que hay sesión; aquí solo se toma el Id.
            return Seguridad.Sesion.UsuarioActual.IdUsuario;
        }

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

        private static List<Medicamento> Ordenar(IEnumerable<Medicamento> lista)
        {
            return lista.OrderBy(m => m.Nombre, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        private static void LanzarPrimerError(Dictionary<string, string> errores)
        {
            if (errores.Count == 0) return;
            KeyValuePair<string, string> primero = errores.First();
            throw new ValidacionException(primero.Value, primero.Key);
        }
    }
}
