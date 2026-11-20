using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using SIGEPOSTA.Negocio;

namespace SIGEPOSTA.Datos
{
    /// <summary>
    /// Acceso a la tabla Auditoria. Decisiones de diseño:
    ///  - SOLO se inserta y se consulta: no existe ningún UPDATE ni DELETE (y además la base tiene un trigger que los impide).
    ///  - Todas las consultas son parametrizadas. Los filtros (fecha, usuario por el inicio del nombre, acción exacta)
    ///    solo agregan condiciones con texto fijo; los valores viajan como parámetros tipados.
    ///  - La lista usa COUNT aparte + OFFSET/FETCH, igual que Pacientes e Inventario.
    /// Métodos virtual para poder sustituirlos por dobles de prueba (sin base de datos).
    /// </summary>
    public class AuditoriaDatos
    {
        private const string Columnas = "IdAuditoria, Fecha, IdUsuario, NombreUsuario, Accion, Entidad, IdEntidad, Detalle, Resultado";

        public virtual void Insertar(RegistroAuditoria r)
        {
            const string sql =
                "INSERT INTO Auditoria (Fecha, IdUsuario, NombreUsuario, Accion, Entidad, IdEntidad, Detalle, Resultado) " +
                "VALUES (@fecha, @idUsuario, @nombre, @accion, @entidad, @idEntidad, @detalle, @resultado)";
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(sql, cn))
            {
                cmd.Parameters.Add("@fecha", SqlDbType.DateTime).Value = r.Fecha;
                cmd.Parameters.Add("@idUsuario", SqlDbType.Int).Value = r.IdUsuario.HasValue ? (object)r.IdUsuario.Value : DBNull.Value;
                cmd.Parameters.Add("@nombre", SqlDbType.NVarChar, 50).Value = Nulo(r.NombreUsuario);
                cmd.Parameters.Add("@accion", SqlDbType.NVarChar, 50).Value = r.Accion;
                cmd.Parameters.Add("@entidad", SqlDbType.NVarChar, 50).Value = Nulo(r.Entidad);
                cmd.Parameters.Add("@idEntidad", SqlDbType.Int).Value = r.IdEntidad.HasValue ? (object)r.IdEntidad.Value : DBNull.Value;
                cmd.Parameters.Add("@detalle", SqlDbType.NVarChar, 200).Value = Nulo(r.Detalle);
                cmd.Parameters.Add("@resultado", SqlDbType.NVarChar, 30).Value = r.Resultado;
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public virtual int Contar(FiltroAuditoria f)
        {
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = new SqlCommand(string.Empty, cn))
            {
                cmd.CommandText = "SELECT COUNT(*) FROM Auditoria" + AgregarFiltros(cmd, f);
                cn.Open();
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        /// <summary>Una página de registros, los más recientes primero.</summary>
        public virtual List<RegistroAuditoria> Listar(FiltroAuditoria f, int pagina, int tamano)
        {
            if (pagina < 1) pagina = 1;
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = new SqlCommand(string.Empty, cn))
            {
                cmd.CommandText = "SELECT " + Columnas + " FROM Auditoria" + AgregarFiltros(cmd, f) +
                                  " ORDER BY Fecha DESC, IdAuditoria DESC OFFSET @salto ROWS FETCH NEXT @tamano ROWS ONLY";
                cmd.Parameters.Add("@salto", SqlDbType.Int).Value = (pagina - 1) * tamano;
                cmd.Parameters.Add("@tamano", SqlDbType.Int).Value = tamano;
                return Leer(cmd, cn);
            }
        }

        /// <summary>Hasta <paramref name="maximo"/> registros que cumplen el filtro (para exportar a CSV).</summary>
        public virtual List<RegistroAuditoria> ListarParaExportar(FiltroAuditoria f, int maximo)
        {
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = new SqlCommand(string.Empty, cn))
            {
                cmd.CommandText = "SELECT TOP (@maximo) " + Columnas + " FROM Auditoria" + AgregarFiltros(cmd, f) +
                                  " ORDER BY Fecha DESC, IdAuditoria DESC";
                cmd.Parameters.Add("@maximo", SqlDbType.Int).Value = maximo;
                return Leer(cmd, cn);
            }
        }

        // ---------- Auxiliares ----------

        /// <summary>Devuelve " WHERE ..." (o vacío) y agrega los parámetros del filtro al comando.</summary>
        private static string AgregarFiltros(SqlCommand cmd, FiltroAuditoria f)
        {
            List<string> condiciones = new List<string>();
            if (f != null)
            {
                if (f.Desde.HasValue)
                {
                    condiciones.Add("Fecha >= @desde");
                    cmd.Parameters.Add("@desde", SqlDbType.DateTime).Value = f.Desde.Value.Date;
                }
                if (f.Hasta.HasValue)
                {
                    condiciones.Add("Fecha < @hasta");   // exclusivo: incluye todo el día "Hasta"
                    cmd.Parameters.Add("@hasta", SqlDbType.DateTime).Value = f.Hasta.Value.Date.AddDays(1);
                }
                if (PatronBusqueda.HayTexto(f.Usuario))
                {
                    condiciones.Add("NombreUsuario LIKE @usuario ESCAPE '\\'");
                    cmd.Parameters.Add("@usuario", SqlDbType.NVarChar, 200).Value = PatronBusqueda.Prefijo(f.Usuario);
                }
                if (!string.IsNullOrWhiteSpace(f.Accion))
                {
                    condiciones.Add("Accion = @accion");
                    cmd.Parameters.Add("@accion", SqlDbType.NVarChar, 50).Value = f.Accion.Trim();
                }
            }
            return condiciones.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", condiciones);
        }

        private static List<RegistroAuditoria> Leer(SqlCommand cmd, SqlConnection cn)
        {
            List<RegistroAuditoria> lista = new List<RegistroAuditoria>();
            cn.Open();
            using (SqlDataReader rd = cmd.ExecuteReader())
            {
                while (rd.Read())
                {
                    RegistroAuditoria r = new RegistroAuditoria();
                    r.IdAuditoria = Convert.ToInt64(rd.GetValue(0));
                    r.Fecha = rd.GetDateTime(1);
                    r.IdUsuario = rd.IsDBNull(2) ? (int?)null : Convert.ToInt32(rd.GetValue(2));
                    r.NombreUsuario = rd.IsDBNull(3) ? null : rd.GetString(3);
                    r.Accion = rd.GetString(4);
                    r.Entidad = rd.IsDBNull(5) ? null : rd.GetString(5);
                    r.IdEntidad = rd.IsDBNull(6) ? (int?)null : Convert.ToInt32(rd.GetValue(6));
                    r.Detalle = rd.IsDBNull(7) ? null : rd.GetString(7);
                    r.Resultado = rd.GetString(8);
                    lista.Add(r);
                }
            }
            return lista;
        }

        private static object Nulo(string texto)
        {
            return string.IsNullOrEmpty(texto) ? (object)DBNull.Value : texto;
        }
    }
}
