using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using SIGEPOSTA.Negocio;

namespace SIGEPOSTA.Datos
{
    /// <summary>
    /// Acceso a datos de Proveedor (ADO.NET, SOLO consultas parametrizadas con tipo y tamaño explícitos).
    /// Decisiones de diseño:
    ///  - Nunca se borra un proveedor: BajaLogica pone Activo = 0 (los movimientos de inventario lo siguen referenciando).
    ///  - La búsqueda es por el INICIO del nombre o del RUC (LIKE 'texto%'), que puede usar índice.
    ///  - ExisteActivo bloquea la fila (UPDLOCK) dentro de la transacción del ingreso: nadie puede dar de baja
    ///    al proveedor entre que se valida y se guarda el movimiento.
    /// </summary>
    public class ProveedorDatos
    {
        private const int ErrorLlaveDuplicada1 = 2627; // violación de UNIQUE constraint
        private const int ErrorLlaveDuplicada2 = 2601; // violación de índice único

        private const string Columnas = "SELECT p.IdProveedor, p.Nombre, p.Ruc, p.Telefono, p.Correo, p.Direccion, p.Activo ";
        private const string Desde = "FROM Proveedor p WHERE p.Activo = 1 ";
        private const string FiltroNombre = "AND p.Nombre LIKE @p ESCAPE '\\' ";
        private const string FiltroNombreORuc = "AND (p.Nombre LIKE @p ESCAPE '\\' OR p.Ruc LIKE @p ESCAPE '\\') ";
        private const string Orden = "ORDER BY p.Nombre, p.IdProveedor ";
        private const string Paginado = "OFFSET @desde ROWS FETCH NEXT @cantidad ROWS ONLY";

        private const string SqlContar0 = "SELECT COUNT(*) " + Desde;
        private const string SqlContarN = "SELECT COUNT(*) " + Desde + FiltroNombre;
        private const string SqlContarNR = "SELECT COUNT(*) " + Desde + FiltroNombreORuc;
        private const string SqlPagina0 = Columnas + Desde + Orden + Paginado;
        private const string SqlPaginaN = Columnas + Desde + FiltroNombre + Orden + Paginado;
        private const string SqlPaginaNR = Columnas + Desde + FiltroNombreORuc + Orden + Paginado;

        /// <summary>Con solo dígitos se busca por RUC o nombre; con otro texto, solo por nombre.</summary>
        private static string ElegirSql(string texto, string sin, string soloNombre, string nombreORuc)
        {
            if (!PatronBusqueda.HayTexto(texto)) return sin;
            return SoloDigitos(texto.Trim()) ? nombreORuc : soloNombre;
        }

        private static bool SoloDigitos(string t)
        {
            foreach (char c in t)
            {
                if (c < '0' || c > '9') return false;
            }
            return t.Length > 0;
        }

        public virtual int Contar(string texto)
        {
            string sql = ElegirSql(texto, SqlContar0, SqlContarN, SqlContarNR);
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(sql, cn))
            {
                AgregarFiltro(cmd, texto);
                cn.Open();
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public virtual List<Proveedor> ListarPagina(string texto, int pagina, int cantidad)
        {
            string sql = ElegirSql(texto, SqlPagina0, SqlPaginaN, SqlPaginaNR);
            List<Proveedor> lista = new List<Proveedor>();
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(sql, cn))
            {
                AgregarFiltro(cmd, texto);
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

        /// <summary>Todos los proveedores activos (para la lista desplegable del ingreso de stock).</summary>
        public virtual List<Proveedor> ListarActivos()
        {
            List<Proveedor> lista = new List<Proveedor>();
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(Columnas + Desde + Orden, cn))
            {
                cn.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read()) lista.Add(Mapear(r));
                }
            }
            return lista;
        }

        /// <summary>¿Hay otro proveedor (activo o dado de baja) con ese RUC? excluirId = el que se está editando (0 si es nuevo).</summary>
        public virtual bool ExisteRuc(string ruc, int excluirId)
        {
            const string sql = "SELECT COUNT(*) FROM Proveedor WHERE Ruc = @ruc AND IdProveedor <> @id";
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(sql, cn))
            {
                cmd.Parameters.Add("@ruc", SqlDbType.Char, 11).Value = ruc;
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = excluirId;
                cn.Open();
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        /// <summary>Inserta el proveedor (activo) y devuelve su IdProveedor.</summary>
        public virtual int Insertar(Proveedor p)
        {
            if (p == null) throw new ArgumentNullException("p");
            const string sql =
                "INSERT INTO Proveedor (Nombre, Ruc, Telefono, Correo, Direccion, Activo, FechaRegistro) " +
                "OUTPUT INSERTED.IdProveedor " +
                "VALUES (@nom, @ruc, @tel, @correo, @dir, 1, SYSDATETIME())";
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(sql, cn))
            {
                AgregarParametros(cmd, p);
                cn.Open();
                try
                {
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
                catch (SqlException ex)
                {
                    if (EsRucDuplicado(ex)) throw new ValidacionException("Ya existe un proveedor con ese RUC", "Ruc");
                    throw;
                }
            }
        }

        public virtual void Actualizar(Proveedor p)
        {
            if (p == null) throw new ArgumentNullException("p");
            const string sql =
                "UPDATE Proveedor SET Nombre = @nom, Ruc = @ruc, Telefono = @tel, Correo = @correo, Direccion = @dir " +
                "WHERE IdProveedor = @id AND Activo = 1";
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(sql, cn))
            {
                AgregarParametros(cmd, p);
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = p.IdProveedor;
                cn.Open();
                try
                {
                    cmd.ExecuteNonQuery();
                }
                catch (SqlException ex)
                {
                    if (EsRucDuplicado(ex)) throw new ValidacionException("Ya existe un proveedor con ese RUC", "Ruc");
                    throw;
                }
            }
        }

        /// <summary>Baja lógica: Activo = 0. Nunca se elimina la fila ni se tocan los movimientos que lo usaron.</summary>
        public virtual void BajaLogica(int idProveedor)
        {
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando("UPDATE Proveedor SET Activo = 0 WHERE IdProveedor = @id", cn))
            {
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = idProveedor;
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// ¿Existe y está activo? Se ejecuta DENTRO de la transacción del ingreso y bloquea la fila hasta el final de
        /// la transacción, para que no la den de baja mientras se guarda el movimiento.
        /// </summary>
        public virtual bool ExisteActivo(int idProveedor, SqlConnection cn, SqlTransaction tx)
        {
            const string sql = "SELECT COUNT(*) FROM Proveedor WITH (UPDLOCK, ROWLOCK) WHERE IdProveedor = @id AND Activo = 1";
            using (SqlCommand cmd = Conexion.Comando(sql, cn, tx))
            {
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = idProveedor;
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        /// <summary>Cuántos movimientos de inventario usaron este proveedor (informativo: la baja se permite igual).</summary>
        public virtual int ContarMovimientos(int idProveedor)
        {
            const string sql = "SELECT COUNT(*) FROM MovimientoInventario WHERE IdProveedor = @id";
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(sql, cn))
            {
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = idProveedor;
                cn.Open();
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        // ---------- Auxiliares ----------

        public static bool EsRucDuplicado(SqlException ex)
        {
            return ex != null && (ex.Number == ErrorLlaveDuplicada1 || ex.Number == ErrorLlaveDuplicada2);
        }

        private static void AgregarFiltro(SqlCommand cmd, string texto)
        {
            if (PatronBusqueda.HayTexto(texto))
                cmd.Parameters.Add("@p", SqlDbType.NVarChar, 200).Value = PatronBusqueda.Prefijo(texto);
        }

        private static void AgregarParametros(SqlCommand cmd, Proveedor p)
        {
            cmd.Parameters.Add("@nom", SqlDbType.NVarChar, Proveedor.MaxNombre).Value = p.Nombre;
            cmd.Parameters.Add("@ruc", SqlDbType.Char, 11).Value = p.Ruc;
            cmd.Parameters.Add("@tel", SqlDbType.NVarChar, 20).Value = Opcional(p.Telefono);
            cmd.Parameters.Add("@correo", SqlDbType.NVarChar, Proveedor.MaxCorreo).Value = Opcional(p.Correo);
            cmd.Parameters.Add("@dir", SqlDbType.NVarChar, Proveedor.MaxDireccion).Value = Opcional(p.Direccion);
        }

        private static object Opcional(string texto)
        {
            return string.IsNullOrEmpty(texto) ? (object)DBNull.Value : texto;
        }

        private static Proveedor Mapear(SqlDataReader r)
        {
            return Proveedor.Cargar(r.GetInt32(0), r.GetString(1), r.GetString(2).Trim(),
                r.IsDBNull(3) ? string.Empty : r.GetString(3),
                r.IsDBNull(4) ? string.Empty : r.GetString(4),
                r.IsDBNull(5) ? string.Empty : r.GetString(5),
                r.GetBoolean(6));
        }
    }
}
