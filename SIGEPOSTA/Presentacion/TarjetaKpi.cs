using System.Drawing;
using System.Windows.Forms;

namespace SIGEPOSTA.Presentacion
{
    /// <summary>Estado de color de una tarjeta de indicador.</summary>
    public enum NivelKpi
    {
        /// <summary>Tarjeta blanca (todo normal).</summary>
        Normal,
        /// <summary>Tarjeta ámbar (alerta).</summary>
        Ambar,
        /// <summary>Tarjeta roja (crítico: vencidos).</summary>
        Rojo
    }

    /// <summary>
    /// Tarjeta de indicador (KPI): un número grande y su etiqueta, con una franja de color a la izquierda.
    /// Decisión de diseño: es un control propio para que las seis tarjetas se vean y se comporten igual
    /// y para que el panel solo diga "muestra este valor con este nivel", sin repetir código de colores.
    /// </summary>
    public class TarjetaKpi : Panel
    {
        private readonly Panel _franja;
        private readonly Label _numero;
        private readonly Label _etiqueta;

        public TarjetaKpi(string etiqueta)
        {
            Height = 118; // alto para el número y hasta 3 líneas de etiqueta
            Dock = DockStyle.Fill;
            Margin = Padding.Empty;
            Padding = new Padding(Estilo.MargenInterior, 10, Estilo.EspacioCampos, 10);

            _etiqueta = new Label();
            _etiqueta.Text = etiqueta;
            _etiqueta.Font = Estilo.FuentePequena;
            _etiqueta.Dock = DockStyle.Fill;
            _etiqueta.TextAlign = ContentAlignment.TopLeft;
            _etiqueta.AutoSize = false; // con AutoSize apagado el texto largo se parte en líneas dentro de la tarjeta

            _numero = new Label();
            _numero.Font = new Font("Segoe UI", 24f, FontStyle.Bold);
            _numero.Dock = DockStyle.Top;
            _numero.Height = 44;
            _numero.TextAlign = ContentAlignment.MiddleLeft;
            _numero.AutoSize = false;

            _franja = new Panel();
            _franja.Dock = DockStyle.Left;
            _franja.Width = 5;

            // Orden de agregado: lo último se acopla primero (franja a la izquierda, número arriba, etiqueta al resto).
            Controls.Add(_etiqueta);
            Controls.Add(_numero);
            Controls.Add(_franja);

            Mostrar(null, NivelKpi.Normal);
        }

        /// <summary>Muestra el valor (null = todavía sin dato: se ve "—") con el color del nivel indicado.</summary>
        public void Mostrar(int? valor, NivelKpi nivel)
        {
            _numero.Text = valor.HasValue ? valor.Value.ToString("N0") : "—";

            Color fondo;
            Color colorNumero;
            Color colorEtiqueta;
            Color colorFranja;
            switch (nivel)
            {
                case NivelKpi.Ambar:
                    fondo = Estilo.AlertaStock;
                    colorNumero = Estilo.Texto;
                    colorEtiqueta = Estilo.Texto;
                    colorFranja = ControlPaint.Dark(Estilo.AlertaStock, 0.2f);
                    break;
                case NivelKpi.Rojo:
                    fondo = Estilo.Error;
                    colorNumero = Color.White;
                    colorEtiqueta = Color.White;
                    colorFranja = ControlPaint.Dark(Estilo.Error, 0.25f);
                    break;
                default:
                    fondo = Estilo.Recuadro;
                    colorNumero = Estilo.Morado;
                    colorEtiqueta = Estilo.Texto;
                    colorFranja = Estilo.Dorado;
                    break;
            }

            BackColor = fondo;
            _numero.BackColor = fondo;
            _etiqueta.BackColor = fondo;
            _numero.ForeColor = colorNumero;
            _etiqueta.ForeColor = colorEtiqueta;
            _franja.BackColor = colorFranja;
        }
    }
}
