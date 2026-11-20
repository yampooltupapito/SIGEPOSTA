using System;
using System.Collections.Generic;

namespace SIGEPOSTA.Negocio
{
    /// <summary>Un punto de un gráfico: una etiqueta (posta, día) y su valor.</summary>
    public class PuntoDato
    {
        public string Etiqueta { get; set; }
        public int Valor { get; set; }

        public PuntoDato() { }

        public PuntoDato(string etiqueta, int valor)
        {
            Etiqueta = etiqueta;
            Valor = valor;
        }
    }

    /// <summary>
    /// Todo lo que muestra el panel de inicio, ya calculado. Decisión de diseño: es un objeto simple de
    /// solo datos; así el panel (en la capa de Presentación) no sabe nada de SQL ni de permisos: solo dibuja.
    /// Los bloques que el rol NO puede ver quedan en su valor por defecto y sus banderas Ve... en false.
    /// </summary>
    public class DatosDashboard
    {
        // ----- Qué bloques corresponden al rol (los decide DashboardServicio, no la pantalla) -----
        public bool VePacientes { get; set; }
        public bool VeInventario { get; set; }

        // ----- Indicadores de pacientes y atenciones -----
        public int PacientesActivos { get; set; }
        public int AtencionesHoy { get; set; }
        public int AtencionesMes { get; set; }

        // ----- Indicadores de inventario -----
        public int StockBajo { get; set; }
        public int PorVencer { get; set; }
        public int Vencidos { get; set; }

        // ----- Gráficos y lista -----
        public List<PuntoDato> AtencionesPorDiaDelMes { get; set; }
        public List<PuntoDato> AtencionesUltimos7Dias { get; set; }
        public List<Medicamento> Alertas { get; set; }

        // ----- Estado de la carga: un bloque puede fallar sin tumbar al otro -----
        public bool ErrorPacientes { get; set; }
        public bool ErrorInventario { get; set; }
        /// <summary>Primer error técnico ocurrido. NUNCA se muestra al usuario; la pantalla solo lo manda al registro.</summary>
        public Exception ErrorTecnico { get; set; }

        public DateTime ActualizadoEn { get; set; }

        /// <summary>Tiempo total de las consultas del panel, medido con Stopwatch.</summary>
        public long Milisegundos { get; set; }

        public DatosDashboard()
        {
            AtencionesPorDiaDelMes = new List<PuntoDato>();
            AtencionesUltimos7Dias = new List<PuntoDato>();
            Alertas = new List<Medicamento>();
        }

        public bool HuboErrores
        {
            get { return ErrorPacientes || ErrorInventario; }
        }
    }
}
