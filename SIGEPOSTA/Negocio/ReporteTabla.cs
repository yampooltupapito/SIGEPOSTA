using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SIGEPOSTA.Negocio
{
    /// <summary>
    /// Resultado genérico de un reporte: títulos de columnas + filas de texto.
    /// Decisión de diseño: con una sola forma de tabla, la ventana de reportes pinta cualquier reporte
    /// y el botón "Exportar a CSV" funciona igual para todos, sin código repetido.
    /// </summary>
    public class ReporteTabla
    {
        public string Titulo { get; set; }
        public List<string> Columnas { get; private set; }
        public List<string[]> Filas { get; private set; }
        /// <summary>Línea de totales o aclaraciones para mostrar bajo la tabla.</summary>
        public string Resumen { get; set; }

        public ReporteTabla(string titulo, IEnumerable<string> columnas)
        {
            Titulo = titulo;
            Columnas = new List<string>(columnas);
            Filas = new List<string[]>();
            Resumen = string.Empty;
        }

        /// <summary>
        /// Guarda el reporte como CSV (UTF-8 con marca BOM para que Excel respete las tildes).
        /// Seguridad: una celda que empiece con = + - @ se escribe con un apóstrofo delante; si no, Excel
        /// la interpretaría como una fórmula (inyección de fórmulas con texto escrito por usuarios).
        /// </summary>
        public void GuardarCsv(string ruta)
        {
            if (string.IsNullOrWhiteSpace(ruta)) throw new ReglaNegocioException("Indique dónde guardar el archivo.");

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(Unir(Columnas));
            foreach (string[] fila in Filas) sb.AppendLine(Unir(fila));
            File.WriteAllText(ruta, sb.ToString(), new UTF8Encoding(true));
        }

        private static string Unir(IEnumerable<string> celdas)
        {
            List<string> partes = new List<string>();
            foreach (string c in celdas) partes.Add(EscaparCsv(c));
            return string.Join(",", partes);
        }

        internal static string EscaparCsv(string valor)
        {
            string v = valor ?? string.Empty;
            if (v.Length > 0 && (v[0] == '=' || v[0] == '+' || v[0] == '-' || v[0] == '@' || v[0] == '\t' || v[0] == '\r'))
                v = "'" + v;
            if (v.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
                v = "\"" + v.Replace("\"", "\"\"") + "\"";
            return v;
        }
    }
}
