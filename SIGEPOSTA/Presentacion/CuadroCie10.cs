using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SIGEPOSTA.Negocio;

namespace SIGEPOSTA.Presentacion
{
    /// <summary>
    /// Cuadro de búsqueda con sugerencias para elegir un diagnóstico CIE-10 (autocompletado).
    /// Decisiones de diseño:
    ///  - Espera 300 ms de pausa antes de consultar y la consulta corre en segundo plano (ControladorBusqueda): escribir
    ///    no congela la ventana ni lanza una consulta por tecla; si el usuario sigue escribiendo, la anterior se descarta.
    ///  - La lista (máx. 15) se ve como "J06.9 - Infección aguda de las vías respiratorias superiores".
    ///  - Solo vale un código ELEGIDO de la lista (CodigoElegido). Si el usuario edita el texto después, la elección se
    ///    anula: así nunca se guarda un código distinto al que se ve.
    ///  - Teclado: ↓ ↑ mueven la selección, Enter elige, Esc cierra la lista.
    /// El código elegido no se escribe en ningún registro desde este control.
    /// </summary>
    public class CuadroCie10 : TextBox
    {
        /// <summary>Lista que nunca toma el foco: el cuadro de texto sigue recibiendo lo que se escribe.</summary>
        private class ListaSinFoco : ListBox
        {
            public ListaSinFoco()
            {
                SetStyle(ControlStyles.Selectable, false);
                TabStop = false;
            }
        }

        private const int MaximoFilasVisibles = 8;

        private readonly Cie10Servicio _servicio;
        private readonly ControladorBusqueda<Cie10> _busqueda;
        private readonly ToolStripDropDown _desplegable = new ToolStripDropDown();
        private readonly ListaSinFoco _lista = new ListaSinFoco();
        private Cie10 _elegido;
        private bool _poniendoTexto;
        private bool _sinResultados;

        /// <summary>Código elegido de la lista (null si no hay elección vigente).</summary>
        public string CodigoElegido
        {
            get { return _elegido == null ? null : _elegido.Codigo; }
        }

        /// <summary>true si hay algo escrito pero no se eligió ninguna sugerencia.</summary>
        public bool HayTextoSinElegir
        {
            get { return _elegido == null && Text.Trim().Length > 0; }
        }

        public CuadroCie10(Cie10Servicio servicio)
        {
            if (servicio == null) throw new ArgumentNullException("servicio");
            _servicio = servicio;

            // Mismo aspecto que los demás cuadros de texto (Estilo.CrearTextBox).
            Font = Estilo.Fuente;
            ForeColor = Estilo.Texto;
            BackColor = Estilo.Recuadro;
            BorderStyle = BorderStyle.FixedSingle;
            AutoSize = false;
            Height = Estilo.AltoCampo;
            MaxLength = 120;
            Estilo.PonerAyuda(this, "Ej.: J06.9, o escriba parte del nombre (sin importar tildes)");

            _lista.Font = Estilo.Fuente;
            _lista.BorderStyle = BorderStyle.None;
            _lista.IntegralHeight = false;
            _lista.MouseDown += Lista_MouseDown;

            ToolStripControlHost anfitrion = new ToolStripControlHost(_lista);
            anfitrion.Margin = Padding.Empty;
            anfitrion.Padding = Padding.Empty;
            anfitrion.AutoSize = false;
            _desplegable.Items.Add(anfitrion);
            _desplegable.AutoClose = false;     // se cierra por código (elegir, Esc o salir del cuadro)
            _desplegable.Padding = Padding.Empty;
            _desplegable.TabStop = false;

            _busqueda = new ControladorBusqueda<Cie10>(
                delegate (string texto, int pagina, object filtro, Func<bool> cancelado) { return _servicio.Sugerir(texto, cancelado); },
                MostrarSugerencias, Fallar, null);
        }

        /// <summary>Vacía el cuadro y la elección (después de guardar la atención).</summary>
        public void Limpiar()
        {
            Ocultar();
            _elegido = null;
            _poniendoTexto = true;
            try { Clear(); }
            finally { _poniendoTexto = false; }
        }

        // ================== Eventos del cuadro ==================

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            if (_poniendoTexto) return;

            _elegido = null;   // el usuario editó: la elección anterior ya no vale
            if (Text.Trim().Length == 0)
            {
                Ocultar();
                return;
            }
            _busqueda.TextoCambio(Text);   // consulta tras 300 ms sin escribir, en segundo plano
        }

        protected override void OnLeave(EventArgs e)
        {
            base.OnLeave(e);
            Ocultar();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_desplegable.Visible)
            {
                if (keyData == Keys.Down) { Mover(1); return true; }
                if (keyData == Keys.Up) { Mover(-1); return true; }
                if (keyData == Keys.Escape) { Ocultar(); return true; }
                if (keyData == Keys.Enter || keyData == Keys.Tab)
                {
                    Cie10 c = _lista.SelectedItem as Cie10;
                    if (c != null) { Elegir(c); return keyData == Keys.Enter; }
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void Lista_MouseDown(object sender, MouseEventArgs e)
        {
            int i = _lista.IndexFromPoint(e.Location);
            if (i < 0) return;
            Cie10 c = _lista.Items[i] as Cie10;
            if (c != null) Elegir(c);
        }

        // ================== Sugerencias ==================

        private void MostrarSugerencias(ResultadoPagina<Cie10> r)
        {
            // Si ya se eligió algo, o el cuadro perdió el foco mientras se buscaba, no se vuelve a abrir la lista.
            if (_elegido != null || !Focused || Text.Trim().Length == 0)
            {
                Ocultar();
                return;
            }

            _lista.BeginUpdate();
            try
            {
                _lista.Items.Clear();
                _sinResultados = r.Items.Count == 0;
                if (_sinResultados) _lista.Items.Add("(Sin coincidencias en el catálogo)");
                else foreach (Cie10 c in r.Items) _lista.Items.Add(c);
                if (!_sinResultados) _lista.SelectedIndex = 0;
            }
            finally
            {
                _lista.EndUpdate();
            }

            int filas = Math.Min(_lista.Items.Count, MaximoFilasVisibles);
            int ancho = Math.Max(Width, 480);
            _lista.Size = new Size(ancho, filas * _lista.ItemHeight + 4);
            _desplegable.Size = new Size(ancho + 2, _lista.Height + 2);
            if (_desplegable.Visible) _desplegable.Refresh();
            else _desplegable.Show(this, new Point(0, Height));
        }

        /// <summary>Un error al consultar solo cierra la lista: sin mensajes técnicos y sin registrar lo escrito.</summary>
        private void Fallar(Exception ex)
        {
            Ocultar();
        }

        private void Mover(int paso)
        {
            if (_sinResultados || _lista.Items.Count == 0) return;
            int i = _lista.SelectedIndex + paso;
            _lista.SelectedIndex = Math.Max(0, Math.Min(_lista.Items.Count - 1, i));
        }

        private void Elegir(Cie10 c)
        {
            _poniendoTexto = true;
            try
            {
                Text = c.Texto;
                SelectionStart = Text.Length;
            }
            finally
            {
                _poniendoTexto = false;
            }
            _elegido = c;
            Ocultar();
        }

        private void Ocultar()
        {
            if (_desplegable.Visible) _desplegable.Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _busqueda.Dispose();
                _desplegable.Dispose();
                _lista.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
