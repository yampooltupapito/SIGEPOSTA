using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using SIGEPOSTA.Negocio;

namespace SIGEPOSTA.Datos
{
    /// <summary>Resultado de la consulta de indicadores de pacientes (tres COUNT en una sola consulta).</summary>
    public class ResumenPacientesFila
    {
        public int PacientesActivos { get; set; }
        public int AtencionesHoy { get; set; }
        public int AtencionesMes { get; set; }
    }

    /// <summary>Resultado de la consulta de indicadores de inventario (tres conteos en una sola consulta).</summary>
    public class ResumenInventarioFila
    {
        public int StockBajo { get; set; }
        public int PorVencer { get; set; }
        public int Vencidos { get; set; }
    }

    /// <summary>
    /// Consultas de SOLO LECTURA del panel de inicio (ADO.NET, SOLO consultas parametrizadas).
    /// Decisión de diseño: todo el conteo y la agrupación se hace en SQL (COUNT, SUM, GROUP BY, TOP):
    /// a la aplicación llegan unas pocas filas ya resumidas, nunca miles de filas para contarlas en memoria.
    /// Cada consulta es una cadena completa y constante (no se arma SQL pegando fragmentos).
    /// Los métodos son virtual para poder sustituir esta clase por un doble de prueba que no toque la base de datos.
    /// Los rangos de fecha son [desde, hastaExclusivo): así un día completo queda incluido sin depender de la hora.
    /// </summary>
    public class DashboardDatos
    {
        // Tres COUNT en una sola ida a la base de datos.
        private const string SqlResumenPacientes =
            "SELECT " +
            "  (SELECT COUNT(*) FROM Paciente WHERE Activo = 1), " +
            "  (SELECT COUNT(*) FROM Atencion WHERE Fecha >= @hoy AND Fecha < @manana), " +
            "  (SELECT COUNT(*) FROM Atencion WHERE Fecha >= @inicioMes AND Fecha < @manana)";

        // Atenciones por día (GROUP BY en SQL). Una sola consulta sirve para el gráfico del mes y el de 7 días.
        // Los días sin atenciones no aparecen: se completan con ceros en Negocio.
        private const string SqlAtencionesPorDia =
            "SELECT CAST(a.Fecha AS DATE) AS Dia, COUNT(*) AS Total " +
            "FROM Atencion a " +
            "WHERE a.Fecha >= @desde AND a.Fecha < @hasta " +
            "GROUP BY CAST(a.Fecha AS DATE) " +
            "ORDER BY CAST(a.Fecha AS DATE)";

        // Stock bajo, por vencer (hoy..30 días) y vencidos, solo de medicamentos activos. Mismas reglas que Medicamento.
        private const string SqlResumenInventario =
            "SELECT " +
            "  ISNULL(SUM(CASE WHEN StockActual <= StockMinimo THEN 1 ELSE 0 END), 0), " +
            "  ISNULL(SUM(CASE WHEN FechaVencimiento >= @hoy AND FechaVencimiento <= @limite THEN 1 ELSE 0 END), 0), " +
            "  ISNULL(SUM(CASE WHEN FechaVencimiento < @hoy THEN 1 ELSE 0 END), 0) " +
            "FROM Medicamento WHERE Activo = 1";

        // Los N más críticos: primero vencidos, luego por vencer (los que vencen antes), luego stock bajo (el menor stock).
        private const string SqlMedicamentosCriticos =
            "SELECT TOP (@n) m.IdMedicamento, m.Nombre, m.Presentacion, m.IdPosta, " +
            "       m.StockActual, m.StockMinimo, m.FechaVencimiento, m.Concentracion, f.Nombre, u.Nombre " +
            "FROM Medicamento m " +
            "LEFT JOIN FormaFarmaceutica f ON f.IdForma = m.IdForma " +
            "LEFT JOIN UnidadMedida u ON u.IdUnidad = m.IdUnidad " +
            "WHERE m.Activo = 1 AND (m.StockActual <= m.StockMinimo OR m.FechaVencimiento <= @limite) " +
            "ORDER BY CASE WHEN m.FechaVencimiento < @hoy THEN 0 WHEN m.FechaVencimiento <= @limite THEN 1 ELSE 2 END, " +
            "         m.FechaVencimiento, m.StockActual, m.Nombre";

        public virtual ResumenPacientesFila ObtenerResumenPacientes(DateTime hoy, DateTime inicioMes, DateTime manana)
        {
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(SqlResumenPacientes, cn))
            {
                cmd.Parameters.Add("@hoy", SqlDbType.DateTime2).Value = hoy;
                cmd.Parameters.Add("@inicioMes", SqlDbType.DateTime2).Value = inicioMes;
                cmd.Parameters.Add("@manana", SqlDbType.DateTime2).Value = manana;
                cn.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    ResumenPacientesFila f = new ResumenPacientesFila();
                    if (r.Read())
                    {
                        f.PacientesActivos = r.GetInt32(0);
                        f.AtencionesHoy = r.GetInt32(1);
                        f.AtencionesMes = r.GetInt32(2);
                    }
                    return f;
                }
            }
        }

        /// <summary>Atenciones por día. Devuelve (fecha, total) solo de los días que tuvieron atenciones.</summary>
        public virtual Dictionary<DateTime, int> ObtenerAtencionesPorDia(DateTime desde, DateTime hastaExclusivo)
        {
            Dictionary<DateTime, int> dias = new Dictionary<DateTime, int>();
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(SqlAtencionesPorDia, cn))
            {
                cmd.Parameters.Add("@desde", SqlDbType.DateTime2).Value = desde;
                cmd.Parameters.Add("@hasta", SqlDbType.DateTime2).Value = hastaExclusivo;
                cn.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read()) dias[r.GetDateTime(0).Date] = r.GetInt32(1);
                }
            }
            return dias;
        }

        public virtual ResumenInventarioFila ObtenerResumenInventario(DateTime hoy, DateTime limite)
        {
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(SqlResumenInventario, cn))
            {
                cmd.Parameters.Add("@hoy", SqlDbType.Date).Value = hoy.Date;
                cmd.Parameters.Add("@limite", SqlDbType.Date).Value = limite.Date;
                cn.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    ResumenInventarioFila f = new ResumenInventarioFila();
                    if (r.Read())
                    {
                        f.StockBajo = r.GetInt32(0);
                        f.PorVencer = r.GetInt32(1);
                        f.Vencidos = r.GetInt32(2);
                    }
                    return f;
                }
            }
        }

        public virtual List<Medicamento> ObtenerMedicamentosCriticos(DateTime hoy, DateTime limite, int cantidad)
        {
            List<Medicamento> lista = new List<Medicamento>();
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(SqlMedicamentosCriticos, cn))
            {
                cmd.Parameters.Add("@n", SqlDbType.Int).Value = cantidad;
                cmd.Parameters.Add("@hoy", SqlDbType.Date).Value = hoy.Date;
                cmd.Parameters.Add("@limite", SqlDbType.Date).Value = limite.Date;
                cn.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        Medicamento m = new Medicamento();
                        m.IdMedicamento = r.GetInt32(0);
                        m.Nombre = r.GetString(1);
                        m.Presentacion = r.IsDBNull(2) ? string.Empty : r.GetString(2);
                        m.IdPosta = r.IsDBNull(3) ? 0 : r.GetInt32(3);
                        m.Stock = r.GetInt32(4);
                        m.StockMinimo = r.GetInt32(5);
                        m.FechaVencimiento = r.IsDBNull(6) ? (DateTime?)null : r.GetDateTime(6);
                        Medicamento.CargarConcentracion(m, r.IsDBNull(7) ? string.Empty : r.GetString(7));
                        m.FormaNombre = r.IsDBNull(8) ? string.Empty : r.GetString(8);
                        m.UnidadNombre = r.IsDBNull(9) ? string.Empty : r.GetString(9);
                        lista.Add(m);
                    }
                }
            }
            return lista;
        }
    }
}
