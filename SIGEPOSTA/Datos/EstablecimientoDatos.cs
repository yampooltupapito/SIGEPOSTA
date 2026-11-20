using System;
using System.Data.SqlClient;

namespace SIGEPOSTA.Datos
{
    /// <summary>
    /// Lectura del establecimiento (tabla Posta). SIGEPOSTA funciona para UN solo establecimiento, pero la
    /// tabla Posta y la columna IdPosta se conservan: así la estructura queda lista para crecer a una red de postas.
    /// Consulta de solo lectura, sin parámetros porque no recibe ningún dato del usuario.
    /// </summary>
    public class EstablecimientoDatos
    {
        // Si hubiera más de una fila, se prefiere la activa y la de menor Id (hoy solo existe una).
        private const string SqlPrimeraPosta = "SELECT TOP 1 IdPosta FROM Posta ORDER BY Activo DESC, IdPosta";

        /// <summary>Devuelve el IdPosta del establecimiento, o 0 si la tabla Posta está vacía.</summary>
        public virtual int ObtenerIdPosta()
        {
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(SqlPrimeraPosta, cn))
            {
                cn.Open();
                object o = cmd.ExecuteScalar();
                return (o == null || o == DBNull.Value) ? 0 : Convert.ToInt32(o);
            }
        }
    }
}
