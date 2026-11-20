using System;
using System.Collections.Generic;
using System.Diagnostics;
using SIGEPOSTA.Datos;
using SIGEPOSTA.Seguridad;

namespace SIGEPOSTA.Negocio
{
    /// <summary>Un renglón de la tabla Auditoria.</summary>
    public class RegistroAuditoria
    {
        public long IdAuditoria { get; set; }
        public DateTime Fecha { get; set; }
        public int? IdUsuario { get; set; }
        public string NombreUsuario { get; set; }
        public string Accion { get; set; }
        public string Entidad { get; set; }
        public int? IdEntidad { get; set; }
        public string Detalle { get; set; }
        public string Resultado { get; set; }
    }

    /// <summary>Filtros de la consulta de auditoría. Todos opcionales.</summary>
    public class FiltroAuditoria
    {
        public DateTime? Desde { get; set; }
        public DateTime? Hasta { get; set; }
        /// <summary>Nombre de usuario (búsqueda por el inicio del texto).</summary>
        public string Usuario { get; set; }
        /// <summary>Acción exacta (una de AccionAuditoria.Todas).</summary>
        public string Accion { get; set; }
    }

    /// <summary>Nombres fijos de las acciones auditadas (así el filtro y los registros usan el mismo texto).</summary>
    public static class AccionAuditoria
    {
        public const string InicioSesion = "Inicio de sesión";
        public const string InicioSesionFallido = "Inicio de sesión fallido";
        public const string BloqueoUsuario = "Bloqueo de usuario";
        public const string CierreSesion = "Cierre de sesión";
        public const string CrearUsuario = "Creación de usuario";
        public const string CambioRol = "Cambio de rol";
        public const string CambioEstadoUsuario = "Cambio de estado de usuario";
        public const string RestablecerContrasena = "Restablecimiento de contraseña";
        public const string AltaPaciente = "Alta de paciente";
        public const string EdicionPaciente = "Edición de paciente";
        public const string BajaPaciente = "Baja de paciente";
        public const string AltaMedicamento = "Alta de medicamento";
        public const string EdicionMedicamento = "Edición de medicamento";
        public const string BajaMedicamento = "Baja de medicamento";
        public const string AltaProveedor = "Alta de proveedor";
        public const string EdicionProveedor = "Edición de proveedor";
        public const string BajaProveedor = "Baja de proveedor";
        public const string AltaCie10 = "Alta de código CIE-10";
        public const string EdicionCie10 = "Edición de código CIE-10";
        public const string BajaCie10 = "Baja de código CIE-10";
        public const string AltaForma = "Alta de forma farmacéutica";
        public const string EdicionForma = "Edición de forma farmacéutica";
        public const string BajaForma = "Baja de forma farmacéutica";
        public const string AltaUnidad = "Alta de unidad de medida";
        public const string EdicionUnidad = "Edición de unidad de medida";
        public const string BajaUnidad = "Baja de unidad de medida";
        public const string Atencion = "Registro de atención";
        public const string MovimientoStock = "Movimiento de stock";
        public const string Respaldo = "Respaldo";
        public const string Restauracion = "Restauración";
        public const string AccesoDenegado = "Acceso denegado";
        public const string ExportarAuditoria = "Exportación de auditoría";

        public static readonly string[] Todas =
        {
            InicioSesion, InicioSesionFallido, BloqueoUsuario, CierreSesion, CrearUsuario, CambioRol, CambioEstadoUsuario,
            RestablecerContrasena, AltaPaciente, EdicionPaciente, BajaPaciente, AltaMedicamento, EdicionMedicamento,
            BajaMedicamento, AltaProveedor, EdicionProveedor, BajaProveedor, AltaCie10, EdicionCie10, BajaCie10, AltaForma, EdicionForma, BajaForma, AltaUnidad, EdicionUnidad, BajaUnidad, Atencion, MovimientoStock, Respaldo, Restauracion, AccesoDenegado, ExportarAuditoria
        };
    }

    /// <summary>
    /// Auditoría: quién hizo qué y cuándo. Decisiones de diseño:
    ///  - Registrar es estático y NUNCA lanza excepciones: si la auditoría falla (base caída, columna más corta, etc.)
    ///    la operación principal sigue; el fallo se avisa por AlFallar (Program lo manda al archivo errores.log).
    ///  - Toma el usuario de la sesión actual (Sesion). Solo el login fallido indica el usuario a mano.
    ///  - REGLA: en Detalle NUNCA van datos sensibles (DNI, diagnósticos, antecedentes, contraseñas ni nombres de
    ///    personas). Se usan identificadores: "Paciente Id 12". Registrar además recorta y limpia el texto.
    ///  - La tabla solo admite INSERT (trigger en la base): aquí no hay forma de editar ni borrar.
    ///  - Consultar y exportar la auditoría es solo para el Administrador.
    /// </summary>
    public class AuditoriaServicio
    {
        public const string Ok = "OK";
        public const string Fallo = "FALLO";
        public const string Denegado = "DENEGADO";
        public const string Cancelado = "CANCELADO";

        public const int MaximoExportar = 50000;

        // Longitudes asumidas de las columnas (recortar evita que un texto largo haga fallar el INSERT).
        private const int LargoAccion = 50, LargoEntidad = 50, LargoDetalle = 200, LargoUsuario = 50, LargoResultado = 30;

        private static AuditoriaDatos _datos = new AuditoriaDatos();

        /// <summary>Se invoca (si está asignada) cuando el registro de auditoría falla. Nunca debe lanzar excepciones.</summary>
        public static Action<Exception> AlFallar { get; set; }

        /// <summary>Solo para pruebas: reemplaza el acceso a datos.</summary>
        public static void UsarDatos(AuditoriaDatos datos)
        {
            _datos = datos ?? new AuditoriaDatos();
        }

        /// <summary>Registra una acción del usuario de la sesión actual (o sin usuario si no hay sesión).</summary>
        public static void Registrar(string accion, string entidad, int? idEntidad, string detalle, string resultado)
        {
            Usuario u = Sesion.UsuarioActual;
            RegistrarComo(u == null ? (int?)null : u.IdUsuario, u == null ? null : u.NombreUsuario,
                          accion, entidad, idEntidad, detalle, resultado);
        }

        /// <summary>Igual que Registrar, pero indicando el usuario (para inicios de sesión fallidos, donde aún no hay sesión).</summary>
        public static void RegistrarComo(int? idUsuario, string nombreUsuario, string accion, string entidad, int? idEntidad,
                                         string detalle, string resultado)
        {
            try
            {
                RegistroAuditoria r = new RegistroAuditoria();
                r.Fecha = DateTime.Now;
                r.IdUsuario = idUsuario;
                r.NombreUsuario = Limpiar(nombreUsuario, LargoUsuario);
                r.Accion = Limpiar(accion, LargoAccion) ?? "(sin acción)";
                r.Entidad = Limpiar(entidad, LargoEntidad);
                r.IdEntidad = idEntidad;
                r.Detalle = Limpiar(detalle, LargoDetalle);
                r.Resultado = Limpiar(resultado, LargoResultado) ?? Ok;
                _datos.Insertar(r);
            }
            catch (Exception ex)
            {
                try
                {
                    Action<Exception> aviso = AlFallar;
                    if (aviso != null) aviso(ex);
                }
                catch (Exception)
                {
                    // Auditar jamás puede romper la operación principal.
                }
            }
        }

        /// <summary>Quita saltos de línea y recorta al largo de la columna (null si queda vacío).</summary>
        internal static string Limpiar(string texto, int largo)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;
            string t = texto.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim();
            return t.Length > largo ? t.Substring(0, largo) : t;
        }

        // ---------- Consulta (solo Administrador) ----------

        private readonly AuditoriaDatos _consulta;

        public AuditoriaServicio() : this(_datos) { }

        public AuditoriaServicio(AuditoriaDatos datos)
        {
            if (datos == null) throw new ArgumentNullException("datos");
            _consulta = datos;
        }

        /// <summary>Una página de registros (más recientes primero) con el total y el tiempo de consulta.</summary>
        public ResultadoPagina<RegistroAuditoria> Buscar(FiltroAuditoria filtro, int pagina)
        {
            Permisos.ExigirAdministrador();
            ValidarFiltro(filtro);
            if (pagina < 1) pagina = 1;

            Stopwatch reloj = Stopwatch.StartNew();
            ResultadoPagina<RegistroAuditoria> r = new ResultadoPagina<RegistroAuditoria>();
            r.Total = _consulta.Contar(filtro);
            r.Pagina = Math.Min(pagina, r.TotalPaginas);
            r.Items = _consulta.Listar(filtro, r.Pagina, r.TamanoPagina);
            reloj.Stop();
            r.Milisegundos = reloj.ElapsedMilliseconds;
            return r;
        }

        /// <summary>Tabla lista para guardar como CSV con los registros que cumplen el filtro (y se audita la exportación).</summary>
        public ReporteTabla Exportar(FiltroAuditoria filtro)
        {
            Permisos.ExigirAdministrador();
            ValidarFiltro(filtro);

            List<RegistroAuditoria> lista = _consulta.ListarParaExportar(filtro, MaximoExportar);
            ReporteTabla t = new ReporteTabla("Auditoría", new[]
            {
                "Fecha", "Usuario", "Acción", "Entidad", "Id entidad", "Detalle", "Resultado"
            });
            foreach (RegistroAuditoria r in lista)
            {
                t.Filas.Add(new[]
                {
                    r.Fecha.ToString("yyyy-MM-dd HH:mm:ss"),
                    r.NombreUsuario ?? string.Empty,
                    r.Accion,
                    r.Entidad ?? string.Empty,
                    r.IdEntidad.HasValue ? r.IdEntidad.Value.ToString() : string.Empty,
                    r.Detalle ?? string.Empty,
                    r.Resultado
                });
            }
            t.Resumen = lista.Count + " registro(s)" + (lista.Count >= MaximoExportar ? " (límite de exportación alcanzado)" : string.Empty);
            Registrar(AccionAuditoria.ExportarAuditoria, "Auditoria", null, lista.Count + " registro(s) exportado(s)", Ok);
            return t;
        }

        private static void ValidarFiltro(FiltroAuditoria f)
        {
            if (f != null && f.Desde.HasValue && f.Hasta.HasValue && f.Desde.Value.Date > f.Hasta.Value.Date)
                throw new ValidacionException("La fecha \"Desde\" no puede ser posterior a \"Hasta\".", "Desde");
        }
    }
}
