using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SIGEPOSTA.Negocio;

namespace SIGEPOSTA.Presentacion
{
    /// <summary>
    /// Mantenimiento del catálogo de diagnósticos CIE-10 (solo Administrador). Arriba el recuadro "Datos del código"
    /// (cuadrícula de 3 columnas del mismo ancho y botones a la derecha); abajo "Catálogo CIE-10" con búsqueda y paginación.
    /// La pantalla solo recoge datos y muestra errores; las reglas y los permisos están en Cie10Servicio.
    /// </summary>
    public class FrmCie10 : Form
    {
        private readonly Cie10Servicio _servicio = new Cie10Servicio();
        private ControladorBusqueda<Cie10> _busqueda;
        private ResultadoPagina<Cie10> _resultado;
        private Button _btnAnterior, _btnSiguiente;
        private Label _lblPagina;
        private ToolStripStatusLabel _lblConsulta;
        private string _codigoSeleccionado;   // null = código nuevo
        private bool _cargando;

        private TextBox _txtCodigo, _txtDescripcion, _txtBuscar;
        private Label _errCodigo, _errDescripcion;
        private Button _btnLimpiar, _btnEliminar, _btnGuardar;
        private DataGridView _grid;

        public FrmCie10()
        {
            // Si el rol no corresponde, ni siquiera se construye la pantalla.
            Permisos.ExigirAdministrador();
            Construir();
            Load += FrmCie10_Load;
        }

        // ================== Construcción de la pantalla ==================

        private void Construir()
        {
            Text = "SIGEPOSTA - Catálogo CIE-10";
            Font = Estilo.Fuente;
            BackColor = Estilo.Fondo;
            ForeColor = Estilo.Texto;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1100, 700);
            MinimumSize = new Size(900, 560);

            TableLayoutPanel raiz = new TableLayoutPanel();
            raiz.Dock = DockStyle.Fill;
            raiz.ColumnCount = 1;
            raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // barra de título
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // datos del código
            raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // lista (ocupa el resto)
            raiz.RowCount = 3;

            raiz.Controls.Add(Estilo.CrearBarraTitulo("Catálogo de diagnósticos CIE-10"), 0, 0);
            GroupBox datos = ConstruirDatos();
            raiz.Controls.Add(datos, 0, 1);
            raiz.Controls.Add(ConstruirLista(), 0, 2);
            Controls.Add(raiz);

            Controls.Add(Estilo.CrearBarraEstado(out _lblConsulta));

            _btnLimpiar.Click += delegate { Limpiar(); };
            _btnEliminar.Click += BtnEliminar_Click;
            _btnGuardar.Click += BtnGuardar_Click;

            Estilo.VincularEnter(this, datos, _btnGuardar);
            Estilo.SinEnter(this, _txtBuscar);
            AcceptButton = _btnGuardar;
            Estilo.EscCierra(this);
            _busqueda = new ControladorBusqueda<Cie10>(
                delegate (string texto, int pagina, object filtro, Func<bool> cancelado) { return _servicio.BuscarPagina(texto, pagina, cancelado); },
                Mostrar, Fallar, CambiarEstado);
            _txtBuscar.TextChanged += delegate { _busqueda.TextoCambio(_txtBuscar.Text); };   // consulta tras 300 ms sin escribir
            _btnAnterior.Click += delegate { if (_resultado != null) _busqueda.IrAPagina(_resultado.Pagina - 1); };
            _btnSiguiente.Click += delegate { if (_resultado != null) _busqueda.IrAPagina(_resultado.Pagina + 1); };
            FormClosed += delegate { _busqueda.Dispose(); };
            _grid.SelectionChanged += Grid_SelectionChanged;
        }

        private GroupBox ConstruirDatos()
        {
            _txtCodigo = Estilo.CrearTextBox(false, 7);
            _txtDescripcion = Estilo.CrearTextBox(false, Cie10.MaxDescripcion);
            Estilo.PonerAyuda(_txtCodigo, "Ej.: J06.9");

            // Cuadrícula de 3 columnas iguales: Código | Descripción (ocupa 2 columnas).
            TableLayoutPanel cuadricula = new TableLayoutPanel();
            cuadricula.ColumnCount = 3;
            for (int i = 0; i < 3; i++) cuadricula.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3f));
            cuadricula.RowCount = 1;
            cuadricula.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            cuadricula.Dock = DockStyle.Fill;
            cuadricula.AutoSize = true;
            cuadricula.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            cuadricula.Margin = Padding.Empty;

            TableLayoutPanel c1 = Estilo.CrearCampoConError("Código (letra + 2 dígitos, ej. J06.9)", _txtCodigo, out _errCodigo);
            TableLayoutPanel c2 = Estilo.CrearCampoConError("Descripción", _txtDescripcion, out _errDescripcion);
            c1.Margin = new Padding(0, 0, Estilo.EspacioCampos, Estilo.EspacioCampos);
            c2.Margin = new Padding(0, 0, 0, Estilo.EspacioCampos);
            cuadricula.Controls.Add(c1, 0, 0);
            cuadricula.Controls.Add(c2, 1, 0);
            cuadricula.SetColumnSpan(c2, 2);

            // Botones a la derecha, de arriba hacia abajo: Limpiar, Eliminar, Guardar.
            _btnLimpiar = Estilo.CrearBotonSecundario("Limpiar");
            _btnEliminar = Estilo.CrearBotonPeligro("Eliminar");
            _btnGuardar = Estilo.CrearBotonPrimario("Guardar");
            TableLayoutPanel botones = new TableLayoutPanel();
            botones.ColumnCount = 1;
            botones.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            botones.Dock = DockStyle.Top;
            botones.AutoSize = true;
            botones.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            botones.Margin = new Padding(Estilo.MargenInterior, 0, 0, 0);
            _btnLimpiar.Margin = new Padding(0, 0, 0, Estilo.EspacioCampos);
            _btnEliminar.Margin = new Padding(0, 0, 0, Estilo.EspacioCampos);
            Estilo.AgregarFila(botones, _btnLimpiar);
            Estilo.AgregarFila(botones, _btnEliminar);
            Estilo.AgregarFila(botones, _btnGuardar);

            TableLayoutPanel contenido = new TableLayoutPanel();
            contenido.ColumnCount = 2;
            contenido.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            contenido.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            contenido.RowCount = 1;
            contenido.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            contenido.Dock = DockStyle.Fill;
            contenido.AutoSize = true;
            contenido.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            contenido.Margin = Padding.Empty;
            contenido.Controls.Add(cuadricula, 0, 0);
            contenido.Controls.Add(botones, 1, 0);

            GroupBox g = Estilo.CrearRecuadro("Datos del código");
            g.Controls.Add(contenido);
            return g;
        }

        private GroupBox ConstruirLista()
        {
            _txtBuscar = Estilo.CrearTextBox();

            _grid = new DataGridView();
            Estilo.EstilizarGrid(_grid);
            _grid.Columns.Add("Codigo", "Código");
            _grid.Columns.Add("Descripcion", "Descripción");
            _grid.Columns["Codigo"].FillWeight = 12;
            _grid.Columns["Descripcion"].FillWeight = 88;

            TableLayoutPanel paginador = Estilo.CrearPaginador(out _btnAnterior, out _btnSiguiente, out _lblPagina);

            TableLayoutPanel contenido = Estilo.CrearColumna();
            contenido.AutoSize = false;
            contenido.RowCount = 3;
            contenido.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            contenido.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            contenido.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            contenido.Controls.Add(Estilo.CrearCampo("Buscar por código o parte del nombre (sin importar tildes ni mayúsculas)", _txtBuscar), 0, 0);
            contenido.Controls.Add(_grid, 0, 1);
            contenido.Controls.Add(paginador, 0, 2);

            GroupBox g = Estilo.CrearRecuadro("Catálogo CIE-10");
            g.AutoSize = false; // la lista ocupa todo el espacio que sobra
            g.Margin = new Padding(Estilo.MargenInterior, 0, Estilo.MargenInterior, Estilo.MargenInterior);
            g.Controls.Add(contenido);
            return g;
        }

        // ================== Carga de datos ==================

        private void FrmCie10_Load(object sender, EventArgs e)
        {
            try
            {
                RecargarLista();
                Limpiar();
            }
            catch (AccesoDenegadoException ex)
            {
                Estilo.MostrarError(this, ex.Message);
                Close();
            }
            catch (Exception)
            {
                Estilo.MostrarError(this, "No se pudieron cargar los datos. Verifique la conexión con la base de datos.");
            }
        }

        private void RecargarLista()
        {
            if (_resultado == null) _busqueda.BuscarYa(); else _busqueda.Recargar();
        }

        private void Mostrar(ResultadoPagina<Cie10> r)
        {
            _resultado = r;
            _cargando = true;
            try
            {
                _grid.Rows.Clear();
                foreach (Cie10 c in r.Items)
                {
                    int i = _grid.Rows.Add(c.Codigo, c.Descripcion);
                    _grid.Rows[i].Tag = c;
                }
                _grid.ClearSelection();
            }
            finally
            {
                _cargando = false;
            }

            _lblPagina.Text = r.TextoPagina;
            _btnAnterior.Enabled = r.HayAnterior;
            _btnSiguiente.Enabled = r.HaySiguiente;
            Estilo.MostrarConsulta(_lblConsulta, r.Milisegundos);
        }

        private void CambiarEstado(bool buscando)
        {
            if (!buscando) return;
            _lblConsulta.ForeColor = Estilo.Morado;
            _lblConsulta.Text = "Buscando…";
        }

        private void Fallar(Exception ex)
        {
            _lblConsulta.ForeColor = Estilo.Error;
            if (ex is AccesoDenegadoException)
            {
                _lblConsulta.Text = ex.Message;
                return;
            }
            _lblConsulta.Text = "No se pudo consultar. Verifique la conexión con la base de datos. (Código: " + ManejadorErrores.Registrar(ex) + ")";
        }

        private Cie10 Seleccionado()
        {
            if (_grid.SelectedRows.Count == 0) return null;
            return _grid.SelectedRows[0].Tag as Cie10;
        }

        private void Grid_SelectionChanged(object sender, EventArgs e)
        {
            if (_cargando) return;
            Cie10 c = Seleccionado();
            if (c == null) return;

            LimpiarErrores();
            _codigoSeleccionado = c.Codigo;
            _txtCodigo.Text = c.Codigo;
            _txtCodigo.Enabled = false;   // el código es la clave y puede estar en atenciones: no se edita
            _txtDescripcion.Text = c.Descripcion;
            _btnEliminar.Enabled = true;
        }

        // ================== Acciones ==================

        private void Limpiar()
        {
            _codigoSeleccionado = null;
            _txtCodigo.Enabled = true;
            _txtCodigo.Clear();
            _txtDescripcion.Clear();
            _grid.ClearSelection();
            LimpiarErrores();
            _btnEliminar.Enabled = false;
            _txtCodigo.Focus();
        }

        private void LimpiarErrores()
        {
            Estilo.MostrarErrorCampo(_errCodigo, null);
            Estilo.MostrarErrorCampo(_errDescripcion, null);
        }

        private void MostrarErrores(Dictionary<string, string> errores)
        {
            string m;
            if (errores.TryGetValue("Codigo", out m)) Estilo.MostrarErrorCampo(_errCodigo, m);
            if (errores.TryGetValue("Descripcion", out m)) Estilo.MostrarErrorCampo(_errDescripcion, m);
        }

        private void BtnGuardar_Click(object sender, EventArgs e)
        {
            LimpiarErrores();
            bool esNuevo = _codigoSeleccionado == null;

            Dictionary<string, string> errores = _servicio.Validar(_txtCodigo.Text, _txtDescripcion.Text, esNuevo);
            if (errores.Count > 0)
            {
                MostrarErrores(errores);
                return;
            }

            try
            {
                if (esNuevo)
                {
                    _servicio.Registrar(_txtCodigo.Text, _txtDescripcion.Text);
                    RecargarLista();
                    Limpiar();
                    Estilo.MostrarExito(this, "Código CIE-10 registrado.");
                }
                else
                {
                    _servicio.Actualizar(_codigoSeleccionado, _txtDescripcion.Text);
                    RecargarLista();
                    Limpiar();
                    Estilo.MostrarExito(this, "Descripción actualizada.");
                }
            }
            catch (ValidacionException ex)
            {
                Dictionary<string, string> uno = new Dictionary<string, string>();
                uno[ex.Campo ?? string.Empty] = ex.Message;   // p. ej. "Ese código CIE-10 ya existe en el catálogo."
                MostrarErrores(uno);
            }
            catch (AccesoDenegadoException ex)
            {
                Estilo.MostrarError(this, ex.Message);
            }
            catch (ReglaNegocioException ex)
            {
                Estilo.MostrarError(this, ex.Message);
            }
            catch (Exception)
            {
                Estilo.MostrarError(this, "No se pudo guardar el código. Verifique la conexión con la base de datos.");
            }
        }

        private void BtnEliminar_Click(object sender, EventArgs e)
        {
            if (_codigoSeleccionado == null) return;

            DialogResult r = MessageBox.Show(this,
                "¿Dar de baja este código CIE-10?\nDejará de ofrecerse al registrar atenciones; las atenciones antiguas lo siguen mostrando.",
                "SIGEPOSTA", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (r != DialogResult.Yes) return;

            try
            {
                _servicio.DarDeBaja(_codigoSeleccionado);
                RecargarLista();
                Limpiar();
                Estilo.MostrarExito(this, "Código dado de baja.");
            }
            catch (AccesoDenegadoException ex)
            {
                Estilo.MostrarError(this, ex.Message);
            }
            catch (ReglaNegocioException ex)
            {
                Estilo.MostrarError(this, ex.Message);
            }
            catch (Exception)
            {
                Estilo.MostrarError(this, "No se pudo dar de baja el código. Verifique la conexión con la base de datos.");
            }
        }
    }
}
