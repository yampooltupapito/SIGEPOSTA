using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using SIGEPOSTA.Negocio;

namespace SIGEPOSTA.Datos
{
    /// <summary>
    /// Acceso a datos de Paciente y Posta (ADO.NET, SOLO consultas parametrizadas).
    /// Decisiones de diseño:
    ///  - Nunca se borra un paciente: BajaLogica pone Activo = 0 (se conserva el historial médico).
    ///  - Los listados NO traen DniCifrado: el DNI solo se pide (ObtenerDniCifrado) cuando se necesita.
    ///  - Insertar recibe la conexión y la transacción desde afuera para que el servicio pueda
    ///    guardar paciente + historia clínica como una sola operación atómica.
    /// </summary>
    public class PacienteDatos
    {
        // Solo las columnas que las pantallas necesitan (sin DniCifrado, DniHash ni fechas de auditoría).
        private const string Columnas =
            "SELECT p.IdPaciente, p.Nombres, p.Apellidos, p.FechaNacimiento, p.Telefono, p.Direccion, p.IdPosta, p.Activo, " +
            "       h.IdHistoria, h.NumeroHistoria ";
        private const string Desde =
            "FROM Paciente p LEFT JOIN HistoriaClinica h ON h.IdPaciente = p.IdPaciente ";
        private const string DondeActivos = "WHERE p.Activo = 1 ";
        // Búsqueda por el INICIO del apellido, del nombre o del N.º de historia (puede usar índices). Hasta 2 partes de texto.
        private const string DondeBusqueda =
            "WHERE p.Activo = 1 AND (" +
            "((p.Apellidos LIKE @p1 ESCAPE '\\' OR p.Nombres LIKE @p1 ESCAPE '\\') " +
            "AND (p.Apellidos LIKE @p2 ESCAPE '\\' OR p.Nombres LIKE @p2 ESCAPE '\\')) " +
            "OR h.NumeroHistoria LIKE @pn ESCAPE '\\') ";
        private const string Orden = "ORDER BY p.Apellidos, p.Nombres, p.IdPaciente ";
        private const string Paginado = "OFFSET @desde ROWS FETCH NEXT @cantidad ROWS ONLY";

        // Cada consulta es una cadena completa y constante (la unión de constantes se hace al compilar).
        private const string SqlContarTodos = "SELECT COUNT(*) " + Desde + DondeActivos;
        private const string SqlContarBusqueda = "SELECT COUNT(*) " + Desde + DondeBusqueda;
        private const string SqlPaginaTodos = Columnas + Desde + DondeActivos + Orden + Paginado;
        private const string SqlPaginaBusqueda = Columnas + Desde + DondeBusqueda + Orden + Paginado;

        /// <summary>Todos los pacientes activos (lo usa el respaldo; las pantallas usan ListarPagina).</summary>
        public List<Paciente> Listar()
        {
            return Consultar(Columnas + Desde + DondeActivos + Orden, null);
        }

        /// <summary>Cuántos pacientes activos cumplen la búsqueda (COUNT aparte, sin traer filas).</summary>
        public virtual int Contar(string texto)
        {
            bool conBusqueda = PatronBusqueda.HayTexto(texto);
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(conBusqueda ? SqlContarBusqueda : SqlContarTodos, cn))
            {
                if (conBusqueda) PatronBusqueda.AgregarParametrosPersona(cmd, texto);
                cn.Open();
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        /// <summary>
        /// UNA página de pacientes (OFFSET/FETCH), ordenada por apellido y nombre.
        /// pagina empieza en 1. Solo se traen las filas de esa página.
        /// </summary>
        public virtual List<Paciente> ListarPagina(string texto, int pagina, int cantidad)
        {
            bool conBusqueda = PatronBusqueda.HayTexto(texto);
            List<Paciente> lista = new List<Paciente>();
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(conBusqueda ? SqlPaginaBusqueda : SqlPaginaTodos, cn))
            {
                if (conBusqueda) PatronBusqueda.AgregarParametrosPersona(cmd, texto);
                cmd.Parameters.Add("@desde", SqlDbType.Int).Value = Math.Max(0, (pagina - 1) * cantidad);
                cmd.Parameters.Add("@cantidad", SqlDbType.Int).Value = cantidad;
                cn.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read()) lista.Add(Mapear(r));
                }
            }
            return lista;
        }

        /// <summary>Busca por el HMAC del DNI (activo o no). Devuelve null si no existe.</summary>
        public Paciente BuscarPorDniHash(string dniHash)
        {
            List<Paciente> r = Consultar(Columnas + Desde + "WHERE p.DniHash = @h", delegate (SqlCommand cmd)
            {
                cmd.Parameters.Add("@h", SqlDbType.Char, 64).Value = dniHash;
            });
            return r.Count == 0 ? null : r[0];
        }

        /// <summary>Devuelve los bytes del DNI cifrado, o null si el paciente no existe.</summary>
        public byte[] ObtenerDniCifrado(int idPaciente)
        {
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando("SELECT DniCifrado FROM Paciente WHERE IdPaciente = @id", cn))
            {
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = idPaciente;
                cn.Open();
                object o = cmd.ExecuteScalar();
                return (o == null || o == DBNull.Value) ? null : (byte[])o;
            }
        }

        /// <summary>Inserta el paciente dentro de la transacción recibida y devuelve su IdPaciente.</summary>
        public int Insertar(Paciente p, SqlConnection cn, SqlTransaction tx)
        {
            if (p == null) throw new ArgumentNullException("p");

            const string sql =
                "INSERT INTO Paciente (DniCifrado, DniHash, Nombres, Apellidos, FechaNacimiento, Telefono, Direccion, " +
                "                      IdPosta, Activo, FechaRegistro) " +
                "OUTPUT INSERTED.IdPaciente " +
                "VALUES (@dniC, @dniH, @nom, @ape, @fn, @tel, @dir, @posta, 1, SYSDATETIME())";

            using (SqlCommand cmd = Conexion.Comando(sql, cn, tx))
            {
                cmd.Parameters.Add("@dniC", SqlDbType.VarBinary, 256).Value = p.DniCifrado;
                cmd.Parameters.Add("@dniH", SqlDbType.Char, 64).Value = p.DniHash;
                cmd.Parameters.Add("@nom", SqlDbType.NVarChar, 80).Value = p.Nombres;
                cmd.Parameters.Add("@ape", SqlDbType.NVarChar, 80).Value = p.Apellidos;
                cmd.Parameters.Add("@fn", SqlDbType.Date).Value = p.FechaNacimiento.Date;
                cmd.Parameters.Add("@tel", SqlDbType.NVarChar, 20).Value = p.Telefono ?? string.Empty;
                cmd.Parameters.Add("@dir", SqlDbType.NVarChar, 150).Value = p.Direccion ?? string.Empty;
                cmd.Parameters.Add("@posta", SqlDbType.Int).Value = p.IdPosta;
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        /// <summary>Actualiza los datos editables. El DNI no se modifica aquí.</summary>
        public void Actualizar(Paciente p)
        {
            if (p == null) throw new ArgumentNullException("p");

            const string sql =
                "UPDATE Paciente SET Nombres = @nom, Apellidos = @ape, FechaNacimiento = @fn, " +
                "Telefono = @tel, Direccion = @dir WHERE IdPaciente = @id"; // la posta no se modifica al editar

            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(sql, cn))
            {
                cmd.Parameters.Add("@nom", SqlDbType.NVarChar, 80).Value = p.Nombres;
                cmd.Parameters.Add("@ape", SqlDbType.NVarChar, 80).Value = p.Apellidos;
                cmd.Parameters.Add("@fn", SqlDbType.Date).Value = p.FechaNacimiento.Date;
                cmd.Parameters.Add("@tel", SqlDbType.NVarChar, 20).Value = p.Telefono ?? string.Empty;
                cmd.Parameters.Add("@dir", SqlDbType.NVarChar, 150).Value = p.Direccion ?? string.Empty;
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = p.IdPaciente;
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>Baja lógica: Activo = 0. Nunca se elimina la fila.</summary>
        public void BajaLogica(int idPaciente)
        {
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando("UPDATE Paciente SET Activo = 0 WHERE IdPaciente = @id", cn))
            {
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = idPaciente;
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        // ---------- Auxiliares ----------

        private static List<Paciente> Consultar(string sql, Action<SqlCommand> parametros)
        {
            List<Paciente> lista = new List<Paciente>();
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(sql, cn))
            {
                if (parametros != null) parametros(cmd);
                cn.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read()) lista.Add(Mapear(r));
                }
            }
            return lista;
        }

        private static Paciente Mapear(SqlDataReader r)
        {
            Paciente p = new Paciente();
            p.IdPaciente = r.GetInt32(0);
            p.Nombres = r.GetString(1);
            p.Apellidos = r.GetString(2);
            p.FechaNacimiento = r.GetDateTime(3);
            p.Telefono = r.IsDBNull(4) ? string.Empty : r.GetString(4);
            p.Direccion = r.IsDBNull(5) ? string.Empty : r.GetString(5);
            p.IdPosta = r.IsDBNull(6) ? 0 : r.GetInt32(6);
            p.Activo = r.GetBoolean(7);
            p.IdHistoria = r.IsDBNull(8) ? 0 : r.GetInt32(8);
            p.NumeroHistoria = r.IsDBNull(9) ? string.Empty : r.GetString(9);
            return p;
        }
    }
}
