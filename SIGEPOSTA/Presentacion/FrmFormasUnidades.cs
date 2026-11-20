using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SIGEPOSTA.Negocio;

namespace SIGEPOSTA.Presentacion
{
    /// <summary>
    /// Formas farmacéuticas y unidades de medida en UNA sola ventana (Administrador y Farmacia). Un selector arriba cambia
    /// qué catálogo se muestra; debajo van "Datos" (cuadrícula de 3 columnas del mismo ancho y botones a la derecha) y la
    /// lista con el número de medicamentos que usa cada elemento. Las reglas y los permisos están en CatalogoServicio.
    /// </summary>
    public class FrmFormasUnidades : Form
    {
        private readonly CatalogoServicio _servicio = new CatalogoServicio();
        private List<ItemCatalogo> _todos = new List<ItemCatalogo>();
        private int _idSeleccionado;   // 0 = elemento nuevo
        private bool _cargando;

        private ComboBox _cboCatalogo;
        private TextBox _txtNombre, _txtBuscar;
        private Label _errNombre;
        private Button _btnLimpiar, _btnEliminar, _btnGuardar;
        private GroupBox _grpDatos, _grpLista;
        private DataGridView _grid;
        private ToolStripStatusLabel _lblConsulta;

        public FrmFormasUnidades()
        {
            // Si el rol no corresponde, ni siquiera se construye la pantalla.
            Permisos.ExigirAccesoCatalogosMedicamento();
            Construir();
            Load += FrmFormasUnidades_Load;
        }

        private TipoCatalogo Tipo
        {
            get { return _cboCatalogo.SelectedIndex == 1 ? TipoCatalogo.Unidad : TipoCatalogo.Forma; }
        }

        // ================== Construcción de la pantalla ==================

        private void Construir()
        {
            Text = "SIGEPOSTA - Formas y unidades";
            Font = Estilo.Fuente;
            BackColor = Estilo.Fondo;
            ForeColor = Estilo.Texto;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1100, 760);
            MinimumSize = new Size(900, 640);

            TableLayoutPanel raiz = new TableLayoutPanel();
            raiz.Dock = DockStyle.Fill;
            raiz.ColumnCount = 1;
            raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            raiz.RowCount = 4;
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // barra de título
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // selector
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // datos
            raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // lista (ocupa el resto)

            raiz.Controls.Add(Estilo.CrearBarraTitulo("Formas farmacéuticas y unidades de medida"), 0, 0);
            raiz.Controls.Add(ConstruirSelector(), 0, 1);
            _grpDatos = ConstruirDatos();
            raiz.Controls.Add(_grpDatos, 0, 2);
            _grpLista = ConstruirLista();
            raiz.Controls.Add(_grpLista, 0, 3);
            Controls.Add(raiz);

            Controls.Add(Estilo.CrearBarraEstado(out _lblConsulta));

            _cboCatalogo.SelectedIndexChanged += delegate { AlCambiarCatalogo(); };
            _btnLimpiar.Click += delegate { Limpiar(); };
            _btnEliminar.Click += BtnEliminar_Click;
            _btnGuardar.Click += BtnGuardar_Click;
            _txtBuscar.TextChanged += delegate { Mostrar(); };
            _grid.SelectionChanged += Grid_SelectionChanged;

            Estilo.VincularEnter(this, _grpDatos, _btnGuardar);
            Estilo.SinEnter(this, _txtBuscar);
            AcceptButton = _btnGuardar;
            Estilo.EscCierra(this);
        }

        private GroupBox ConstruirSelector()
        {
            _cboCatalogo = Estilo.CrearCombo();
            _cboCatalogo.Items.Add("Formas farmacéuticas");
            _cboCatalogo.Items.Add("Unidades de medida");

            // Misma cuadrícula de 3 columnas iguales: el selector ocupa la primera.
            TableLayoutPanel cuadricula = CrearCuadricula(1);
            TableLayoutPanel c = Estilo.CrearCampo("Catálogo", _cboCatalogo);
            c.Margin = new Padding(0, 0, Estilo.EspacioCampos, Estilo.EspacioCampos);
            cuadricula.Controls.Add(c, 0, 0);

            GroupBox g = Estilo.CrearRecuadro("Qué desea administrar");
            g.Controls.Add(cuadricula);
            return g;
        }

        private GroupBox ConstruirDatos()
        {
            _txtNombre = Estilo.CrearTextBox(false, ItemCatalogo.MaxNombre);

            TableLayoutPanel cuadricula = CrearCuadricula(1);
            TableLayoutPanel c1 = Estilo.CrearCampoConError("Nombre", _txtNombre, out _errNombre);
            c1.Margin = new Padding(0, 0, Estilo.EspacioCampos, Estilo.EspacioCampos);
            cuadricula.Controls.Add(c1, 0, 0);
            cuadricula.SetColumnSpan(c1, 2);

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

            GroupBox g = Estilo.CrearRecuadro("Datos");
            g.Controls.Add(contenido);
            return g;
        }

        /// <summary>Cuadrícula de 3 columnas iguales (el campo ocupa 1 o 2 de ellas).</summary>
        private static TableLayoutPanel CrearCuadricula(int filas)
        {
            TableLayoutPanel t = new TableLayoutPanel();
            t.ColumnCount = 3;
            for (int i = 0; i < 3; i++) t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3f));
            t.RowCount = filas;
            for (int i = 0; i < filas; i++) t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Dock = DockStyle.Fill;
            t.AutoSize = true;
            t.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            t.Margin = Padding.Empty;
            return t;
        }

        private GroupBox ConstruirLista()
        {
            _txtBuscar = Estilo.CrearTextBox();

            _grid = new DataGridView();
            Estilo.EstilizarGrid(_grid);
            _grid.Columns.Add("Nombre", "Nombre");
            _grid.Columns.Add("Usan", "Medicamentos que lo usan");
            _grid.Columns["Nombre"].FillWeight = 70;
            _grid.Columns["Usan"].FillWeight = 30;
            _grid.Columns["Usan"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            TableLayoutPanel contenido = Estilo.CrearColumna();
            contenido.AutoSize = false;
            contenido.RowCount = 2;
            contenido.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            contenido.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            contenido.Controls.Add(Estilo.CrearCampo("Buscar por nombre", _txtBuscar), 0, 0);
            contenido.Controls.Add(_grid, 0, 1);

            GroupBox g = Estilo.CrearRecuadro("Lista");
            g.AutoSize = false; // la lista ocupa todo el espacio que sobra
            g.Margin = new Padding(Estilo.MargenInterior, 0, Estilo.MargenInterior, Estilo.MargenInterior);
            g.Controls.Add(contenido);
            return g;
        }

        // ================== Carga de datos ==================

        private void FrmFormasUnidades_Load(object sender, EventArgs e)
        {
            try
            {
                _cboCatalogo.SelectedIndex = 0;   // dispara AlCambiarCatalogo → carga la lista
            }
            catch (AccesoDenegadoException ex)
            {
                Estilo.MostrarError(this, ex.Message);
                Close();
            }
        }

        private void AlCambiarCatalogo()
        {
            bool formas = Tipo == TipoCatalogo.Forma;
            _grpDatos.Text = formas ? "Datos de la forma farmacéutica" : "Datos de la unidad de medida";
            _grpLista.Text = formas ? "Formas farmacéuticas" : "Unidades de medida";
            _txtBuscar.Clear();
            Recargar();
            Limpiar();
        }

        /// <summary>Vuelve a leer el catálogo elegido (son pocas filas: no hace falta paginar).</summary>
        private void Recargar()
        {
            try
            {
                Stopwatch reloj = Stopwatch.StartNew();
                _todos = _servicio.ListarActivos(Tipo);
                reloj.Stop();
                Mostrar();
                Estilo.MostrarConsulta(_lblConsulta, reloj.ElapsedMilliseconds);
            }
            catch (AccesoDenegadoException ex)
            {
                Estilo.MostrarError(this, ex.Message);
                Close();
            }
            catch (Exception)
            {
                _lblConsulta.ForeColor = Estilo.Error;
                _lblConsulta.Text = "No se pudo consultar. Verifique la conexión con la base de datos.";
            }
        }

        /// <summary>Aplica el texto de búsqueda (en memoria, con LINQ) y pinta la tabla.</summary>
        private void Mostrar()
        {
            string t = _txtBuscar.Text.Trim();
            IEnumerable<ItemCatalogo> filas = _todos;
            if (t.Length > 0)
                filas = filas.Where(i => i.Nombre.IndexOf(t, StringComparison.CurrentCultureIgnoreCase) >= 0);

            _cargando = true;
            try
            {
                _grid.Rows.Clear();
                foreach (ItemCatalogo i in filas)
                {
                    int n = _grid.Rows.Add(i.Nombre, i.MedicamentosQueLoUsan);
                    _grid.Rows[n].Tag = i;
                }
                _grid.ClearSelection();
            }
            finally
            {
                _cargando = false;
            }
        }

        private ItemCatalogo Seleccionado()
        {
            if (_grid.SelectedRows.Count == 0) return null;
            return _grid.SelectedRows[0].Tag as ItemCatalogo;
        }

        private void Grid_SelectionChanged(object sender, EventArgs e)
        {
            if (_cargando) return;
            ItemCatalogo i = Seleccionado();
            if (i == null) return;

            Estilo.MostrarErrorCampo(_errNombre, null);
            _idSeleccionado = i.Id;
            _txtNombre.Text = i.Nombre;

            // "Sin definir" es el valor de los medicamentos antiguos: se ve, pero no se modifica ni se da de baja.
            bool editable = !i.EsSinDefinir;
            _txtNombre.Enabled = editable;
            _btnGuardar.Enabled = editable;
            _btnEliminar.Enabled = editable;
            if (!editable)
                Estilo.MostrarErrorCampo(_errNombre, "\"" + Medicamento.SinDefinir + "\" es el valor de los medicamentos antiguos y no se puede modificar ni dar de baja.");
        }

        // ================== Acciones ==================

        private void Limpiar()
        {
            _idSeleccionado = 0;
            _txtNombre.Enabled = true;
            _txtNombre.Clear();
            _btnGuardar.Enabled = true;
            _btnEliminar.Enabled = false;
            _grid.ClearSelection();
            Estilo.MostrarErrorCampo(_errNombre, null);
            _txtNombre.Focus();
        }

        private void BtnGuardar_Click(object sender, EventArgs e)
        {
            Estilo.MostrarErrorCampo(_errNombre, null);
            Dictionary<string, string> errores = _servicio.Validar(_txtNombre.Text);
            if (errores.Count > 0)
            {
                Estilo.MostrarErrorCampo(_errNombre, errores["Nombre"]);
                return;
            }

            try
            {
                if (_idSeleccionado == 0)
                {
                    _servicio.Registrar(Tipo, _txtNombre.Text);
                    Recargar();
                    Limpiar();
                    Estilo.MostrarExito(this, Tipo == TipoCatalogo.Forma ? "Forma farmacéutica registrada." : "Unidad de medida registrada.");
                }
                else
                {
                    _servicio.Actualizar(Tipo, _idSeleccionado, _txtNombre.Text);
                    Recargar();
                    Limpiar();
                    Estilo.MostrarExito(this, "Nombre actualizado.");
                }
            }
            catch (ValidacionException ex)
            {
                Estilo.MostrarErrorCampo(_errNombre, ex.Message);   // p. ej. "Ya existe una forma farmacéutica con ese nombre."
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
                Estilo.MostrarError(this, "No se pudo guardar. Verifique la conexión con la base de datos.");
            }
        }

        private void BtnEliminar_Click(object sender, EventArgs e)
        {
            if (_idSeleccionado == 0) return;

            string que = CatalogoServicio.NombreTipo(Tipo);
            string aviso = "¿Dar de baja esta " + (Tipo == TipoCatalogo.Forma ? "forma farmacéutica" : "unidad de medida") +
                           "?\nDejará de ofrecerse en medicamentos nuevos.";
            try
            {
                int usan = _servicio.ContarMedicamentos(Tipo, _idSeleccionado);
                if (usan > 0)
                    aviso = "Esta " + que + " la usan " + usan.ToString("N0") + " medicamento(s) activo(s).\n" +
                            "Seguirán mostrándola, pero ya no se ofrecerá en medicamentos nuevos ni al editarlos.\n\n¿Dar de baja de todas formas?";
            }
            catch (AccesoDenegadoException ex)
            {
                Estilo.MostrarError(this, ex.Message);
                return;
            }
            catch (Exception)
            {
                // El conteo es solo informativo: si falla, se pregunta igual con el texto básico.
            }

            DialogResult r = MessageBox.Show(this, aviso, "SIGEPOSTA",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (r != DialogResult.Yes) return;

            try
            {
                _servicio.DarDeBaja(Tipo, _idSeleccionado);
                Recargar();
                Limpiar();
                Estilo.MostrarExito(this, "Dado de baja.");
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
                Estilo.MostrarError(this, "No se pudo dar de baja. Verifique la conexión con la base de datos.");
            }
        }
    }
}
