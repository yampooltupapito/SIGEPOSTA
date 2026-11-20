using System;
using System.Data;
using System.Data.SqlClient;

namespace SIGEPOSTA.Datos
{
    /// <summary>
    /// Arma los patrones LIKE de las búsquedas. Decisiones de diseño:
    ///  - Búsqueda por el INICIO del texto (LIKE 'texto%'): así SQL Server puede usar un índice sobre esa columna.
    ///    Un LIKE '%texto%' (contiene) obliga a leer toda la tabla aunque exista el índice.
    ///  - Los comodines que escriba el usuario (% _ [ \) se escapan: se buscan como texto literal.
    ///  - El texto viaja SIEMPRE como parámetro tipado (nunca pegado al SQL).
    /// </summary>
    public static class PatronBusqueda
    {
        public const int LargoMaximo = 100;

        public static bool HayTexto(string texto)
        {
            return !string.IsNullOrWhiteSpace(texto);
        }

        /// <summary>Escapa los comodines de LIKE para que lo escrito se busque literalmente.</summary>
        public static string Escapar(string texto)
        {
            return texto.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
        }

        /// <summary>"texto%" (búsqueda por el inicio), limitado a LargoMaximo caracteres.</summary>
        public static string Prefijo(string texto)
        {
            string t = (texto ?? string.Empty).Trim();
            if (t.Length > LargoMaximo) t = t.Substring(0, LargoMaximo);
            return Escapar(t) + "%";
        }

        /// <summary>
        /// Agrega los parámetros de la búsqueda de pacientes: @p1 = primera palabra, @p2 = el resto de las palabras
        /// (o "%" si hay una sola) y @pn = el texto completo (para el N.º de historia). Todos NVARCHAR(200).
        /// </summary>
        public static void AgregarParametrosPersona(SqlCommand cmd, string texto)
        {
            string t = (texto ?? string.Empty).Trim();
            string primera = t;
            string resto = string.Empty;
            int espacio = t.IndexOf(' ');
            if (espacio > 0)
            {
                primera = t.Substring(0, espacio);
                resto = t.Substring(espacio + 1).Trim();
            }
            cmd.Parameters.Add("@p1", SqlDbType.NVarChar, 200).Value = Prefijo(primera);
            cmd.Parameters.Add("@p2", SqlDbType.NVarChar, 200).Value = resto.Length == 0 ? "%" : Prefijo(resto);
            cmd.Parameters.Add("@pn", SqlDbType.NVarChar, 200).Value = Prefijo(t);
        }
    }
}
