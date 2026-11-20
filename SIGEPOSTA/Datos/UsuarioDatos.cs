using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using SIGEPOSTA.Negocio;

namespace SIGEPOSTA.Datos
{
    /// <summary>
    /// Acceso a datos de la tabla Usuario con ADO.NET.
    /// Decisiones de diseño:
    ///  - TODAS las consultas son parametrizadas (nunca se concatena texto del usuario en el SQL).
    ///  - Las reglas "no desactivar/degradar al último administrador activo" se repiten DENTRO del
    ///    UPDATE (condición EXISTS). Así, aunque dos administradores lo intenten a la vez, la base de datos
    ///    no permite dejar el sistema sin administrador; la capa de negocio solo da mensajes más claros.
    /// </summary>
    public class UsuarioDatos
    {
        private const int ErrorLlaveDuplicada1 = 2627; // violación de UNIQUE constraint
        private const int ErrorLlaveDuplicada2 = 2601; // violación de índice único
        private const int IdRolAdministrador = (int)Rol.Administrador;

        private const string SelectBase =
            "SELECT u.IdUsuario, u.NombreUsuario, u.NombreCompleto, u.PasswordHash, u.Salt, u.Iteraciones, " +
            "       u.IdRol, r.Nombre, u.Activo, u.IntentosFallidos, u.BloqueadoHasta " +
            "FROM Usuario u INNER JOIN Rol r ON r.IdRol = u.IdRol ";

        /// <summary>Devuelve el usuario (con su rol) o null si no existe.</summary>
        public Usuario ObtenerPorNombre(string nombreUsuario)
        {
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(SelectBase + "WHERE u.NombreUsuario = @nombre", cn))
            {
                cmd.Parameters.Add("@nombre", SqlDbType.NVarChar, 50).Value = nombreUsuario;
                cn.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    return r.Read() ? Mapear(r) : null;
                }
            }
        }

        public Usuario ObtenerPorId(int idUsuario)
        {
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(SelectBase + "WHERE u.IdUsuario = @id", cn))
            {
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = idUsuario;
                cn.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    return r.Read() ? Mapear(r) : null;
                }
            }
        }

        /// <summary>Todos los usuarios (activos e inactivos), ordenados por nombre de usuario.</summary>
        public List<Usuario> Listar()
        {
            List<Usuario> lista = new List<Usuario>();
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(SelectBase + "ORDER BY u.NombreUsuario", cn))
            {
                cn.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read()) lista.Add(Mapear(r));
                }
            }
            return lista;
        }

        public int ContarUsuarios()
        {
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando("SELECT COUNT(*) FROM Usuario", cn))
            {
                cn.Open();
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public int ContarAdministradoresActivos()
        {
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando("SELECT COUNT(*) FROM Usuario WHERE IdRol = @rol AND Activo = 1", cn))
            {
                cmd.Parameters.Add("@rol", SqlDbType.Int).Value = IdRolAdministrador;
                cn.Open();
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        /// <summary>Inserta el usuario y devuelve su nuevo IdUsuario.</summary>
        public int Crear(Usuario u)
        {
            if (u == null) throw new ArgumentNullException("u");

            const string sql =
                "INSERT INTO Usuario (NombreUsuario, NombreCompleto, PasswordHash, Salt, Iteraciones, IdRol, " +
                "                     Activo, IntentosFallidos, BloqueadoHasta, FechaCreacion) " +
                "OUTPUT INSERTED.IdUsuario " +
                "VALUES (@nombre, @completo, @hash, @salt, @iter, @rol, @activo, 0, NULL, SYSDATETIME())";

            try
            {
                using (SqlConnection cn = Conexion.Crear())
                using (SqlCommand cmd = Conexion.Comando(sql, cn))
                {
                    cmd.Parameters.Add("@nombre", SqlDbType.NVarChar, 50).Value = u.NombreUsuario;
                    cmd.Parameters.Add("@completo", SqlDbType.NVarChar, 120).Value = u.NombreCompleto;
                    cmd.Parameters.Add("@hash", SqlDbType.VarBinary, 32).Value = u.PasswordHash;
                    cmd.Parameters.Add("@salt", SqlDbType.VarBinary, 16).Value = u.Salt;
                    cmd.Parameters.Add("@iter", SqlDbType.Int).Value = u.Iteraciones;
                    cmd.Parameters.Add("@rol", SqlDbType.Int).Value = (int)u.Rol;
                    cmd.Parameters.Add("@activo", SqlDbType.Bit).Value = u.Activo;
                    cn.Open();
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == ErrorLlaveDuplicada1 || ex.Number == ErrorLlaveDuplicada2)
                    throw new ReglaNegocioException("Ya existe un usuario con ese nombre de usuario.");
                throw;
            }
        }

        /// <summary>
        /// Cambia el rol. Devuelve las filas afectadas: 0 si el usuario no existe o si el cambio dejaría
        /// al sistema sin ningún administrador activo (la condición está dentro del UPDATE).
        /// </summary>
        public int CambiarRol(int idUsuario, Rol nuevoRol)
        {
            const string sql =
                "UPDATE Usuario SET IdRol = @rol WHERE IdUsuario = @id AND " +
                "(@rol = @admin OR IdRol <> @admin OR Activo = 0 OR " +
                " EXISTS (SELECT 1 FROM Usuario o WHERE o.IdRol = @admin AND o.Activo = 1 AND o.IdUsuario <> @id))";

            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = new SqlCommand(sql, cn))
            {
                cmd.Parameters.Add("@rol", SqlDbType.Int).Value = (int)nuevoRol;
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = idUsuario;
                cmd.Parameters.Add("@admin", SqlDbType.Int).Value = IdRolAdministrador;
                cn.Open();
                return cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Activa o desactiva. Devuelve las filas afectadas: 0 si el usuario no existe o si desactivarlo
        /// dejaría al sistema sin administrador activo (la condición está dentro del UPDATE).
        /// </summary>
        public int CambiarActivo(int idUsuario, bool activo)
        {
            const string sql =
                "UPDATE Usuario SET Activo = @activo WHERE IdUsuario = @id AND " +
                "(@activo = 1 OR IdRol <> @admin OR Activo = 0 OR " +
                " EXISTS (SELECT 1 FROM Usuario o WHERE o.IdRol = @admin AND o.Activo = 1 AND o.IdUsuario <> @id))";

            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = new SqlCommand(sql, cn))
            {
                cmd.Parameters.Add("@activo", SqlDbType.Bit).Value = activo;
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = idUsuario;
                cmd.Parameters.Add("@admin", SqlDbType.Int).Value = IdRolAdministrador;
                cn.Open();
                return cmd.ExecuteNonQuery();
            }
        }

        /// <summary>Guarda un hash/salt NUEVOS y levanta cualquier bloqueo por intentos fallidos.</summary>
        public int RestablecerContrasena(int idUsuario, byte[] hash, byte[] salt, int iteraciones)
        {
            const string sql =
                "UPDATE Usuario SET PasswordHash = @hash, Salt = @salt, Iteraciones = @iter, " +
                "IntentosFallidos = 0, BloqueadoHasta = NULL WHERE IdUsuario = @id";

            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = new SqlCommand(sql, cn))
            {
                cmd.Parameters.Add("@hash", SqlDbType.VarBinary, 32).Value = hash;
                cmd.Parameters.Add("@salt", SqlDbType.VarBinary, 16).Value = salt;
                cmd.Parameters.Add("@iter", SqlDbType.Int).Value = iteraciones;
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = idUsuario;
                cn.Open();
                return cmd.ExecuteNonQuery();
            }
        }

        /// <summary>Suma un intento fallido y devuelve el total acumulado.</summary>
        public int RegistrarIntentoFallido(int idUsuario)
        {
            const string sql =
                "UPDATE Usuario SET IntentosFallidos = IntentosFallidos + 1 WHERE IdUsuario = @id; " +
                "SELECT IntentosFallidos FROM Usuario WHERE IdUsuario = @id;";

            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(sql, cn))
            {
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = idUsuario;
                cn.Open();
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        /// <summary>Bloquea la cuenta hasta la fecha indicada y reinicia el contador de intentos.</summary>
        public void BloquearHasta(int idUsuario, DateTime hasta)
        {
            const string sql =
                "UPDATE Usuario SET BloqueadoHasta = @hasta, IntentosFallidos = 0 WHERE IdUsuario = @id";

            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(sql, cn))
            {
                cmd.Parameters.Add("@hasta", SqlDbType.DateTime2).Value = hasta;
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = idUsuario;
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>Pone en cero los intentos fallidos y quita el bloqueo (tras un login correcto).</summary>
        public void ReiniciarIntentos(int idUsuario)
        {
            const string sql =
                "UPDATE Usuario SET IntentosFallidos = 0, BloqueadoHasta = NULL WHERE IdUsuario = @id";

            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(sql, cn))
            {
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = idUsuario;
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        private static Usuario Mapear(SqlDataReader r)
        {
            Usuario u = new Usuario();
            u.IdUsuario = r.GetInt32(0);
            u.NombreUsuario = r.GetString(1);
            u.NombreCompleto = r.GetString(2);
            u.PasswordHash = (byte[])r[3];
            u.Salt = (byte[])r[4];
            u.Iteraciones = r.GetInt32(5);
            u.Rol = (Rol)r.GetInt32(6);
            u.RolNombre = r.GetString(7);
            u.Activo = r.GetBoolean(8);
            u.IntentosFallidos = r.GetInt32(9);
            u.BloqueadoHasta = r.IsDBNull(10) ? (DateTime?)null : r.GetDateTime(10);
            return u;
        }
    }
}
