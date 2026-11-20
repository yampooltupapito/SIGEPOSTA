using System;
using System.Collections.Generic;

namespace SIGEPOSTA.Negocio
{
    /// <summary>Constantes de la paginación.</summary>
    public static class Paginacion
    {
        /// <summary>Filas por página en las listas grandes (pacientes, inventario).</summary>
        public const int TamanoPagina = 50;

        public static int CalcularTotalPaginas(int total, int tamano)
        {
            if (tamano <= 0) throw new ArgumentOutOfRangeException("tamano");
            return total <= 0 ? 1 : (total + tamano - 1) / tamano;
        }
    }

    /// <summary>
    /// Una página de resultados: SOLO las filas de esa página más el total de coincidencias (COUNT aparte),
    /// para poder mostrar "Página 2 de 10" sin traer todas las filas. Incluye cuánto tardó la consulta.
    /// </summary>
    public class ResultadoPagina<T>
    {
        public List<T> Items { get; set; }
        /// <summary>Total de filas que cumplen la búsqueda (en TODAS las páginas).</summary>
        public int Total { get; set; }
        /// <summary>Página mostrada (empieza en 1).</summary>
        public int Pagina { get; set; }
        public int TamanoPagina { get; set; }
        /// <summary>Tiempo de las consultas a la base de datos (COUNT + página), medido con Stopwatch.</summary>
        public long Milisegundos { get; set; }

        public ResultadoPagina()
        {
            Items = new List<T>();
            Pagina = 1;
            TamanoPagina = Paginacion.TamanoPagina;
        }

        public int TotalPaginas
        {
            get { return Paginacion.CalcularTotalPaginas(Total, TamanoPagina); }
        }

        public bool HayAnterior
        {
            get { return Pagina > 1; }
        }

        public bool HaySiguiente
        {
            get { return Pagina < TotalPaginas; }
        }

        /// <summary>Texto para la pantalla, ej. "Página 2 de 10 · 478 resultados".</summary>
        public string TextoPagina
        {
            get { return "Página " + Pagina + " de " + TotalPaginas + " · " + Total.ToString("N0") + " resultado(s)"; }
        }
    }
}
