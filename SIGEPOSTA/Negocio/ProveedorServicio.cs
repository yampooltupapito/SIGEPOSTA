using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SIGEPOSTA.Datos;

namespace SIGEPOSTA.Negocio
{
    /// <summary>
    /// Reglas de negocio de los proveedores de medicamentos.
    /// Decisiones de diseño:
    ///  - CADA método público vuelve a validar el rol (Administrador y Farmacia): ocultar botones no es seguridad.
    ///  - Baja lógica (Activo = 0): el proveedor deja de poder elegirse en nuevos ingresos, pero los movimientos
    ///    anteriores siguen mostrando su nombre.
    ///  - RUC duplicado: se revisa antes de guardar y, por si dos usuarios guardan a la vez, el índice UNIQUE de la
    ///    base lo vuelve a impedir (ProveedorDatos traduce ese error al mismo mensaje).
    ///  - Auditoría: solo "Proveedor Id N" (nada de RUC, nombre, teléfono ni correo).
    /// </summary>
    public class ProveedorServicio
    {
        public const string MensajeRucDuplicado = "Ya existe un proveedor con ese RUC";

        private readonly ProveedorDatos _datos;

        public ProveedorServicio() : this(new ProveedorDatos()) { }

        public ProveedorServicio(ProveedorDatos datos)
        {
            if (datos == null) throw new ArgumentNullException("datos");
            _datos = datos;
        }

        // ---------- Consultas ----------

        /// <summary>
        /// Una PÁGINA de proveedores activos (50 filas). El texto busca por el inicio del nombre o, si son solo
        /// dígitos, también por el inicio del RUC. Devuelve null si se canceló entre el COUNT y la página.
        /// </summary>
        public ResultadoPagina<Proveedor> BuscarPagina(string texto, int pagina, Func<bool> cancelado)
        {
            Permisos.ExigirAccesoProveedores();
            Stopwatch reloj = Stopwatch.StartNew();

            int total = _datos.Contar(texto);
            if (cancelado != null && cancelado()) return null;

            int paginas = Paginacion.CalcularTotalPaginas(total, Paginacion.TamanoPagina);
            int paginaFinal = Math.Max(1, Math.Min(pagina, paginas));
            List<Proveedor> filas = _datos.ListarPagina(texto, paginaFinal, Paginacion.TamanoPagina);

            reloj.Stop();
            ResultadoPagina<Proveedor> r = new ResultadoPagina<Proveedor>();
            r.Items = filas;
            r.Total = total;
            r.Pagina = paginaFinal;
            r.Milisegundos = reloj.ElapsedMilliseconds;
            return r;
        }

        public List<Proveedor> ListarActivos()
        {
            Permisos.ExigirAccesoProveedores();
            return _datos.ListarActivos();
        }

        public int ContarMovimientos(int idProveedor)
        {
            Permisos.ExigirAccesoProveedores();
            return _datos.ContarMovimientos(idProveedor);
        }

        // ---------- Validación ----------

        /// <summary>
        /// Valida los datos. Devuelve campo → mensaje (vacío si todo está bien).
        /// Claves: Nombre, Ruc, Telefono, Correo, Direccion. No consulta la base (el RUC duplicado se revisa al guardar).
        /// </summary>
        public Dictionary<string, string> Validar(string nombre, string ruc, string telefono, string correo, string direccion)
        {
            Dictionary<string, string> errores = new Dictionary<string, string>();
            Proveedor temporal = new Proveedor();

            Action<string, Action> probar = (campo, asignar) =>
            {
                try { asignar(); }
                catch (ArgumentException ex) { errores[campo] = ex.Message; }
            };
            probar("Nombre", () => temporal.Nombre = nombre);
            probar("Ruc", () => temporal.Ruc = ruc);
            probar("Telefono", () => temporal.Telefono = telefono);
            probar("Correo", () => temporal.Correo = correo);
            probar("Direccion", () => temporal.Direccion = direccion);
            return errores;
        }

        // ---------- Altas, cambios y bajas ----------

        public Proveedor Registrar(string nombre, string ruc, string telefono, string correo, string direccion)
        {
            Permisos.ExigirAccesoProveedores();
            LanzarPrimerError(Validar(nombre, ruc, telefono, correo, direccion));

            Proveedor p = new Proveedor(nombre, ruc, telefono, correo, direccion);
            if (_datos.ExisteRuc(p.Ruc, 0)) throw new ValidacionException(MensajeRucDuplicado, "Ruc");

            p.IdProveedor = _datos.Insertar(p);
            AuditoriaServicio.Registrar(AccionAuditoria.AltaProveedor, "Proveedor", p.IdProveedor,
                "Proveedor Id " + p.IdProveedor, AuditoriaServicio.Ok);
            return p;
        }

        public void Actualizar(int idProveedor, string nombre, string ruc, string telefono, string correo, string direccion)
        {
            Permisos.ExigirAccesoProveedores();
            if (idProveedor <= 0) throw new ReglaNegocioException("No hay un proveedor seleccionado.");
            LanzarPrimerError(Validar(nombre, ruc, telefono, correo, direccion));

            Proveedor p = new Proveedor(nombre, ruc, telefono, correo, direccion);
            p.IdProveedor = idProveedor;
            if (_datos.ExisteRuc(p.Ruc, idProveedor)) throw new ValidacionException(MensajeRucDuplicado, "Ruc");

            _datos.Actualizar(p);
            AuditoriaServicio.Registrar(AccionAuditoria.EdicionProveedor, "Proveedor", idProveedor,
                "Proveedor Id " + idProveedor, AuditoriaServicio.Ok);
        }

        /// <summary>Baja lógica. Se permite aunque tenga movimientos: su historial se conserva.</summary>
        public void DarDeBaja(int idProveedor)
        {
            Permisos.ExigirAccesoProveedores();
            if (idProveedor <= 0) throw new ReglaNegocioException("No hay un proveedor seleccionado.");
            _datos.BajaLogica(idProveedor);
            AuditoriaServicio.Registrar(AccionAuditoria.BajaProveedor, "Proveedor", idProveedor,
                "Proveedor Id " + idProveedor, AuditoriaServicio.Ok);
        }

        private static void LanzarPrimerError(Dictionary<string, string> errores)
        {
            if (errores.Count == 0) return;
            KeyValuePair<string, string> primero = errores.First();
            throw new ValidacionException(primero.Value, primero.Key);
        }
    }
}
