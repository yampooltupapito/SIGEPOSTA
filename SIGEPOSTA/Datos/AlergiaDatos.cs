using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using SIGEPOSTA.Negocio;

namespace SIGEPOSTA.Datos
{
    /// <summary>
    /// Acceso a datos de AlergiaPaciente (ADO.NET, SOLO consultas parametrizadas).
    /// Decisión de diseño: una alergia NUNCA se borra; Desactivar pone Activo = 0 y la fila se conserva.
    /// </summary>
    public class AlergiaDatos
    {
        private const string Columnas =
            "SELECT a.IdAlergia, a.IdPaciente, a.Sustancia, a.Gravedad, a.Observacion, a.Activo, a.Fecha ";

        // Primero las activas, luego las más graves y por último por nombre.
        private const string SqlPorPaciente =
            Columnas + "FROM AlergiaPaciente a WHERE a.IdPaciente = @pac " +
            "ORDER BY a.Activo DESC, CASE a.Gravedad WHEN N'Grave' THEN 0 WHEN N'Moderada' THEN 1 ELSE 2 END, a.Sustancia";

        // Alergias ACTIVAS del paciente dueño de una historia (se usa DENTRO de la transacción de la receta).
        private const string SqlActivasPorHistoria =
            Columnas + "FROM AlergiaPaciente a INNER JOIN HistoriaClinica h ON h.IdPaciente = a.IdPaciente " +
            "WHERE h.IdHistoria = @hist AND a.Activo = 1";

        /// <summary>Todas las alergias del paciente (activas e inactivas).</summary>
        public virtual List<Alergia> ListarPorPaciente(int idPaciente)
        {
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(SqlPorPaciente, cn))
            {
                cmd.Parameters.Add("@pac", SqlDbType.Int).Value = idPaciente;
                cn.Open();
                return Leer(cmd);
            }
        }

        /// <summary>Alergias activas del paciente de la historia, dentro de la transacción recibida.</summary>
        public virtual List<Alergia> ListarActivasPorHistoria(int idHistoria, SqlConnection cn, SqlTransaction tx)
        {
            using (SqlCommand cmd = Conexion.Comando(SqlActivasPorHistoria, cn, tx))
            {
                cmd.Parameters.Add("@hist", SqlDbType.Int).Value = idHistoria;
                return Leer(cmd);
            }
        }

        public virtual int Insertar(Alergia a)
        {
            if (a == null) throw new ArgumentNullException("a");

            const string sql =
                "INSERT INTO AlergiaPaciente (IdPaciente, Sustancia, Gravedad, Observacion, Activo, Fecha) " +
                "OUTPUT INSERTED.IdAlergia VALUES (@pac, @sus, @grav, @obs, 1, SYSDATETIME())";

            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(sql, cn))
            {
                cmd.Parameters.Add("@pac", SqlDbType.Int).Value = a.IdPaciente;
                cmd.Parameters.Add("@sus", SqlDbType.NVarChar, 100).Value = a.Sustancia;
                cmd.Parameters.Add("@grav", SqlDbType.NVarChar, 10).Value = a.Gravedad;
                cmd.Parameters.Add("@obs", SqlDbType.NVarChar, 200).Value =
                    string.IsNullOrEmpty(a.Observacion) ? (object)DBNull.Value : a.Observacion;
                cn.Open();
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        /// <summary>Desactiva la alergia (Activo = 0). Nunca se elimina la fila.</summary>
        public virtual void Desactivar(int idAlergia)
        {
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando("UPDATE AlergiaPaciente SET Activo = 0 WHERE IdAlergia = @id", cn))
            {
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = idAlergia;
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        private static List<Alergia> Leer(SqlCommand cmd)
        {
            List<Alergia> lista = new List<Alergia>();
            using (SqlDataReader r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    Alergia a = new Alergia();
                    a.IdAlergia = r.GetInt32(0);
                    a.IdPaciente = r.GetInt32(1);
                    a.Sustancia = r.GetString(2);
                    a.Gravedad = r.GetString(3).Trim();
                    a.Observacion = r.IsDBNull(4) ? string.Empty : r.GetString(4);
                    a.Activo = r.GetBoolean(5);
                    a.Fecha = r.GetDateTime(6);
                    lista.Add(a);
                }
            }
            return lista;
        }
    }
}
