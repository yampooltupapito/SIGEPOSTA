using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SIGEPOSTA.Presentacion
{
    /// <summary>
    /// Lista desplegable con borde. Un ComboBox plano (FlatStyle.Flat) no dibuja borde, y se veía distinto de los
    /// cuadros de texto; aquí se dibuja el mismo borde fino gris después de que Windows pinta el control.
    /// </summary>
    internal class ComboConBorde : ComboBox
    {
        private const int WM_PAINT = 0x000F;

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_PAINT && Width > 2 && Height > 2)
            {
                using (Graphics g = Graphics.FromHwnd(Handle))
                using (Pen lapiz = new Pen(Estilo.BordeCampo))
                {
                    g.DrawRectangle(lapiz, 0, 0, Width - 1, Height - 1);
                }
            }
        }
    }

    /// <summary>
    /// Único lugar donde viven colores, fuentes y medidas de la interfaz, más los métodos que
    /// crean controles ya estilizados. Si mañana cambia la paleta, se cambia aquí y nada más.
    /// Decisión de diseño: todo el diseño usa TableLayoutPanel + Dock/Anchor (sin coordenadas fijas),
    /// y las medidas de espaciado salen de las constantes de esta clase.
    /// </summary>
    public static class Estilo
    {
        // ---------- Paleta ----------
        public static readonly Color Rojo = ColorTranslator.FromHtml("#A6192E");        // barra de título, encabezado de tabla, Guardar
        public static readonly Color Morado = ColorTranslator.FromHtml("#5B2A86");      // títulos de recuadros
        public static readonly Color Dorado = ColorTranslator.FromHtml("#C9A227");      // línea de detalle y fila seleccionada
        public static readonly Color Fondo = ColorTranslator.FromHtml("#FAF6F0");       // fondo de ventana
        public static readonly Color Recuadro = ColorTranslator.FromHtml("#FFFFFF");    // fondo de recuadros
        public static readonly Color Texto = ColorTranslator.FromHtml("#2B2B2B");
        public static readonly Color Error = ColorTranslator.FromHtml("#D32F2F");
        public static readonly Color Exito = ColorTranslator.FromHtml("#2E7D32");
        public static readonly Color AlertaStock = ColorTranslator.FromHtml("#E0A100");
        public static readonly Color Gris = ColorTranslator.FromHtml("#6B6B6B");        // botón secundario
        public static readonly Color FilaAlterna = ColorTranslator.FromHtml("#F6F0E6"); // filas alternadas de tablas
        public static readonly Color Borde = ColorTranslator.FromHtml("#D9D0C3");
        public static readonly Color BordeCampo = Color.FromArgb(122, 122, 122);        // borde de cuadros de texto y listas

        // ---------- Medidas (en píxeles) ----------
        public const int EspacioCampos = 12;     // entre un campo y el siguiente
        public const int MargenInterior = 16;    // margen interior de recuadros y ventanas
        public const int EspacioEtiqueta = 4;    // entre la etiqueta y su cuadro
        public const int AltoCampo = 28;         // alto de TODOS los cuadros de texto, listas y selectores de fecha
        public const int AltoBoton = 38;
        public const int AltoBotonMinimo = 36;   // ningún botón mide menos que esto
        public const int AltoBarraTitulo = 52;
        public const int GrosorLinea = 3;        // línea dorada de detalle
        public const int AnchoDialogo = 440;     // ancho de las ventanas de diálogo (login, crear admin)
        public const int AnchoTextoDialogo = AnchoDialogo - 4 * MargenInterior; // ancho útil para textos largos

        // ---------- Fuentes ----------
        public static readonly Font Fuente = new Font("Segoe UI", 10f, FontStyle.Regular);
        public static readonly Font FuenteNegrita = new Font("Segoe UI", 10f, FontStyle.Bold);
        public static readonly Font FuentePequena = new Font("Segoe UI", 9f, FontStyle.Regular);
        public static readonly Font FuenteGrupo = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        public static readonly Font FuenteTitulo = new Font("Segoe UI", 14f, FontStyle.Bold);

        // ---------- Ventanas ----------

        /// <summary>Configura una ventana de diálogo: fondo, fuente, borde fijo, centrada y que se ajusta a su contenido.</summary>
        public static void PrepararDialogo(Form f, string titulo)
        {
            f.Text = titulo;
            f.Font = Fuente;
            f.BackColor = Fondo;
            f.ForeColor = Texto;
            f.StartPosition = FormStartPosition.CenterScreen;
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.MaximizeBox = false;
            f.MinimizeBox = false;
            f.ShowInTaskbar = true;
            f.AutoScaleMode = AutoScaleMode.Font;
            f.AutoSize = true;
            f.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        }

        /// <summary>Contenedor raíz de un diálogo: una columna de ancho fijo y filas que crecen con el contenido.</summary>
        public static TableLayoutPanel CrearRaizDialogo()
        {
            TableLayoutPanel t = new TableLayoutPanel();
            t.ColumnCount = 1;
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, AnchoDialogo));
            t.Dock = DockStyle.Fill;
            t.AutoSize = true;
            t.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            t.Margin = Padding.Empty;
            t.Padding = Padding.Empty;
            return t;
        }

        /// <summary>Barra superior roja con el título y una línea fina dorada en su borde inferior.</summary>
        public static Panel CrearBarraTitulo(string texto)
        {
            Panel barra = new Panel();
            barra.BackColor = Rojo;
            barra.Dock = DockStyle.Fill;
            barra.Height = AltoBarraTitulo;
            barra.Margin = Padding.Empty;

            Label lbl = new Label();
            lbl.Text = texto;
            lbl.Font = FuenteTitulo;
            lbl.ForeColor = Color.White;
            lbl.BackColor = Color.Transparent;
            lbl.Dock = DockStyle.Fill;
            lbl.TextAlign = ContentAlignment.MiddleLeft;
            lbl.Padding = new Padding(MargenInterior, 0, MargenInterior, 0);

            Panel linea = new Panel();
            linea.BackColor = Dorado;
            linea.Dock = DockStyle.Bottom;
            linea.Height = GrosorLinea;

            barra.Controls.Add(lbl);
            barra.Controls.Add(linea);
            return barra;
        }

        /// <summary>
        /// Agrega un botón (borde blanco, fondo rojo) al extremo derecho de la barra de título.
        /// Ej.: "Cerrar sesión" siempre visible arriba a la derecha.
        /// </summary>
        public static Button AgregarBotonABarra(Panel barra, string texto, EventHandler alHacerClic)
        {
            Panel contenedor = new Panel();
            contenedor.Dock = DockStyle.Right;
            contenedor.Width = 170;
            contenedor.BackColor = Color.Transparent;
            contenedor.Padding = new Padding(EspacioCampos, 10, MargenInterior, 10);

            Button b = new Button();
            b.Text = texto;
            b.Font = FuenteNegrita;
            b.ForeColor = Color.White;
            b.BackColor = Rojo;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Color.White;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = ControlPaint.Dark(Rojo, 0.15f);
            b.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(Rojo, 0.3f);
            b.UseVisualStyleBackColor = false;
            b.Cursor = Cursors.Hand;
            b.Dock = DockStyle.Fill;
            b.Click += alHacerClic;
            contenedor.Controls.Add(b);

            barra.Controls.Add(contenedor);
            // Se coloca debajo de la línea dorada en el orden de acople, para que la línea siga de lado a lado.
            barra.Controls.SetChildIndex(contenedor, 1);
            return b;
        }

        /// <summary>
        /// Enter en cualquiera de los controles del grupo ejecuta el botón indicado (por ejemplo, Enter en
        /// los campos de "Datos" dispara Guardar y en los de "Movimiento" dispara Registrar). Los botones
        /// quedan fuera: ya reaccionan a Enter por sí mismos.
        /// </summary>
        public static void VincularEnter(Form formulario, Control grupo, IButtonControl boton)
        {
            foreach (Control c in grupo.Controls)
            {
                if (!(c is Button))
                {
                    c.Enter += delegate { formulario.AcceptButton = boton; };
                }
                if (c.HasChildren) VincularEnter(formulario, c, boton);
            }
        }

        /// <summary>Al entrar a este control (ej. el cuadro de búsqueda) Enter no activa ningún botón.</summary>
        public static void SinEnter(Form formulario, Control control)
        {
            control.Enter += delegate { formulario.AcceptButton = null; };
        }

        /// <summary>Esc cierra la ventana (salvo que haya una lista desplegable abierta: entonces solo la cierra).</summary>
        public static void EscCierra(Form formulario)
        {
            formulario.KeyPreview = true;
            formulario.KeyDown += delegate (object s, KeyEventArgs e)
            {
                if (e.KeyCode != Keys.Escape) return;
                ComboBox combo = formulario.ActiveControl as ComboBox;
                if (combo != null && combo.DroppedDown) return;
                e.Handled = true;
                formulario.Close();
            };
        }

        /// <summary>Barra de progreso continua con el estilo de la aplicación.</summary>
        public static ProgressBar CrearBarraProgreso()
        {
            ProgressBar p = new ProgressBar();
            p.Style = ProgressBarStyle.Continuous;
            p.Minimum = 0;
            p.Maximum = 100;
            p.Height = 22;
            p.Dock = DockStyle.Fill;
            p.Margin = new Padding(0, 0, 0, EspacioEtiqueta);
            return p;
        }

        // ---------- Contenedores ----------

        /// <summary>Recuadro blanco con título morado (GroupBox) que se ajusta a su contenido.</summary>
        public static GroupBox CrearRecuadro(string titulo)
        {
            GroupBox g = new GroupBox();
            g.Text = titulo;
            g.Font = FuenteGrupo;
            g.ForeColor = Morado;
            g.BackColor = Recuadro;
            g.Padding = new Padding(MargenInterior);
            g.Margin = new Padding(MargenInterior);
            g.Dock = DockStyle.Fill;
            g.AutoSize = true;
            g.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            return g;
        }

        /// <summary>Columna vertical de una sola columna; cada fila crece según su contenido.</summary>
        public static TableLayoutPanel CrearColumna()
        {
            TableLayoutPanel t = new TableLayoutPanel();
            t.ColumnCount = 1;
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            t.Dock = DockStyle.Fill;
            t.AutoSize = true;
            t.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            t.BackColor = Color.Transparent;
            t.Margin = Padding.Empty;
            t.Padding = Padding.Empty;
            return t;
        }

        /// <summary>Agrega un control como una fila nueva al final de una columna.</summary>
        public static void AgregarFila(TableLayoutPanel tabla, Control control)
        {
            tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tabla.RowCount = tabla.RowStyles.Count;
            tabla.Controls.Add(control, 0, tabla.RowCount - 1);
        }

        // ---------- Campos ----------

        /// <summary>Cuadro de texto con el alto estándar. Con esClave oculta lo que se escribe.</summary>
        public static TextBox CrearTextBox(bool esClave = false, int maxLongitud = 0)
        {
            TextBox t = new TextBox();
            t.Font = Fuente;
            t.ForeColor = Texto;
            t.BackColor = Recuadro;
            t.BorderStyle = BorderStyle.FixedSingle;
            t.AutoSize = false;
            t.Height = AltoCampo;
            t.UseSystemPasswordChar = esClave;
            if (maxLongitud > 0) t.MaxLength = maxLongitud;
            return t;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private const int EM_SETCUEBANNER = 0x1501;

        /// <summary>Texto de ayuda gris dentro del cuadro vacío (placeholder). Desaparece al escribir.</summary>
        public static void PonerAyuda(TextBox cuadro, string ayuda)
        {
            EventHandler aplicar = delegate
            {
                if (cuadro.IsHandleCreated) SendMessage(cuadro.Handle, EM_SETCUEBANNER, (IntPtr)1, ayuda);
            };
            cuadro.HandleCreated += aplicar;
            aplicar(cuadro, EventArgs.Empty);
        }

        /// <summary>Campo completo: etiqueta arriba, cuadro abajo (4 px entre ambos) y 12 px de separación con el siguiente.</summary>
        public static TableLayoutPanel CrearCampo(string etiqueta, Control control)
        {
            TableLayoutPanel campo = new TableLayoutPanel();
            campo.ColumnCount = 1;
            campo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            campo.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            campo.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            campo.RowCount = 2;
            campo.Dock = DockStyle.Fill;
            campo.AutoSize = true;
            campo.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            campo.BackColor = Color.Transparent;
            campo.Padding = Padding.Empty;
            campo.Margin = new Padding(0, 0, 0, EspacioCampos);

            Label lbl = new Label();
            lbl.Text = etiqueta;
            lbl.Font = Fuente;
            lbl.ForeColor = Texto;
            lbl.AutoSize = true;
            lbl.Margin = new Padding(0, 0, 0, EspacioEtiquetaSeguro());

            control.Dock = DockStyle.Fill;
            control.Margin = Padding.Empty;

            campo.Controls.Add(lbl, 0, 0);
            campo.Controls.Add(control, 0, 1);
            return campo;
        }

        private static int EspacioEtiquetaSeguro()
        {
            return EspacioEtiqueta;
        }

        /// <summary>
        /// Igual que CrearCampo, pero con una tercera fila para el error (rojo) justo debajo del cuadro.
        /// Mientras no haya error la etiqueta está vacía y no ocupa espacio.
        /// </summary>
        public static TableLayoutPanel CrearCampoConError(string etiqueta, Control control, out Label lblError)
        {
            TableLayoutPanel campo = CrearCampo(etiqueta, control);
            campo.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            campo.RowCount = 3;

            Label err = new Label();
            err.Font = FuentePequena;
            err.ForeColor = Error;
            err.AutoSize = true;
            err.MaximumSize = new Size(320, 0);
            err.Margin = new Padding(0, EspacioEtiqueta, 0, 0);
            campo.Controls.Add(err, 0, 2);

            // Un mensaje largo se parte en varias líneas. Para que el contenedor MIDA bien esas líneas (y no
            // recorte lo de abajo), el ancho máximo del texto se ajusta siempre al ancho real de la celda.
            campo.SizeChanged += delegate
            {
                int ancho = Math.Max(60, campo.ClientSize.Width);
                if (err.MaximumSize.Width != ancho) err.MaximumSize = new Size(ancho, 0);
            };

            lblError = err;
            return campo;
        }

        /// <summary>Muestra (o limpia, si el texto es null o vacío) el error de un campo.</summary>
        public static void MostrarErrorCampo(Label lblError, string mensaje)
        {
            lblError.Text = mensaje ?? string.Empty;
        }

        /// <summary>Lista desplegable con el alto estándar (se dibuja a mano para poder fijar el alto).</summary>
        public static ComboBox CrearCombo()
        {
            ComboBox c = new ComboConBorde();
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.Font = Fuente;
            c.ForeColor = Texto;
            c.BackColor = Recuadro;
            c.FlatStyle = FlatStyle.Flat;
            c.DrawMode = DrawMode.OwnerDrawFixed;
            c.ItemHeight = AltoCampo - 6;
            c.DrawItem += delegate (object s, DrawItemEventArgs e)
            {
                if (e.Index < 0) return;
                bool resaltado = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
                using (SolidBrush fondo = new SolidBrush(resaltado ? Dorado : Recuadro))
                {
                    e.Graphics.FillRectangle(fondo, e.Bounds);
                }
                TextRenderer.DrawText(e.Graphics, c.GetItemText(c.Items[e.Index]), c.Font, e.Bounds, Texto,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            };
            return c;
        }

        /// <summary>Selector de fecha corta (dd/MM/aaaa) que no permite fechas futuras.</summary>
        public static DateTimePicker CrearSelectorFecha()
        {
            DateTimePicker d = new DateTimePicker();
            d.Format = DateTimePickerFormat.Short;
            d.Font = new Font("Segoe UI", 11.5f); // con este tamaño el selector de fecha mide lo mismo que los demás cuadros (28 px)
            d.MaxDate = DateTime.Today;
            d.Value = DateTime.Today;
            return d;
        }

        /// <summary>
        /// Selector de fecha para vencimientos: permite fechas futuras y trae una casilla;
        /// mientras la casilla esté sin marcar se considera "sin fecha" (así la fecha puede ser obligatoria).
        /// </summary>
        public static DateTimePicker CrearSelectorFechaVencimiento()
        {
            DateTimePicker d = new DateTimePicker();
            d.Format = DateTimePickerFormat.Short;
            d.Font = new Font("Segoe UI", 11.5f);
            d.ShowCheckBox = true;
            d.Checked = false;
            d.Value = DateTime.Today;
            return d;
        }

        /// <summary>Casilla de verificación con el estilo de la aplicación.</summary>
        public static CheckBox CrearCasilla(string texto)
        {
            CheckBox c = new CheckBox();
            c.Text = texto;
            c.Font = Fuente;
            c.ForeColor = Texto;
            c.BackColor = Color.Transparent;
            c.AutoSize = true;
            c.Cursor = Cursors.Hand;
            return c;
        }

        /// <summary>Cuadro de texto de varias líneas (el único que no tiene el alto estándar).</summary>
        public static TextBox CrearTextoMultilinea(int lineas, int maxLongitud)
        {
            TextBox t = CrearTextBox(false, maxLongitud);
            t.Multiline = true;
            t.ScrollBars = ScrollBars.Vertical;
            t.AcceptsReturn = true;
            t.Height = Fuente.Height * lineas + 10;
            return t;
        }

        /// <summary>Cuadro de solo lectura para mostrar datos (mismo alto, fondo de ventana).</summary>
        public static TextBox CrearTextoSoloLectura()
        {
            TextBox t = CrearTextBox();
            t.ReadOnly = true;
            t.BackColor = Fondo;
            t.TabStop = false;
            return t;
        }

        /// <summary>Etiqueta de texto (ayuda, aviso) que se parte en varias líneas sin pasar de un ancho máximo.</summary>
        public static Label CrearTexto(string texto, int anchoMaximo)
        {
            Label l = new Label();
            l.Text = texto;
            l.Font = Fuente;
            l.ForeColor = Texto;
            l.AutoSize = true;
            l.MaximumSize = new Size(anchoMaximo, 0);
            l.Margin = new Padding(0, 0, 0, EspacioCampos);
            return l;
        }

        /// <summary>Etiqueta para mensajes en línea (error o éxito). Úsese con Mensaje(...).</summary>
        public static Label CrearEtiquetaMensaje(int anchoMaximo)
        {
            Label l = CrearTexto(string.Empty, anchoMaximo);
            l.Font = FuenteNegrita;
            return l;
        }

        /// <summary>Muestra un mensaje en línea: rojo si es error, verde si es éxito.</summary>
        public static void Mensaje(Label etiqueta, string texto, bool esError)
        {
            etiqueta.ForeColor = esError ? Error : Exito;
            etiqueta.Text = texto;
        }

        // ---------- Botones ----------

        public static Button CrearBotonPrimario(string texto)
        {
            return CrearBoton(texto, Rojo);
        }

        public static Button CrearBotonSecundario(string texto)
        {
            return CrearBoton(texto, Gris);
        }

        public static Button CrearBotonPeligro(string texto)
        {
            return CrearBoton(texto, Error);
        }

        private static Button CrearBoton(string texto, Color fondo)
        {
            Button b = new Button();
            b.Text = texto;
            b.Font = FuenteNegrita;
            b.ForeColor = Color.White;
            b.BackColor = fondo;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = ControlPaint.Dark(fondo, 0.1f);
            b.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(fondo, 0.2f);
            b.UseVisualStyleBackColor = false;
            b.Cursor = Cursors.Hand;
            b.Height = AltoBoton;
            b.MinimumSize = new Size(0, AltoBotonMinimo);   // nunca se recorta el texto por falta de alto
            b.TextAlign = ContentAlignment.MiddleCenter;
            b.Dock = DockStyle.Fill;
            b.Margin = Padding.Empty;

            // Windows pinta en gris el texto de un botón plano DESHABILITADO. Se vuelve a pintar en blanco (con el
            // fondo atenuado) para que "Cancelar" y los botones grises siempre tengan texto blanco legible.
            b.Paint += delegate (object s, PaintEventArgs e)
            {
                Button boton = (Button)s;
                if (boton.Enabled) return;
                Color atenuado = Color.FromArgb(
                    (boton.BackColor.R * 70 + 255 * 30) / 100,
                    (boton.BackColor.G * 70 + 255 * 30) / 100,
                    (boton.BackColor.B * 70 + 255 * 30) / 100);
                using (SolidBrush pincel = new SolidBrush(atenuado))
                {
                    e.Graphics.FillRectangle(pincel, boton.ClientRectangle);
                }
                TextRenderer.DrawText(e.Graphics, boton.Text, boton.Font, boton.ClientRectangle, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                    TextFormatFlags.EndEllipsis);
            };
            b.EnabledChanged += delegate (object s, EventArgs e) { ((Button)s).Invalidate(); };
            return b;
        }

        /// <summary>Fila de botones del mismo ancho, repartidos en columnas iguales.</summary>
        public static TableLayoutPanel CrearFilaBotones(params Button[] botones)
        {
            TableLayoutPanel t = new TableLayoutPanel();
            t.ColumnCount = botones.Length;
            t.RowCount = 1;
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Dock = DockStyle.Fill;
            t.AutoSize = true;
            t.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            t.BackColor = Color.Transparent;
            t.Margin = Padding.Empty;
            t.Padding = Padding.Empty;

            for (int i = 0; i < botones.Length; i++)
            {
                t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / botones.Length));
                int izq = i == 0 ? 0 : EspacioCampos / 2;
                int der = i == botones.Length - 1 ? 0 : EspacioCampos / 2;
                botones[i].Margin = new Padding(izq, 0, der, 0);
                t.Controls.Add(botones[i], i, 0);
            }
            return t;
        }

        // ---------- Barra de estado y paginación ----------

        /// <summary>
        /// Barra de estado inferior con una etiqueta de texto. Se usa para mostrar "Consulta en X ms"
        /// (tiempo medido con Stopwatch) en cada ventana que consulta la base de datos.
        /// </summary>
        public static StatusStrip CrearBarraEstado(out ToolStripStatusLabel etiqueta)
        {
            StatusStrip barra = new StatusStrip();
            barra.Dock = DockStyle.Bottom;
            barra.SizingGrip = false;
            barra.BackColor = Recuadro;
            barra.Font = FuentePequena;

            etiqueta = new ToolStripStatusLabel();
            etiqueta.Text = "Listo";
            etiqueta.ForeColor = Gris;
            etiqueta.Spring = true;                        // ocupa todo el ancho
            etiqueta.TextAlign = ContentAlignment.MiddleLeft;
            barra.Items.Add(etiqueta);
            return barra;
        }

        /// <summary>Escribe "Consulta en X ms" en la barra de estado.</summary>
        public static void MostrarConsulta(ToolStripStatusLabel etiqueta, long milisegundos)
        {
            etiqueta.ForeColor = Gris;
            etiqueta.Text = "Consulta en " + milisegundos.ToString("N0") + " ms";
        }

        /// <summary>
        /// Fila de paginación: [Anterior]  Página 2 de 10 · 478 resultado(s)  [Siguiente].
        /// Los botones usan el estilo secundario (gris) con el mismo alto que los demás.
        /// </summary>
        public static TableLayoutPanel CrearPaginador(out Button anterior, out Button siguiente, out Label texto)
        {
            anterior = CrearBotonSecundario("◀ Anterior");
            siguiente = CrearBotonSecundario("Siguiente ▶");
            foreach (Button b in new[] { anterior, siguiente })
            {
                b.Dock = DockStyle.None;
                b.Anchor = AnchorStyles.None;
                b.Width = 130;
                b.Enabled = false;
            }

            texto = new Label();
            texto.Text = string.Empty;
            texto.Font = Fuente;
            texto.ForeColor = Texto;
            texto.AutoSize = false;
            texto.Dock = DockStyle.Fill;
            texto.TextAlign = ContentAlignment.MiddleCenter;
            texto.Margin = Padding.Empty;

            TableLayoutPanel t = new TableLayoutPanel();
            t.ColumnCount = 3;
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.RowCount = 1;
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Dock = DockStyle.Fill;
            t.AutoSize = true;
            t.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            t.Margin = new Padding(0, EspacioCampos, 0, 0);
            anterior.Margin = Padding.Empty;
            siguiente.Margin = Padding.Empty;
            t.Controls.Add(anterior, 0, 0);
            t.Controls.Add(texto, 1, 0);
            t.Controls.Add(siguiente, 2, 0);
            return t;
        }

        // ---------- Tablas ----------

        /// <summary>Estilo común de las tablas: encabezado rojo, filas alternadas, fila completa, solo lectura.</summary>
        public static void EstilizarGrid(DataGridView g)
        {
            g.Font = Fuente;
            g.BackgroundColor = Recuadro;
            g.BorderStyle = BorderStyle.None;
            g.GridColor = Borde;
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            g.EnableHeadersVisualStyles = false;
            g.RowHeadersVisible = false;

            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            g.ColumnHeadersHeight = 36;
            g.ColumnHeadersDefaultCellStyle.BackColor = Rojo;
            g.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            g.ColumnHeadersDefaultCellStyle.Font = FuenteNegrita;
            g.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;   // encabezado en una sola línea
            g.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Rojo;
            g.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;

            g.DefaultCellStyle.BackColor = Recuadro;
            g.DefaultCellStyle.ForeColor = Texto;
            g.DefaultCellStyle.SelectionBackColor = Dorado;
            g.DefaultCellStyle.SelectionForeColor = Texto;
            g.AlternatingRowsDefaultCellStyle.BackColor = FilaAlterna;
            g.AlternatingRowsDefaultCellStyle.ForeColor = Texto;
            g.AlternatingRowsDefaultCellStyle.SelectionBackColor = Dorado;
            g.AlternatingRowsDefaultCellStyle.SelectionForeColor = Texto;
            g.RowTemplate.Height = 30;

            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.MultiSelect = false;
            g.ReadOnly = true;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            g.Dock = DockStyle.Fill;
        }

        /// <summary>
        /// Pinta una fila de medicamento según su alerta: rojo si está VENCIDO, ámbar si tiene STOCK BAJO o
        /// está POR VENCER; sin alerta queda con el estilo normal. Se usa en Inventario y en el panel de inicio.
        /// </summary>
        public static void PintarFilaAlerta(DataGridViewRow fila, SIGEPOSTA.Negocio.Medicamento m)
        {
            Color fondo;
            Color texto;
            if (m.EstaVencido) { fondo = Error; texto = Color.White; }
            else if (m.EstaBajoStock || m.EstaPorVencer) { fondo = AlertaStock; texto = Texto; }
            else return;

            fila.DefaultCellStyle.BackColor = fondo;
            fila.DefaultCellStyle.ForeColor = texto;
            // Al seleccionarla se oscurece el mismo color, para que se note la selección sin perder la alerta.
            fila.DefaultCellStyle.SelectionBackColor = ControlPaint.Dark(fondo, 0.25f);
            fila.DefaultCellStyle.SelectionForeColor = Color.White;
        }

        // ---------- Mensajes ----------

        public static void MostrarError(IWin32Window dueno, string mensaje)
        {
            MessageBox.Show(dueno, mensaje, "SIGEPOSTA", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static void MostrarExito(IWin32Window dueno, string mensaje)
        {
            MessageBox.Show(dueno, mensaje, "SIGEPOSTA", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static void MostrarInfo(IWin32Window dueno, string mensaje)
        {
            MessageBox.Show(dueno, mensaje, "SIGEPOSTA", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
