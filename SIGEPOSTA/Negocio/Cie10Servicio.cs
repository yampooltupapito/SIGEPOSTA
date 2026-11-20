using System;
using System.Collections.Generic;
using System.Diagnostics;
using SIGEPOSTA.Datos;
using SIGEPOSTA.Seguridad;

namespace SIGEPOSTA.Negocio
{
    /// <summary>
    /// Catálogo de diagnósticos CIE-10.
    /// Decisiones de diseño:
    ///  - Mantener el catálogo (listar, agregar, editar, dar de baja) es SOLO del Administrador.
    ///  - Consultar sugerencias y descripciones (para atender) lo pueden hacer Administrador y Médico, que son
    ///    quienes registran atenciones.
    ///  - El código no se edita (es la clave); solo la descripción. La baja es lógica.
    ///  - Auditoría: solo "Catálogo CIE-10" (sin el código, para que ningún código CIE-10 llegue jamás a la bitácora).
    /// </summary>
    public class Cie10Servicio
    {
        public const int MaximoSugerencias = 15;

        private readonly Cie10Datos _datos;

        public Cie10Servicio() : this(new Cie10Datos()) { }

        public Cie10Servicio(Cie10Datos datos)
        {
            if (datos == null) throw new ArgumentNullException("datos");
            _datos = datos;
        }

        // ---------- Consultas ----------

        /// <summary>Una PÁGINA del catálogo (50 filas) para la pantalla de mantenimiento. Solo Administrador. Null si se canceló.</summary>
        public ResultadoPagina<Cie10> BuscarPagina(string texto, int pagina, Func<bool> cancelado)
        {
            Permisos.ExigirAdministrador();
            Stopwatch reloj = Stopwatch.StartNew();

            int total = _datos.Contar(texto);
            if (cancelado != null && cancelado()) return null;

            int paginas = Paginacion.CalcularTotalPaginas(total, Paginacion.TamanoPagina);
            int paginaFinal = Math.Max(1, Math.Min(pagina, paginas));
            List<Cie10> filas = _datos.ListarPagina(texto, paginaFinal, Paginacion.TamanoPagina);

            reloj.Stop();
            ResultadoPagina<Cie10> r = new ResultadoPagina<Cie10>();
            r.Items = filas;
            r.Total = total;
            r.Pagina = paginaFinal;
            r.Milisegundos = reloj.ElapsedMilliseconds;
            return r;
        }

        /// <summary>
        /// Sugerencias para el autocompletado de "Diagnóstico CIE-10" (máx. 15, solo activos). Administrador o Médico.
        /// Se devuelve como ResultadoPagina para reutilizar la búsqueda en segundo plano de la pantalla.
        /// </summary>
        public ResultadoPagina<Cie10> Sugerir(string texto, Func<bool> cancelado)
        {
            Permisos.ExigirAccesoPacientes();
            Stopwatch reloj = Stopwatch.StartNew();
            List<Cie10> filas = _datos.Sugerir(texto, MaximoSugerencias);
            reloj.Stop();
            if (cancelado != null && cancelado()) return null;

            ResultadoPagina<Cie10> r = new ResultadoPagina<Cie10>();
            r.Items = filas;
            r.Total = filas.Count;
            r.Pagina = 1;
            r.Milisegundos = reloj.ElapsedMilliseconds;
            return r;
        }

        /// <summary>El código exacto (activo o no). Null si no existe en el catálogo.</summary>
        public Cie10 Obtener(string codigo)
        {
            Permisos.ExigirAccesoPacientes();
            string c = Validador.NormalizarCie10(codigo);
            if (!Validador.EsCodigoCie10(c)) return null;
            return _datos.ObtenerPorCodigo(c);
        }

        // ---------- Validación ----------

        /// <summary>Valida código (formato) y descripción. Claves: Codigo, Descripcion. No consulta la base.</summary>
        public Dictionary<string, string> Validar(string codigo, string descripcion, bool validarCodigo)
        {
            Dictionary<string, string> errores = new Dictionary<string, string>();
            Cie10 temporal = new Cie10();
            if (validarCodigo)
            {
                try { temporal.Codigo = codigo; }
                catch (ArgumentException ex) { errores["Codigo"] = ex.Message; }
            }
            try { temporal.Descripcion = descripcion; }
            catch (ArgumentException ex) { errores["Descripcion"] = ex.Message; }
            return errores;
        }

        // ---------- Altas, cambios y bajas (solo Administrador) ----------

        public Cie10 Registrar(string codigo, string descripcion)
        {
            Permisos.ExigirAdministrador();
            LanzarPrimerError(Validar(codigo, descripcion, true));

            Cie10 c = new Cie10(codigo, descripcion);
            Cie10 existente = _datos.ObtenerPorCodigo(c.Codigo);
            if (existente != null)
                throw new ValidacionException(existente.Activo
                    ? "Ese código CIE-10 ya existe en el catálogo."
                    : "Ese código CIE-10 ya existe en el catálogo pero está dado de baja.", "Codigo");

            _datos.Insertar(c);   // si dos usuarios lo crean a la vez, la clave primaria lo vuelve a impedir
            AuditoriaServicio.Registrar(AccionAuditoria.AltaCie10, "Cie10", null, "Catálogo CIE-10", AuditoriaServicio.Ok);
            return c;
        }

        /// <summary>Cambia solo la descripción (el código no se edita).</summary>
        public void Actualizar(string codigo, string descripcion)
        {
            Permisos.ExigirAdministrador();
            LanzarPrimerError(Validar(codigo, descripcion, false));
            string c = Validador.Cie10Codigo(codigo, "Codigo");

            Cie10 temporal = new Cie10();
            temporal.Descripcion = descripcion;
            _datos.ActualizarDescripcion(c, temporal.Descripcion);
            AuditoriaServicio.Registrar(AccionAuditoria.EdicionCie10, "Cie10", null, "Catálogo CIE-10", AuditoriaServicio.Ok);
        }

        /// <summary>Baja lógica: deja de ofrecerse, pero las atenciones antiguas lo siguen mostrando.</summary>
        public void DarDeBaja(string codigo)
        {
            Permisos.ExigirAdministrador();
            string c = Validador.Cie10Codigo(codigo, "Codigo");
            _datos.BajaLogica(c);
            AuditoriaServicio.Registrar(AccionAuditoria.BajaCie10, "Cie10", null, "Catálogo CIE-10", AuditoriaServicio.Ok);
        }

        // ---------- Cifrado del código de una atención ----------

        /// <summary>
        /// Cifra el código (AES-256-CBC con IV aleatorio, igual que el diagnóstico). El código ya debe venir validado.
        /// Es el ÚNICO formato en que se guarda en la base de datos (Atencion.Cie10Cifrado).
        /// </summary>
        public static byte[] CifrarCodigo(string codigoValidado)
        {
            return CifradoAes.CifrarABytes(codigoValidado);
        }

        /// <summary>
        /// Descifra el código guardado. Devuelve null si no hay dato (atención antigua) y marca ilegible
        /// si había bytes pero no se pudo descifrar (llave distinta, dato dañado) o el resultado no tiene formato CIE-10.
        /// No lanza excepciones ni escribe nada en ningún registro: así un dato dañado no deja rastro del contenido.
        /// </summary>
        public static string DescifrarCodigo(byte[] datos, out bool ilegible)
        {
            ilegible = false;
            if (datos == null || datos.Length == 0) return null;
            try
            {
                string c = Validador.NormalizarCie10(CifradoAes.DescifrarDeBytes(datos));
                if (Validador.EsCodigoCie10(c)) return c;
            }
            catch (Exception)
            {
                // CryptographicException u otro error al leer: se trata igual (el mensaje podría traer datos internos).
            }
            ilegible = true;
            return null;
        }

        private static void LanzarPrimerError(Dictionary<string, string> errores)
        {
            foreach (KeyValuePair<string, string> par in errores)
                throw new ValidacionException(par.Value, par.Key);
        }
    }
}
