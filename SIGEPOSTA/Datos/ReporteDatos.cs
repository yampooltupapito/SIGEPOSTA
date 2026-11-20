using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using SIGEPOSTA.Negocio;

namespace SIGEPOSTA.Datos
{
    /// <summary>Fila de detalle de una atención (para agrupar por fecha con LINQ).</summary>
    public class AtencionReporteFila
    {
        public int IdAtencion { get; set; }
        public DateTime Fecha { get; set; }
        public int IdPaciente { get; set; }
    }

    /// <summary>Una atención con su código CIE-10 todavía CIFRADO (null = atención antigua sin código).</summary>
    public class DiagnosticoReporteFila
    {
        public DateTime Fecha { get; set; }
        public byte[] Cie10Cifrado { get; set; }
    }

    /// <summary>Fila de detalle de un movimiento de inventario con el nombre del medicamento.</summary>
    public class MovimientoReporteFila
    {
        public DateTime Fecha { get; set; }
        public string Medicamento { get; set; }
        public string Presentacion { get; set; }
        public string Concentracion { get; set; }
        public string Forma { get; set; }
        public string Unidad { get; set; }
        public TipoMovimiento Tipo { get; set; }
        public int Cantidad { get; set; }
        public string Motivo { get; set; }
        public string Usuario { get; set; }
    }

    /// <summary>
    /// Consultas de solo lectura para los reportes (ADO.NET, SOLO consultas parametrizadas).
    /// Decisión de diseño: aquí solo se traen las filas de detalle del rango; el conteo y la agrupación
    /// se hacen en la capa de negocio con LINQ sobre List&lt;T&gt;.
    /// El rango es [desde, hastaExclusivo): así un día completo queda incluido sin depender de la hora.
    /// Como el sistema es de un solo establecimiento, los reportes se filtran por RANGO DE FECHAS (ya no por posta).
    /// </summary>
    public class ReporteDatos
    {
        private const string SqlAtenciones =
            "SELECT a.IdAtencion, a.Fecha, h.IdPaciente " +
            "FROM Atencion a " +
            "INNER JOIN HistoriaClinica h ON h.IdHistoria = a.IdHistoria " +
            "WHERE a.Fecha >= @desde AND a.Fecha < @hasta";

        private const string SqlMovimientos =
            "SELECT mv.Fecha, m.Nombre, m.Presentacion, mv.Tipo, mv.Cantidad, mv.Motivo, u.NombreCompleto, " +
            "       m.Concentracion, ff.Nombre, um.Nombre " +
            "FROM MovimientoInventario mv " +
            "INNER JOIN Medicamento m ON m.IdMedicamento = mv.IdMedicamento " +
            "LEFT JOIN FormaFarmaceutica ff ON ff.IdForma = m.IdForma " +
            "LEFT JOIN UnidadMedida um ON um.IdUnidad = m.IdUnidad " +
            "LEFT JOIN Usuario u ON u.IdUsuario = mv.IdUsuario " +
            "WHERE mv.Fecha >= @desde AND mv.Fecha < @hasta";

        public virtual List<AtencionReporteFila> ListarAtenciones(DateTime desde, DateTime hastaExclusivo)
        {
            List<AtencionReporteFila> lista = new List<AtencionReporteFila>();
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(SqlAtenciones, cn))
            {
                cmd.Parameters.Add("@desde", SqlDbType.DateTime2).Value = desde;
                cmd.Parameters.Add("@hasta", SqlDbType.DateTime2).Value = hastaExclusivo;
                cn.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        AtencionReporteFila f = new AtencionReporteFila();
                        f.IdAtencion = r.GetInt32(0);
                        f.Fecha = r.GetDateTime(1);
                        f.IdPaciente = r.GetInt32(2);
                        lista.Add(f);
                    }
                }
            }
            return lista;
        }

        /// <summary>
        /// Atenciones del rango con su código CIE-10 CIFRADO. No se agrupa en SQL: el código está cifrado con IV aleatorio
        /// (dos atenciones con el mismo código dan bytes distintos), así que SQL no puede contarlos; se descifra en memoria.
        /// </summary>
        public virtual List<DiagnosticoReporteFila> ListarDiagnosticos(DateTime desde, DateTime hastaExclusivo)
        {
            const string sql = "SELECT a.Fecha, a.Cie10Cifrado FROM Atencion a WHERE a.Fecha >= @desde AND a.Fecha < @hasta";
            List<DiagnosticoReporteFila> lista = new List<DiagnosticoReporteFila>();
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(sql, cn))
            {
                cmd.Parameters.Add("@desde", SqlDbType.DateTime2).Value = desde;
                cmd.Parameters.Add("@hasta", SqlDbType.DateTime2).Value = hastaExclusivo;
                cn.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        DiagnosticoReporteFila f = new DiagnosticoReporteFila();
                        f.Fecha = r.GetDateTime(0);
                        f.Cie10Cifrado = r.IsDBNull(1) ? null : (byte[])r[1];
                        lista.Add(f);
                    }
                }
            }
            return lista;
        }

        public virtual List<MovimientoReporteFila> ListarMovimientos(DateTime desde, DateTime hastaExclusivo)
        {
            List<MovimientoReporteFila> lista = new List<MovimientoReporteFila>();
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(SqlMovimientos, cn))
            {
                cmd.Parameters.Add("@desde", SqlDbType.DateTime2).Value = desde;
                cmd.Parameters.Add("@hasta", SqlDbType.DateTime2).Value = hastaExclusivo;
                cn.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        MovimientoReporteFila f = new MovimientoReporteFila();
                        f.Fecha = r.GetDateTime(0);
                        f.Medicamento = r.GetString(1);
                        f.Presentacion = r.IsDBNull(2) ? string.Empty : r.GetString(2);
                        f.Tipo = string.Equals(r.GetString(3).Trim(), "Ingreso", StringComparison.OrdinalIgnoreCase)
                            ? TipoMovimiento.Ingreso : TipoMovimiento.Salida;
                        f.Cantidad = r.GetInt32(4);
                        f.Motivo = r.IsDBNull(5) ? string.Empty : r.GetString(5);
                        f.Usuario = r.IsDBNull(6) ? string.Empty : r.GetString(6);
                        f.Concentracion = r.IsDBNull(7) ? string.Empty : r.GetString(7);
                        f.Forma = r.IsDBNull(8) ? string.Empty : r.GetString(8);
                        f.Unidad = r.IsDBNull(9) ? string.Empty : r.GetString(9);
                        lista.Add(f);
                    }
                }
            }
            return lista;
        }
    }
}
