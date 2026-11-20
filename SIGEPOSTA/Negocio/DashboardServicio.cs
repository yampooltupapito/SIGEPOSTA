using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SIGEPOSTA.Datos;

namespace SIGEPOSTA.Negocio
{
    /// <summary>
    /// Arma los datos del panel de inicio según el ROL del usuario.
    /// Decisiones de diseño:
    ///  - Los permisos se validan AQUÍ (no solo ocultando controles): el bloque de pacientes/atenciones
    ///    solo se consulta si el rol es Administrador o Médico, y el de inventario solo si es Administrador
    ///    o Farmacia. Un rol sin permiso ni siquiera ejecuta esas consultas.
    ///  - Cada bloque se carga por separado: si falla la consulta de inventario, los datos de pacientes
    ///    igual se muestran (y al revés). El error técnico se guarda aparte; la pantalla jamás lo muestra.
    ///  - El conteo y la agrupación los hace SQL (DashboardDatos); aquí, con LINQ, solo se completan los
    ///    días sin atenciones con cero y se ordenan/recortan los resultados.
    /// </summary>
    public class DashboardServicio
    {
        public const int CantidadAlertas = 5;
        public const int DiasDelGrafico = 7;

        private readonly DashboardDatos _datos;

        public DashboardServicio() : this(new DashboardDatos()) { }

        public DashboardServicio(DashboardDatos datos)
        {
            if (datos == null) throw new ArgumentNullException("datos");
            _datos = datos;
        }

        /// <summary>
        /// Carga todo lo que el rol actual puede ver. Lanza AccesoDenegadoException solo si no hay sesión.
        /// Los fallos de base de datos NO se lanzan: se anotan en ErrorPacientes / ErrorInventario.
        /// </summary>
        public DatosDashboard Cargar()
        {
            Permisos.ExigirAccesoDashboard();

            Stopwatch reloj = Stopwatch.StartNew();
            DatosDashboard d = new DatosDashboard();
            d.VePacientes = Permisos.PuedeAccederPacientes;     // Administrador y Médico
            d.VeInventario = Permisos.PuedeAccederInventario;   // Administrador y Farmacia

            if (d.VePacientes)
            {
                try { CargarBloquePacientes(d); }
                catch (AccesoDenegadoException) { throw; }
                catch (Exception ex)
                {
                    d.ErrorPacientes = true;
                    if (d.ErrorTecnico == null) d.ErrorTecnico = ex;
                }
            }

            if (d.VeInventario)
            {
                try { CargarBloqueInventario(d); }
                catch (AccesoDenegadoException) { throw; }
                catch (Exception ex)
                {
                    d.ErrorInventario = true;
                    if (d.ErrorTecnico == null) d.ErrorTecnico = ex;
                }
            }

            reloj.Stop();
            d.Milisegundos = reloj.ElapsedMilliseconds;
            d.ActualizadoEn = DateTime.Now;
            return d;
        }

        /// <summary>
        /// Indicadores y gráficos de pacientes y atenciones. Exige Administrador o Médico
        /// (por eso un Farmacia que llame a este método directamente es rechazado).
        /// </summary>
        public void CargarBloquePacientes(DatosDashboard d)
        {
            Permisos.ExigirAccesoPacientes();

            DateTime hoy = DateTime.Today;
            DateTime manana = hoy.AddDays(1);
            DateTime inicioMes = new DateTime(hoy.Year, hoy.Month, 1);

            ResumenPacientesFila r = _datos.ObtenerResumenPacientes(hoy, inicioMes, manana);
            d.PacientesActivos = r.PacientesActivos;
            d.AtencionesHoy = r.AtencionesHoy;
            d.AtencionesMes = r.AtencionesMes;

            // Una sola consulta GROUP BY por día alimenta los DOS gráficos (mes y últimos 7 días):
            // se pide desde el más antiguo de los dos rangos. Los días sin atenciones no vienen de SQL y se completan con 0.
            DateTime desde7 = hoy.AddDays(-(DiasDelGrafico - 1));
            DateTime desde = inicioMes < desde7 ? inicioMes : desde7;
            Dictionary<DateTime, int> porDia = _datos.ObtenerAtencionesPorDia(desde, manana);
            Func<DateTime, int> valorDelDia = dia => porDia.ContainsKey(dia) ? porDia[dia] : 0;

            // Atenciones por día del mes (del día 1 hasta hoy).
            d.AtencionesPorDiaDelMes = Enumerable.Range(0, (hoy - inicioMes).Days + 1)
                .Select(i => inicioMes.AddDays(i))
                .Select(dia => new PuntoDato(dia.Day.ToString(), valorDelDia(dia)))
                .ToList();

            // Últimos 7 días (incluido hoy).
            d.AtencionesUltimos7Dias = Enumerable.Range(0, DiasDelGrafico)
                .Select(i => desde7.AddDays(i))
                .Select(dia => new PuntoDato(dia.ToString("dd/MM"), valorDelDia(dia)))
                .ToList();
        }

        /// <summary>
        /// Indicadores de inventario y los 5 medicamentos más críticos. Exige Administrador o Farmacia
        /// (un Médico que llame a este método directamente es rechazado).
        /// </summary>
        public void CargarBloqueInventario(DatosDashboard d)
        {
            Permisos.ExigirAccesoInventario();

            DateTime hoy = DateTime.Today;
            DateTime limite = hoy.AddDays(Medicamento.DiasAlertaVencimiento);

            ResumenInventarioFila r = _datos.ObtenerResumenInventario(hoy, limite);
            d.StockBajo = r.StockBajo;
            d.PorVencer = r.PorVencer;
            d.Vencidos = r.Vencidos;

            d.Alertas = _datos.ObtenerMedicamentosCriticos(hoy, limite, CantidadAlertas)
                .OrderByDescending(m => m.EstaVencido)
                .ThenByDescending(m => m.EstaPorVencer)
                .ThenBy(m => m.Stock)
                .ToList();
        }
    }
}
