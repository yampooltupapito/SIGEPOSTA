using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using SIGEPOSTA.Negocio;

namespace SIGEPOSTA.Datos
{
    /// <summary>
    /// Acceso a datos de Medicamento y MovimientoInventario (ADO.NET, SOLO consultas parametrizadas).
    /// Decisiones de diseño:
    ///  - Nunca se borra un medicamento: BajaLogica pone Activo = 0 para conservar el historial.
    ///  - El stock NO se edita con Actualizar: solo cambia con IncrementarStock / DescontarStock,
    ///    que reciben la transacción para guardar el cambio y su movimiento como una sola operación.
    ///  - DescontarStock hace la comprobación DENTRO del UPDATE (StockActual >= @cant): es atómico
    ///    y evita que dos salidas simultáneas dejen el stock negativo. El CHECK de la tabla es la última barrera.
    /// </summary>
    public class MedicamentoDatos
    {
        private const string TextoIngreso = "Ingreso";
        private const string TextoSalida = "Salida";

        // Solo las columnas necesarias (sin el nombre de la posta: no se muestra).
        private const string Columnas =
            "SELECT m.IdMedicamento, m.Nombre, m.Presentacion, m.IdPosta, m.StockActual, m.StockMinimo, m.FechaVencimiento, m.Activo, " +
            "       m.IdForma, m.IdUnidad, m.Concentracion, f.Nombre, u.Nombre ";
        private const string Desde = "FROM Medicamento m " +
            "LEFT JOIN FormaFarmaceutica f ON f.IdForma = m.IdForma " +
            "LEFT JOIN UnidadMedida u ON u.IdUnidad = m.IdUnidad ";
        private const string DondeBase = "WHERE m.Activo = 1 AND (@forma = 0 OR m.IdForma = @forma) ";   // @forma = 0 → todas las formas
        private const string FiltroTexto = "AND m.Nombre LIKE @p ESCAPE '\\' ";   // por el INICIO del nombre (puede usar índice)
        private const string FiltroCriticos = "AND (m.StockActual <= m.StockMinimo OR m.FechaVencimiento <= @limite) ";
        private const string Orden = "ORDER BY m.Nombre, m.IdMedicamento ";
        private const string Paginado = "OFFSET @desde ROWS FETCH NEXT @cantidad ROWS ONLY";

        // Cada combinación (texto / solo críticos) es una cadena completa y constante.
        private const string SqlContar00 = "SELECT COUNT(*) " + Desde + DondeBase;
        private const string SqlContar10 = "SELECT COUNT(*) " + Desde + DondeBase + FiltroTexto;
        private const string SqlContar01 = "SELECT COUNT(*) " + Desde + DondeBase + FiltroCriticos;
        private const string SqlContar11 = "SELECT COUNT(*) " + Desde + DondeBase + FiltroTexto + FiltroCriticos;
        private const string SqlPagina00 = Columnas + Desde + DondeBase + Orden + Paginado;
        private const string SqlPagina10 = Columnas + Desde + DondeBase + FiltroTexto + Orden + Paginado;
        private const string SqlPagina01 = Columnas + Desde + DondeBase + FiltroCriticos + Orden + Paginado;
        private const string SqlPagina11 = Columnas + Desde + DondeBase + FiltroTexto + FiltroCriticos + Orden + Paginado;

        /// <summary>Todos los medicamentos activos (lo usa la receta; la pantalla de inventario usa ListarPagina).</summary>
        public List<Medicamento> Listar()
        {
            return Consultar(Columnas + Desde + DondeBase + Orden, delegate (SqlCommand cmd) { cmd.Parameters.Add("@forma", SqlDbType.Int).Value = 0; });
        }

        /// <summary>Cuántos medicamentos activos cumplen la búsqueda (COUNT aparte, sin traer filas).</summary>
        public virtual int Contar(string texto, bool soloCriticos, DateTime limiteVencimiento, int idForma)
        {
            bool t = PatronBusqueda.HayTexto(texto);
            string sql = t ? (soloCriticos ? SqlContar11 : SqlContar10) : (soloCriticos ? SqlContar01 : SqlContar00);
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(sql, cn))
            {
                AgregarFiltros(cmd, texto, soloCriticos, limiteVencimiento, idForma);
                cn.Open();
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        /// <summary>UNA página de medicamentos (OFFSET/FETCH), ordenada por nombre. pagina empieza en 1.</summary>
        public virtual List<Medicamento> ListarPagina(string texto, bool soloCriticos, DateTime limiteVencimiento, int idForma, int pagina, int cantidad)
        {
            bool t = PatronBusqueda.HayTexto(texto);
            string sql = t ? (soloCriticos ? SqlPagina11 : SqlPagina10) : (soloCriticos ? SqlPagina01 : SqlPagina00);
            List<Medicamento> lista = new List<Medicamento>();
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(sql, cn))
            {
                AgregarFiltros(cmd, texto, soloCriticos, limiteVencimiento, idForma);
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

        private static void AgregarFiltros(SqlCommand cmd, string texto, bool soloCriticos, DateTime limiteVencimiento, int idForma)
        {
            cmd.Parameters.Add("@forma", SqlDbType.Int).Value = Math.Max(0, idForma);
            if (PatronBusqueda.HayTexto(texto))
                cmd.Parameters.Add("@p", SqlDbType.NVarChar, 200).Value = PatronBusqueda.Prefijo(texto);
            if (soloCriticos)
                cmd.Parameters.Add("@limite", SqlDbType.Date).Value = limiteVencimiento.Date;
        }

        /// <summary>Inserta el medicamento dentro de la transacción recibida y devuelve su IdMedicamento.</summary>
        public int Insertar(Medicamento m, SqlConnection cn, SqlTransaction tx)
        {
            if (m == null) throw new ArgumentNullException("m");

            const string sql =
                "INSERT INTO Medicamento (Nombre, Presentacion, IdPosta, StockActual, StockMinimo, FechaVencimiento, Activo, " +
                "                         IdForma, IdUnidad, Concentracion) " +
                "OUTPUT INSERTED.IdMedicamento " +
                "VALUES (@nom, @pres, @posta, @stock, @min, @venc, 1, @forma, @unidad, @conc)";

            using (SqlCommand cmd = Conexion.Comando(sql, cn, tx))
            {
                AgregarParametrosEditables(cmd, m);
                cmd.Parameters.Add("@posta", SqlDbType.Int).Value = m.IdPosta; // solo el alta asigna la posta
                cmd.Parameters.Add("@stock", SqlDbType.Int).Value = m.Stock;
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        /// <summary>Actualiza los datos editables. El stock NO se toca aquí: solo cambia con movimientos.</summary>
        public void Actualizar(Medicamento m)
        {
            if (m == null) throw new ArgumentNullException("m");

            const string sql =
                "UPDATE Medicamento SET Nombre = @nom, Presentacion = @pres, " +
                "StockMinimo = @min, FechaVencimiento = @venc, IdForma = @forma, IdUnidad = @unidad, Concentracion = @conc " +
                "WHERE IdMedicamento = @id"; // la posta no se modifica al editar

            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(sql, cn))
            {
                AgregarParametrosEditables(cmd, m);
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = m.IdMedicamento;
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>Baja lógica: Activo = 0. Nunca se elimina la fila.</summary>
        public void BajaLogica(int idMedicamento)
        {
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando("UPDATE Medicamento SET Activo = 0 WHERE IdMedicamento = @id", cn))
            {
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = idMedicamento;
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        // Consulta completa y constante (sin armar SQL pegando texto). WITH (UPDLOCK, ROWLOCK) bloquea la fila
        // hasta el final de la transacción: nadie puede cambiar su posta, estado o stock entre que se valida y se descuenta.
        private const string SqlPorIdBloqueado =
            "SELECT m.IdMedicamento, m.Nombre, m.Presentacion, m.IdPosta, " +
            "       m.StockActual, m.StockMinimo, m.FechaVencimiento, m.Activo, " +
            "       m.IdForma, m.IdUnidad, m.Concentracion, f.Nombre, u.Nombre " +
            "FROM Medicamento m WITH (UPDLOCK, ROWLOCK) " +
            "LEFT JOIN FormaFarmaceutica f ON f.IdForma = m.IdForma " +
            "LEFT JOIN UnidadMedida u ON u.IdUnidad = m.IdUnidad " +
            "WHERE m.IdMedicamento = @id";

        /// <summary>
        /// Lee un medicamento (activo o no) DENTRO de la transacción, bloqueando su fila. Null si no existe.
        /// Se usa para validar una receta antes de descontar el stock.
        /// </summary>
        public Medicamento ObtenerPorIdParaReceta(int idMedicamento, SqlConnection cn, SqlTransaction tx)
        {
            using (SqlCommand cmd = Conexion.Comando(SqlPorIdBloqueado, cn, tx))
            {
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = idMedicamento;
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    return r.Read() ? Mapear(r) : null;
                }
            }
        }

        /// <summary>Suma stock. Devuelve las filas afectadas (0 si el medicamento no existe o está dado de baja).</summary>
        public int IncrementarStock(int idMedicamento, int cantidad, SqlConnection cn, SqlTransaction tx)
        {
            const string sql =
                "UPDATE Medicamento SET StockActual = StockActual + @cant WHERE IdMedicamento = @id AND Activo = 1";

            using (SqlCommand cmd = new SqlCommand(sql, cn, tx))
            {
                cmd.Parameters.Add("@cant", SqlDbType.Int).Value = cantidad;
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = idMedicamento;
                return cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Resta stock SOLO si alcanza. Devuelve las filas afectadas: 0 significa que no alcanzó
        /// (o que el medicamento no existe), y el stock queda exactamente como estaba.
        /// </summary>
        public int DescontarStock(int idMedicamento, int cantidad, SqlConnection cn, SqlTransaction tx)
        {
            const string sql =
                "UPDATE Medicamento SET StockActual = StockActual - @cant " +
                "WHERE IdMedicamento = @id AND StockActual >= @cant AND Activo = 1";

            using (SqlCommand cmd = new SqlCommand(sql, cn, tx))
            {
                cmd.Parameters.Add("@cant", SqlDbType.Int).Value = cantidad;
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = idMedicamento;
                return cmd.ExecuteNonQuery();
            }
        }

        /// <summary>Stock actual dentro de la transacción (null si no existe o está inactivo). Sirve para explicar un rechazo.</summary>
        public int? ObtenerStock(int idMedicamento, SqlConnection cn, SqlTransaction tx)
        {
            using (SqlCommand cmd = Conexion.Comando(
                "SELECT StockActual FROM Medicamento WHERE IdMedicamento = @id AND Activo = 1", cn, tx))
            {
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = idMedicamento;
                object o = cmd.ExecuteScalar();
                if (o == null || o == DBNull.Value) return null;
                return Convert.ToInt32(o);
            }
        }

        /// <summary>Registra el movimiento (kardex) dentro de la transacción recibida. Devuelve su IdMovimiento.</summary>
        public int InsertarMovimiento(MovimientoInventario mov, SqlConnection cn, SqlTransaction tx)
        {
            if (mov == null) throw new ArgumentNullException("mov");

            const string sql =
                "INSERT INTO MovimientoInventario (IdMedicamento, Tipo, Cantidad, Fecha, IdUsuario, Motivo, IdProveedor, NumeroDocumento) " +
                "OUTPUT INSERTED.IdMovimiento " +
                "VALUES (@med, @tipo, @cant, SYSDATETIME(), @usr, @motivo, @prov, @doc)";

            using (SqlCommand cmd = Conexion.Comando(sql, cn, tx))
            {
                cmd.Parameters.Add("@med", SqlDbType.Int).Value = mov.IdMedicamento;
                cmd.Parameters.Add("@tipo", SqlDbType.NVarChar, 10).Value =
                    mov.Tipo == TipoMovimiento.Ingreso ? TextoIngreso : TextoSalida;
                cmd.Parameters.Add("@cant", SqlDbType.Int).Value = mov.Cantidad;
                cmd.Parameters.Add("@usr", SqlDbType.Int).Value = mov.IdUsuario;
                cmd.Parameters.Add("@motivo", SqlDbType.NVarChar, 200).Value =
                    string.IsNullOrEmpty(mov.Motivo) ? (object)DBNull.Value : mov.Motivo;
                cmd.Parameters.Add("@prov", SqlDbType.Int).Value =
                    mov.IdProveedor.HasValue ? (object)mov.IdProveedor.Value : DBNull.Value;
                cmd.Parameters.Add("@doc", SqlDbType.NVarChar, MovimientoInventario.MaxNumeroDocumento).Value =
                    string.IsNullOrEmpty(mov.NumeroDocumento) ? (object)DBNull.Value : mov.NumeroDocumento;
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        /// <summary>Movimientos de un medicamento, del más reciente al más antiguo.</summary>
        public List<MovimientoInventario> ListarMovimientos(int idMedicamento)
        {
            const string sql =
                "SELECT mv.IdMovimiento, mv.IdMedicamento, mv.Tipo, mv.Cantidad, mv.Fecha, mv.IdUsuario, mv.Motivo, u.NombreCompleto, mv.IdProveedor, pr.Nombre, mv.NumeroDocumento " +
                "FROM MovimientoInventario mv LEFT JOIN Usuario u ON u.IdUsuario = mv.IdUsuario " +
                "LEFT JOIN Proveedor pr ON pr.IdProveedor = mv.IdProveedor " +   // sin filtrar Activo: el historial conserva proveedores dados de baja
                "WHERE mv.IdMedicamento = @id ORDER BY mv.Fecha DESC, mv.IdMovimiento DESC";

            List<MovimientoInventario> lista = new List<MovimientoInventario>();
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(sql, cn))
            {
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = idMedicamento;
                cn.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        MovimientoInventario mv = new MovimientoInventario();
                        mv.IdMovimiento = r.GetInt32(0);
                        mv.IdMedicamento = r.GetInt32(1);
                        mv.Tipo = string.Equals(r.GetString(2).Trim(), TextoIngreso, StringComparison.OrdinalIgnoreCase)
                            ? TipoMovimiento.Ingreso : TipoMovimiento.Salida;
                        mv.Cantidad = r.GetInt32(3);
                        mv.Fecha = r.GetDateTime(4);
                        mv.IdUsuario = r.GetInt32(5);
                        mv.Motivo = r.IsDBNull(6) ? string.Empty : r.GetString(6);
                        mv.NombreUsuario = r.IsDBNull(7) ? string.Empty : r.GetString(7);
                        mv.IdProveedor = r.IsDBNull(8) ? (int?)null : r.GetInt32(8);
                        mv.NombreProveedor = r.IsDBNull(9) ? string.Empty : r.GetString(9);
                        // Se asigna al campo vía el setter solo si cabe; un dato viejo más largo se recorta para no romper la lista.
                        string doc = r.IsDBNull(10) ? string.Empty : r.GetString(10).Trim();
                        mv.NumeroDocumento = doc.Length > MovimientoInventario.MaxNumeroDocumento
                            ? doc.Substring(0, MovimientoInventario.MaxNumeroDocumento) : doc;
                        lista.Add(mv);
                    }
                }
            }
            return lista;
        }

        // ---------- Auxiliares ----------

        private static void AgregarParametrosEditables(SqlCommand cmd, Medicamento m)
        {
            cmd.Parameters.Add("@nom", SqlDbType.NVarChar, 100).Value = m.Nombre;
            cmd.Parameters.Add("@pres", SqlDbType.NVarChar, 60).Value =
                string.IsNullOrEmpty(m.Presentacion) ? (object)DBNull.Value : m.Presentacion;
            cmd.Parameters.Add("@min", SqlDbType.Int).Value = m.StockMinimo;
            cmd.Parameters.Add("@forma", SqlDbType.Int).Value = m.IdForma.HasValue ? (object)m.IdForma.Value : DBNull.Value;
            cmd.Parameters.Add("@unidad", SqlDbType.Int).Value = m.IdUnidad.HasValue ? (object)m.IdUnidad.Value : DBNull.Value;
            cmd.Parameters.Add("@conc", SqlDbType.NVarChar, Medicamento.MaxConcentracion).Value =
                string.IsNullOrEmpty(m.Concentracion) ? (object)DBNull.Value : m.Concentracion;
            cmd.Parameters.Add("@venc", SqlDbType.Date).Value =
                m.FechaVencimiento.HasValue ? (object)m.FechaVencimiento.Value.Date : DBNull.Value;
        }

        private static List<Medicamento> Consultar(string sql, Action<SqlCommand> parametros)
        {
            List<Medicamento> lista = new List<Medicamento>();
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

        private static Medicamento Mapear(SqlDataReader r)
        {
            Medicamento m = new Medicamento();
            m.IdMedicamento = r.GetInt32(0);
            m.Nombre = r.GetString(1);
            m.Presentacion = r.IsDBNull(2) ? string.Empty : r.GetString(2);
            m.IdPosta = r.IsDBNull(3) ? 0 : r.GetInt32(3);
            m.Stock = r.GetInt32(4);
            m.StockMinimo = r.GetInt32(5);
            m.FechaVencimiento = r.IsDBNull(6) ? (DateTime?)null : r.GetDateTime(6);
            m.Activo = r.GetBoolean(7);
            m.IdForma = r.IsDBNull(8) ? (int?)null : r.GetInt32(8);
            m.IdUnidad = r.IsDBNull(9) ? (int?)null : r.GetInt32(9);
            // Se carga SIN validar (un dato guardado desde SSMS más largo no debe romper la lista).
            Medicamento.CargarConcentracion(m, r.IsDBNull(10) ? string.Empty : r.GetString(10));
            m.FormaNombre = r.IsDBNull(11) ? string.Empty : r.GetString(11);
            m.UnidadNombre = r.IsDBNull(12) ? string.Empty : r.GetString(12);
            return m;
        }

    }
}
