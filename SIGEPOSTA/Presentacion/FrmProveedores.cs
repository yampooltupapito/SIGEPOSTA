using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SIGEPOSTA.Negocio;

namespace SIGEPOSTA.Presentacion
{
    /// <summary>
    /// Proveedores de medicamentos. Arriba el recuadro "Datos del proveedor" (cuadrícula de 3 columnas del mismo
    /// ancho y botones a la derecha); abajo "Lista de proveedores" con búsqueda y paginación.
    /// La pantalla solo recoge datos y muestra errores; las reglas y los permisos están en ProveedorServicio.
    /// </summary>
    public class FrmProveedores : Form
    {
        private readonly ProveedorServicio _servicio = new ProveedorServicio();
        // Búsqueda mientras se escribe (espera de 300 ms), en segundo plano y paginada de a 50.
        private ControladorBusqueda<Proveedor> _busqueda;
        private ResultadoPagina<Proveedor> _resultado;
        private Button _btnAnterior, _btnSiguiente;
        private Label _lblPagina;
        private ToolStripStatusLabel _lblConsulta;
        private int _idSeleccionado; // 0 = proveedor nuevo
        private bool _cargando;      // evita reaccionar a eventos mientras se llena la tabla

        private TextBox _txtRuc, _txtNombre, _txtTelefono, _txtCorreo, _txtDireccion, _txtBuscar;
        private Label _errRuc, _errNombre, _errTelefono, _errCorreo, _errDireccion;
        private Button _btnLimpiar, _btnEliminar, _btnGuardar;
        private DataGridView _grid;

        public FrmProveedores()
        {
            // Si el rol no corresponde, ni siquiera se construye la pantalla.
            Permisos.ExigirAccesoProveedores();
            Construir();
            Load += FrmProveedores_Load;
        }

        // ================== Construcción de la pantalla ==================

        private void Construir()
        {
            Text = "SIGEPOSTA - Proveedores";
            Font = Estilo.Fuente;
            BackColor = Estilo.Fondo;
            ForeColor = Estilo.Texto;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1100, 740);
            MinimumSize = new Size(900, 620);

            TableLayoutPanel raiz = new TableLayoutPanel();
            raiz.Dock = DockStyle.Fill;
            raiz.ColumnCount = 1;
            raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // barra de título
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // datos del proveedor
            raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // lista (ocupa el resto)
            raiz.RowCount = 3;

            raiz.Controls.Add(Estilo.CrearBarraTitulo("Proveedores de medicamentos"), 0, 0);
            GroupBox datos = ConstruirDatos();
            raiz.Controls.Add(datos, 0, 1);
            raiz.Controls.Add(ConstruirLista(), 0, 2);
            Controls.Add(raiz);

            // Barra de estado: "Consulta en X ms" (se agrega después del contenido para acoplarse abajo).
            Controls.Add(Estilo.CrearBarraEstado(out _lblConsulta));

            _btnLimpiar.Click += delegate { Limpiar(); };
            _btnEliminar.Click += BtnEliminar_Click;
            _btnGuardar.Click += BtnGuardar_Click;

            // Enter en los campos de datos = Guardar; en el cuadro de búsqueda no hace nada; Esc cierra.
            Estilo.VincularEnter(this, datos, _btnGuardar);
            Estilo.SinEnter(this, _txtBuscar);
            AcceptButton = _btnGuardar;
            Estilo.EscCierra(this);
            _busqueda = new ControladorBusqueda<Proveedor>(
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
            _txtRuc = Estilo.CrearTextBox(false, 11);
            _txtNombre = Estilo.CrearTextBox(false, Proveedor.MaxNombre);
            _txtTelefono = Estilo.CrearTextBox(false, 9);
            _txtCorreo = Estilo.CrearTextBox(false, Proveedor.MaxCorreo);
            _txtDireccion = Estilo.CrearTextBox(false, Proveedor.MaxDireccion);
            _txtRuc.KeyPress += SoloDigitos;
            _txtTelefono.KeyPress += SoloDigitos;

            // Cuadrícula de 3 columnas iguales, 2 filas: RUC | Razón social | Teléfono ; Correo | Dirección (2 columnas).
            TableLayoutPanel cuadricula = new TableLayoutPanel();
            cuadricula.ColumnCount = 3;
            for (int i = 0; i < 3; i++) cuadricula.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3f));
            cuadricula.RowCount = 2;
            cuadricula.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            cuadricula.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            cuadricula.Dock = DockStyle.Fill;
            cuadricula.AutoSize = true;
            cuadricula.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            cuadricula.Margin = Padding.Empty;

            AgregarCampo(cuadricula, "RUC (11 dígitos)", _txtRuc, out _errRuc, 0, 0);
            AgregarCampo(cuadricula, "Nombre o razón social", _txtNombre, out _errNombre, 1, 0);
            AgregarCampo(cuadricula, "Teléfono (opcional)", _txtTelefono, out _errTelefono, 2, 0);
            AgregarCampo(cuadricula, "Correo (opcional)", _txtCorreo, out _errCorreo, 0, 1);
            AgregarCampo(cuadricula, "Dirección (opcional)", _txtDireccion, out _errDireccion, 1, 1);
            // La dirección ocupa las 2 columnas restantes de la fila 2.
            cuadricula.SetColumnSpan(cuadricula.GetControlFromPosition(1, 1), 2);
            cuadricula.GetControlFromPosition(1, 1).Margin = new Padding(0, 0, 0, Estilo.EspacioCampos);

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

            GroupBox g = Estilo.CrearRecuadro("Datos del proveedor");
            g.Controls.Add(contenido);
            return g;
        }

        /// <summary>Agrega un campo (con su error debajo) a la cuadrícula; separa 12 px de la columna vecina.</summary>
        private static void AgregarCampo(TableLayoutPanel cuadricula, string etiqueta, Control control, out Label error, int col, int fila)
        {
            TableLayoutPanel campo = Estilo.CrearCampoConError(etiqueta, control, out error);
            campo.Margin = new Padding(0, 0, col < 2 ? Estilo.EspacioCampos : 0, Estilo.EspacioCampos);
            cuadricula.Controls.Add(campo, col, fila);
        }

        private GroupBox ConstruirLista()
        {
            _txtBuscar = Estilo.CrearTextBox();

            _grid = new DataGridView();
            Estilo.EstilizarGrid(_grid);
            _grid.Columns.Add("Nombre", "Nombre o razón social");
            _grid.Columns.Add("Ruc", "RUC");
            _grid.Columns.Add("Telefono", "Teléfono");
            _grid.Columns.Add("Correo", "Correo");
            _grid.Columns["Nombre"].FillWeight = 40;
            _grid.Columns["Ruc"].FillWeight = 15;
            _grid.Columns["Telefono"].FillWeight = 13;
            _grid.Columns["Correo"].FillWeight = 32;

            TableLayoutPanel paginador = Estilo.CrearPaginador(out _btnAnterior, out _btnSiguiente, out _lblPagina);

            TableLayoutPanel contenido = Estilo.CrearColumna();
            contenido.AutoSize = false;
            contenido.RowCount = 3;
            contenido.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            contenido.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            contenido.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            contenido.Controls.Add(Estilo.CrearCampo("Buscar por nombre o RUC (escriba el inicio)", _txtBuscar), 0, 0);
            contenido.Controls.Add(_grid, 0, 1);
            contenido.Controls.Add(paginador, 0, 2);

            GroupBox g = Estilo.CrearRecuadro("Lista de proveedores");
            g.AutoSize = false; // la lista ocupa todo el espacio que sobra
            g.Margin = new Padding(Estilo.MargenInterior, 0, Estilo.MargenInterior, Estilo.MargenInterior);
            g.Controls.Add(contenido);
            return g;
        }

        // ================== Carga de datos ==================

        private void FrmProveedores_Load(object sender, EventArgs e)
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

        /// <summary>Vuelve a consultar la página actual (tras guardar, dar de baja, etc.).</summary>
        private void RecargarLista()
        {
            if (_resultado == null) _busqueda.BuscarYa(); else _busqueda.Recargar();
        }

        /// <summary>Muestra UNA página de proveedores (llega del servicio ya paginada).</summary>
        private void Mostrar(ResultadoPagina<Proveedor> r)
        {
            _resultado = r;

            _cargando = true;
            try
            {
                _grid.Rows.Clear();
                foreach (Proveedor p in r.Items)
                {
                    int i = _grid.Rows.Add(p.Nombre, p.Ruc, p.Telefono, p.Correo);
                    _grid.Rows[i].Tag = p;
                }
                _grid.ClearSelection();
            }
            finally
            {
                _cargando = false;
            }

            _lblPagina.Text = r.TextoPagina;                 // "Página 2 de 10 · 478 resultado(s)"
            _btnAnterior.Enabled = r.HayAnterior;
            _btnSiguiente.Enabled = r.HaySiguiente;
            Estilo.MostrarConsulta(_lblConsulta, r.Milisegundos);   // "Consulta en X ms"
        }

        private void CambiarEstado(bool buscando)
        {
            if (!buscando) return;
            _lblConsulta.ForeColor = Estilo.Morado;
            _lblConsulta.Text = "Buscando…";
        }

        /// <summary>Un error de la búsqueda nunca muestra texto técnico: mensaje amable y código de referencia.</summary>
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

        private Proveedor ProveedorSeleccionado()
        {
            if (_grid.SelectedRows.Count == 0) return null;
            return _grid.SelectedRows[0].Tag as Proveedor;
        }

        private void Grid_SelectionChanged(object sender, EventArgs e)
        {
            if (_cargando) return;
            Proveedor p = ProveedorSeleccionado();
            if (p == null) return;

            LimpiarErrores();
            _idSeleccionado = p.IdProveedor;
            _txtRuc.Text = p.Ruc;
            _txtNombre.Text = p.Nombre;
            _txtTelefono.Text = p.Telefono;
            _txtCorreo.Text = p.Correo;
            _txtDireccion.Text = p.Direccion;
            _btnEliminar.Enabled = true;
        }

        // ================== Acciones ==================

        private void Limpiar()
        {
            _idSeleccionado = 0;
            _txtRuc.Clear();
            _txtNombre.Clear();
            _txtTelefono.Clear();
            _txtCorreo.Clear();
            _txtDireccion.Clear();
            _grid.ClearSelection();
            LimpiarErrores();
            _btnEliminar.Enabled = false;
            _txtRuc.Focus();
        }

        private void LimpiarErrores()
        {
            foreach (Label l in new[] { _errRuc, _errNombre, _errTelefono, _errCorreo, _errDireccion })
                Estilo.MostrarErrorCampo(l, null);
        }

        private void MostrarErrores(Dictionary<string, string> errores)
        {
            string m;
            if (errores.TryGetValue("Ruc", out m)) Estilo.MostrarErrorCampo(_errRuc, m);
            if (errores.TryGetValue("Nombre", out m)) Estilo.MostrarErrorCampo(_errNombre, m);
            if (errores.TryGetValue("Telefono", out m)) Estilo.MostrarErrorCampo(_errTelefono, m);
            if (errores.TryGetValue("Correo", out m)) Estilo.MostrarErrorCampo(_errCorreo, m);
            if (errores.TryGetValue("Direccion", out m)) Estilo.MostrarErrorCampo(_errDireccion, m);
        }

        private void BtnGuardar_Click(object sender, EventArgs e)
        {
            LimpiarErrores();
            bool esNuevo = _idSeleccionado == 0;

            Dictionary<string, string> errores = _servicio.Validar(_txtNombre.Text, _txtRuc.Text,
                _txtTelefono.Text, _txtCorreo.Text, _txtDireccion.Text);
            if (errores.Count > 0)
            {
                MostrarErrores(errores);
                return;
            }

            try
            {
                if (esNuevo)
                {
                    _servicio.Registrar(_txtNombre.Text, _txtRuc.Text, _txtTelefono.Text, _txtCorreo.Text, _txtDireccion.Text);
                    RecargarLista();
                    Limpiar();
                    Estilo.MostrarExito(this, "Proveedor registrado.");
                }
                else
                {
                    _servicio.Actualizar(_idSeleccionado, _txtNombre.Text, _txtRuc.Text, _txtTelefono.Text, _txtCorreo.Text, _txtDireccion.Text);
                    RecargarLista();
                    Limpiar();
                    Estilo.MostrarExito(this, "Datos del proveedor actualizados.");
                }
            }
            catch (ValidacionException ex)
            {
                Dictionary<string, string> uno = new Dictionary<string, string>();
                uno[ex.Campo ?? string.Empty] = ex.Message;   // p. ej. "Ya existe un proveedor con ese RUC" bajo el campo RUC
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
                Estilo.MostrarError(this, "No se pudo guardar el proveedor. Verifique la conexión con la base de datos.");
            }
        }

        private void BtnEliminar_Click(object sender, EventArgs e)
        {
            if (_idSeleccionado == 0) return;

            string aviso = "¿Dar de baja a este proveedor?\nSolo dejará de aparecer para nuevos ingresos; los movimientos anteriores se conservan.";
            try
            {
                int usos = _servicio.ContarMovimientos(_idSeleccionado);
                if (usos > 0)
                    aviso = "Este proveedor figura en " + usos.ToString("N0") + " movimiento(s) de inventario.\n" + aviso;
            }
            catch (AccesoDenegadoException ex)
            {
                Estilo.MostrarError(this, ex.Message);
                return;
            }
            catch (Exception)
            {
                // Es solo informativo: si falla la consulta, se pregunta igual con el texto básico.
            }

            DialogResult r = MessageBox.Show(this, aviso, "SIGEPOSTA",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (r != DialogResult.Yes) return;

            try
            {
                _servicio.DarDeBaja(_idSeleccionado);
                RecargarLista();
                Limpiar();
                Estilo.MostrarExito(this, "Proveedor dado de baja.");
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
                Estilo.MostrarError(this, "No se pudo dar de baja al proveedor. Verifique la conexión con la base de datos.");
            }
        }

        private static void SoloDigitos(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true;
        }
    }
}
