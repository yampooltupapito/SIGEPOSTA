using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using SIGEPOSTA.Negocio;

namespace SIGEPOSTA.Datos
{
    /// <summary>
    /// Acceso a datos del catálogo Cie10 (ADO.NET, SOLO consultas parametrizadas con tipo y tamaño explícitos).
    /// Decisiones de diseño:
    ///  - Búsqueda SIN importar tildes ni mayúsculas: se compara con COLLATE Latin1_General_CI_AI (CI = sin mayúsculas,
    ///    AI = sin tildes). "infeccion" encuentra "Infección".
    ///  - El código se busca por el INICIO (LIKE 'j06%'); la descripción, por CONTENIDO de cada palabra escrita
    ///    (LIKE '%palabra%', hasta 4 palabras, todas deben aparecer). Un LIKE '%x%' no usa índice, pero el catálogo
    ///    es pequeño (unos cientos o miles de filas), por eso aquí sí se acepta.
    ///  - Cada palabra va en su propio parámetro (@w0..@w3; las que sobran valen '%'), así la consulta es UNA sola
    ///    cadena constante: nunca se arma SQL pegando el texto del usuario.
    ///  - Baja lógica (Activo = 0): nunca se borra, porque atenciones antiguas pueden referirse al código.
    /// </summary>
    public class Cie10Datos
    {
        private const int ErrorLlaveDuplicada1 = 2627;
        private const int ErrorLlaveDuplicada2 = 2601;
        public const int MaxPalabras = 4;

        private const string Columnas = "SELECT c.Codigo, c.Descripcion, c.Activo ";
        private const string Donde =
            "FROM Cie10 c WHERE c.Activo = 1 AND (" +
            "c.Codigo COLLATE Latin1_General_CI_AI LIKE @cod ESCAPE '\\' OR (" +
            "c.Descripcion COLLATE Latin1_General_CI_AI LIKE @w0 ESCAPE '\\' AND " +
            "c.Descripcion COLLATE Latin1_General_CI_AI LIKE @w1 ESCAPE '\\' AND " +
            "c.Descripcion COLLATE Latin1_General_CI_AI LIKE @w2 ESCAPE '\\' AND " +
            "c.Descripcion COLLATE Latin1_General_CI_AI LIKE @w3 ESCAPE '\\')) ";

        private const string SqlContar = "SELECT COUNT(*) " + Donde;
        private const string SqlPagina = Columnas + Donde + "ORDER BY c.Codigo OFFSET @desde ROWS FETCH NEXT @cantidad ROWS ONLY";
        private const string SqlSugerir = "SELECT TOP (@max) c.Codigo, c.Descripcion, c.Activo " + Donde + "ORDER BY c.Codigo";

        public virtual int Contar(string texto)
        {
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(SqlContar, cn))
            {
                AgregarFiltro(cmd, texto);
                cn.Open();
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public virtual List<Cie10> ListarPagina(string texto, int pagina, int cantidad)
        {
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(SqlPagina, cn))
            {
                AgregarFiltro(cmd, texto);
                cmd.Parameters.Add("@desde", SqlDbType.Int).Value = Math.Max(0, (pagina - 1) * cantidad);
                cmd.Parameters.Add("@cantidad", SqlDbType.Int).Value = cantidad;
                cn.Open();
                return Leer(cmd);
            }
        }

        /// <summary>Hasta "max" códigos ACTIVOS que coinciden (para el autocompletado).</summary>
        public virtual List<Cie10> Sugerir(string texto, int max)
        {
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(SqlSugerir, cn))
            {
                AgregarFiltro(cmd, texto);
                cmd.Parameters.Add("@max", SqlDbType.Int).Value = max;
                cn.Open();
                return Leer(cmd);
            }
        }

        /// <summary>El código exacto, ACTIVO O NO (las atenciones antiguas siguen mostrando códigos dados de baja). Null si no existe.</summary>
        public virtual Cie10 ObtenerPorCodigo(string codigo)
        {
            const string sql = "SELECT c.Codigo, c.Descripcion, c.Activo FROM Cie10 c WHERE c.Codigo = @cod";
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(sql, cn))
            {
                cmd.Parameters.Add("@cod", SqlDbType.NVarChar, 7).Value = codigo;
                cn.Open();
                List<Cie10> r = Leer(cmd);
                return r.Count == 0 ? null : r[0];
            }
        }

        /// <summary>Todo el catálogo (activos y dados de baja): lo usa el reporte para poner la descripción a cada código.</summary>
        public virtual List<Cie10> ListarTodos()
        {
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando("SELECT c.Codigo, c.Descripcion, c.Activo FROM Cie10 c ORDER BY c.Codigo", cn))
            {
                cn.Open();
                return Leer(cmd);
            }
        }

        public virtual void Insertar(Cie10 c)
        {
            if (c == null) throw new ArgumentNullException("c");
            const string sql = "INSERT INTO Cie10 (Codigo, Descripcion, Activo) VALUES (@cod, @desc, 1)";
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(sql, cn))
            {
                cmd.Parameters.Add("@cod", SqlDbType.NVarChar, 7).Value = c.Codigo;
                cmd.Parameters.Add("@desc", SqlDbType.NVarChar, Cie10.MaxDescripcion).Value = c.Descripcion;
                cn.Open();
                try
                {
                    cmd.ExecuteNonQuery();
                }
                catch (SqlException ex)
                {
                    if (ex.Number == ErrorLlaveDuplicada1 || ex.Number == ErrorLlaveDuplicada2)
                        throw new ValidacionException("Ese código CIE-10 ya existe en el catálogo.", "Codigo");
                    throw;
                }
            }
        }

        /// <summary>Cambia la descripción. El código no se edita (es la clave y puede estar en atenciones).</summary>
        public virtual void ActualizarDescripcion(string codigo, string descripcion)
        {
            const string sql = "UPDATE Cie10 SET Descripcion = @desc WHERE Codigo = @cod AND Activo = 1";
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(sql, cn))
            {
                cmd.Parameters.Add("@cod", SqlDbType.NVarChar, 7).Value = codigo;
                cmd.Parameters.Add("@desc", SqlDbType.NVarChar, Cie10.MaxDescripcion).Value = descripcion;
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public virtual void BajaLogica(string codigo)
        {
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando("UPDATE Cie10 SET Activo = 0 WHERE Codigo = @cod", cn))
            {
                cmd.Parameters.Add("@cod", SqlDbType.NVarChar, 7).Value = codigo;
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        // ---------- Auxiliares ----------

        /// <summary>Divide el texto en palabras (máximo MaxPalabras) para los parámetros de búsqueda.</summary>
        public static string[] Palabras(string texto)
        {
            string t = (texto ?? string.Empty).Trim();
            if (t.Length > PatronBusqueda.LargoMaximo) t = t.Substring(0, PatronBusqueda.LargoMaximo);
            string[] todas = t.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int n = Math.Min(todas.Length, MaxPalabras);
            string[] r = new string[n];
            Array.Copy(todas, r, n);
            return r;
        }

        private static void AgregarFiltro(SqlCommand cmd, string texto)
        {
            string[] p = Palabras(texto);
            bool hay = p.Length > 0;
            // Sin texto: todo vale ('%'). Con texto: @cod busca por el inicio del código y @w0..@w3 por palabras de la descripción.
            cmd.Parameters.Add("@cod", SqlDbType.NVarChar, 200).Value = hay ? PatronBusqueda.Prefijo(string.Join(" ", p)) : "%";
            for (int i = 0; i < MaxPalabras; i++)
            {
                string valor = i < p.Length ? "%" + PatronBusqueda.Escapar(p[i]) + "%" : "%";
                cmd.Parameters.Add("@w" + i, SqlDbType.NVarChar, 200).Value = valor;
            }
        }

        private static List<Cie10> Leer(SqlCommand cmd)
        {
            List<Cie10> lista = new List<Cie10>();
            using (SqlDataReader r = cmd.ExecuteReader())
            {
                while (r.Read())
                    lista.Add(Cie10.Cargar(r.GetString(0), r.IsDBNull(1) ? string.Empty : r.GetString(1), r.GetBoolean(2)));
            }
            return lista;
        }
    }
}
