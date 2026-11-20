using System;
using System.Configuration;
using System.Data.SqlClient;

namespace SIGEPOSTA.Datos
{
    /// <summary>
    /// Punto único de acceso a la conexión con SQL Server.
    /// Decisión de diseño: la cadena vive en App.config (no en el código) y usa Autenticación de Windows,
    /// por lo que no existe ninguna contraseña de base de datos que filtrar.
    /// </summary>
    public static class Conexion
    {
        private const string NombreCadena = "SIGEPOSTA";

        public static string ObtenerCadena()
        {
            ConnectionStringSettings cfg = ConfigurationManager.ConnectionStrings[NombreCadena];
            if (cfg == null || string.IsNullOrWhiteSpace(cfg.ConnectionString))
                throw new InvalidOperationException("Falta la cadena de conexión '" + NombreCadena + "' en App.config.");
            return cfg.ConnectionString;
        }

        /// <summary>Devuelve una conexión NUEVA y cerrada. Quien la pida debe cerrarla con using.</summary>
        public static SqlConnection Crear()
        {
            return new SqlConnection(ObtenerCadena());
        }

        private const string PrefijoSinConteo = "SET NOCOUNT ON; ";

        /// <summary>
        /// Crea un comando con SET NOCOUNT ON: SQL Server deja de enviar el mensaje "N filas afectadas" tras cada
        /// instrucción (menos tráfico y menos trabajo, sobre todo en lotes con varias instrucciones).
        /// ATENCIÓN: con NOCOUNT ON, ExecuteNonQuery devuelve -1 en lugar de las filas afectadas. Por eso los
        /// UPDATE cuyo número de filas SÍ se revisa (descontar stock, cambiar rol/estado, restablecer contraseña)
        /// se crean con "new SqlCommand" directamente, sin este prefijo.
        /// </summary>
        public static SqlCommand Comando(string sql, SqlConnection cn)
        {
            return new SqlCommand(PrefijoSinConteo + sql, cn);
        }

        public static SqlCommand Comando(string sql, SqlConnection cn, SqlTransaction tx)
        {
            return new SqlCommand(PrefijoSinConteo + sql, cn, tx);
        }
    }
}
