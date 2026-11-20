using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SIGEPOSTA.Negocio;

namespace SIGEPOSTA.Presentacion
{
    /// <summary>
    /// Inventario de medicamentos. De arriba abajo: "Datos del medicamento" (cuadrícula de 3 columnas y
    /// botones a la derecha), "Movimiento de stock" e "Inventario" (búsqueda, filtro de críticos y tabla).
    /// La pantalla solo recoge datos y muestra errores; las reglas y los permisos están en InventarioServicio.
    /// </summary>
    public class FrmInventario : Form
    {
        /// <summary>Filtros de la lista (inmutable: la búsqueda en segundo plano recibe una copia que no cambia).</summary>
        private class FiltroInventario
        {
            public readonly bool SoloCriticos;
            public readonly int IdForma;
            public FiltroInventario(bool soloCriticos, int idForma) { SoloCriticos = soloCriticos; IdForma = idForma; }
        }

        private readonly InventarioServicio _servicio = new InventarioServicio();
        // Búsqueda mientras se escribe (espera de 300 ms), en segundo plano y paginada de a 50.
        private ControladorBusqueda<Medicamento> _busqueda;
        private ResultadoPagina<Medicamento> _resultado;
        private Button _btnAnterior, _btnSiguiente;
        private Label _lblPagina;
        private ToolStripStatusLabel _lblConsulta;
        // Tras un movimiento: qué fila volver a seleccionar y qué aviso mostrar cuando llegue la lista actualizada.
        private int _idPorSeleccionar;
        private string _avisoTrasRecarga;
        private int _idSeleccionado; // 0 = medicamento nuevo
        private bool _cargando;      // evita reaccionar a eventos mientras se llena la tabla

        // Datos del medicamento
        private TextBox _txtNombre, _txtConcentracion, _txtMinimo, _txtInicial;
        private ComboBox _cboForma, _cboUnidad, _cboFiltroForma;
        private Label _errConcentracion, _errForma, _errUnidad;
        private DateTimePicker _dtpVence;
        private Label _errNombre, _errMinimo, _errVence, _errInicial;
        private Button _btnLimpiar, _btnEliminar, _btnGuardar;

        // Movimiento de stock
        private ComboBox _cboTipo;
        private TextBox _txtCantidad, _txtMotivo, _txtDocumento;
        private ComboBox _cboProveedor;
        private Label _errTipo, _errCantidad, _errMotivo, _errProveedor, _errDocumento;
        private Button _btnMovimiento;

        // Inventario
        private TextBox _txtBuscar;
        private CheckBox _chkCriticos;
        private Button _btnHistorial;
        private DataGridView _grid;

        public FrmInventario()
        {
            // Si el rol no corresponde, ni siquiera se construye la pantalla.
            Permisos.ExigirAccesoInventario();
            Construir();
            Load += FrmInventario_Load;
        }

        // ================== Construcción de la pantalla ==================

        private void Construir()
        {
            Text = "SIGEPOSTA - Inventario de medicamentos";
            Font = Estilo.Fuente;
            BackColor = Estilo.Fondo;
            ForeColor = Estilo.Texto;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1150, 960);
            MinimumSize = new Size(950, 910);

            TableLayoutPanel raiz = new TableLayoutPanel();
            raiz.Dock = DockStyle.Fill;
            raiz.ColumnCount = 1;
            raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            raiz.RowCount = 4;
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // barra de título
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // datos del medicamento
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // movimiento de stock
            raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // inventario (ocupa el resto)

            raiz.Controls.Add(Estilo.CrearBarraTitulo("Inventario de medicamentos"), 0, 0);
            GroupBox datos = ConstruirDatos();
            GroupBox movimiento = ConstruirMovimiento();
            raiz.Controls.Add(datos, 0, 1);
            raiz.Controls.Add(movimiento, 0, 2);
            raiz.Controls.Add(ConstruirInventario(), 0, 3);
            Controls.Add(raiz);

            // Barra de estado: "Consulta en X ms" (se agrega después del contenido para acoplarse abajo).
            Controls.Add(Estilo.CrearBarraEstado(out _lblConsulta));

            _btnLimpiar.Click += delegate { Limpiar(); };
            _btnEliminar.Click += BtnEliminar_Click;
            _btnGuardar.Click += BtnGuardar_Click;
            _btnMovimiento.Click += BtnMovimiento_Click;
            _btnHistorial.Click += delegate { AbrirHistorial(); };

            // Enter en "Datos" = Guardar; en "Movimiento" = Registrar movimiento; en la búsqueda no hace nada; Esc cierra.
            Estilo.VincularEnter(this, datos, _btnGuardar);
            Estilo.VincularEnter(this, movimiento, _btnMovimiento);
            Estilo.SinEnter(this, _txtBuscar);
            AcceptButton = _btnGuardar;
            Estilo.EscCierra(this);
            _busqueda = new ControladorBusqueda<Medicamento>(
                delegate (string texto, int pagina, object filtro, Func<bool> cancelado)
                {
                    FiltroInventario fi = (FiltroInventario)filtro;
                    return _servicio.BuscarPagina(texto, fi.SoloCriticos, fi.IdForma, pagina, cancelado);
                },
                Mostrar, Fallar, CambiarEstado);
            _busqueda.Filtro = new FiltroInventario(false, 0);
            _txtBuscar.TextChanged += delegate { _busqueda.TextoCambio(_txtBuscar.Text); };   // consulta tras 300 ms sin escribir
            _chkCriticos.CheckedChanged += delegate { CambiarFiltro(); };
            _cboFiltroForma.SelectedIndexChanged += delegate { CambiarFiltro(); };
            _btnAnterior.Click += delegate { if (_resultado != null) _busqueda.IrAPagina(_resultado.Pagina - 1); };
            _btnSiguiente.Click += delegate { if (_resultado != null) _busqueda.IrAPagina(_resultado.Pagina + 1); };
            FormClosed += delegate { _busqueda.Dispose(); };
            _grid.SelectionChanged += Grid_SelectionChanged;
            _grid.CellDoubleClick += delegate (object s, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0) AbrirHistorial(); };
        }

        private GroupBox ConstruirDatos()
        {
            _txtNombre = Estilo.CrearTextBox(false, 100);
            _txtConcentracion = Estilo.CrearTextBox(false, Medicamento.MaxConcentracion);
            _cboForma = Estilo.CrearCombo();
            _cboUnidad = Estilo.CrearCombo();
            _txtMinimo = Estilo.CrearTextBox(false, 7);
            _dtpVence = Estilo.CrearSelectorFechaVencimiento();
            _txtInicial = Estilo.CrearTextBox(false, 7);
            _txtMinimo.KeyPress += SoloDigitos;
            _txtInicial.KeyPress += SoloDigitos;
            Estilo.PonerAyuda(_txtConcentracion, "Ej.: 500 mg");

            // 4 columnas del mismo ancho y 2 filas, sin huecos (8 casillas, Nombre ocupa 2):
            // fila 1 = Nombre (2 columnas) | Concentración | Forma farmacéutica;
            // fila 2 = Unidad | Stock mínimo | Fecha de vencimiento | Stock inicial.
            TableLayoutPanel cuadricula = CrearCuadricula(4, 2);
            AgregarCampo(cuadricula, "Nombre", _txtNombre, out _errNombre, 0, 0, 4);
            cuadricula.SetColumnSpan(cuadricula.GetControlFromPosition(0, 0), 2);
            AgregarCampo(cuadricula, "Concentración (opcional)", _txtConcentracion, out _errConcentracion, 2, 0, 4);
            AgregarCampo(cuadricula, "Forma farmacéutica", _cboForma, out _errForma, 3, 0, 4);
            AgregarCampo(cuadricula, "Unidad", _cboUnidad, out _errUnidad, 0, 1, 4);
            AgregarCampo(cuadricula, "Stock mínimo", _txtMinimo, out _errMinimo, 1, 1, 4);
            AgregarCampo(cuadricula, "Fecha de vencimiento", _dtpVence, out _errVence, 2, 1, 4);
            AgregarCampo(cuadricula, "Stock inicial (al crear)", _txtInicial, out _errInicial, 3, 1, 4);
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

            GroupBox g = Estilo.CrearRecuadro("Datos del medicamento");
            g.Controls.Add(contenido);
            return g;
        }

        private GroupBox ConstruirMovimiento()
        {
            _cboTipo = Estilo.CrearCombo();
            _cboTipo.Items.Add("Ingreso");
            _cboTipo.Items.Add("Salida");
            _cboTipo.SelectedIndex = 0;
            _txtCantidad = Estilo.CrearTextBox(false, 7);
            _txtCantidad.KeyPress += SoloDigitos;
            _txtMotivo = Estilo.CrearTextBox(false, 200);
            _cboProveedor = Estilo.CrearCombo();
            _txtDocumento = Estilo.CrearTextBox(false, MovimientoInventario.MaxNumeroDocumento);
            _btnMovimiento = Estilo.CrearBotonPrimario("Registrar movimiento");
            _btnMovimiento.Dock = DockStyle.None;
            _btnMovimiento.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _btnMovimiento.Width = 200;
            _btnMovimiento.Margin = new Padding(Estilo.EspacioCampos, 0, 0, Estilo.EspacioCampos);

            // Cuadrícula de 3 columnas iguales + botón a la derecha (ancho fijo).
            // Fila 1: Tipo | Cantidad | Motivo. Fila 2: Proveedor | N.º de guía o factura | (libre) + botón.
            TableLayoutPanel fila = new TableLayoutPanel();
            fila.ColumnCount = 4;
            for (int i = 0; i < 3; i++) fila.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3f));
            fila.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            fila.RowCount = 2;
            fila.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            fila.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            fila.Dock = DockStyle.Fill;
            fila.AutoSize = true;
            fila.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            fila.Margin = Padding.Empty;

            Label e1, e2, e3, e4, e5;
            TableLayoutPanel c1 = Estilo.CrearCampoConError("Tipo", _cboTipo, out e1);
            TableLayoutPanel c2 = Estilo.CrearCampoConError("Cantidad", _txtCantidad, out e2);
            TableLayoutPanel c3 = Estilo.CrearCampoConError("Motivo (opcional)", _txtMotivo, out e3);
            TableLayoutPanel c4 = Estilo.CrearCampoConError("Proveedor (solo ingresos)", _cboProveedor, out e4);
            TableLayoutPanel c5 = Estilo.CrearCampoConError("N.º de guía o factura (solo ingresos)", _txtDocumento, out e5);
            _errTipo = e1; _errCantidad = e2; _errMotivo = e3; _errProveedor = e4; _errDocumento = e5;
            c1.Margin = new Padding(0, 0, Estilo.EspacioCampos, Estilo.EspacioCampos);
            c2.Margin = new Padding(0, 0, Estilo.EspacioCampos, Estilo.EspacioCampos);
            c3.Margin = new Padding(0, 0, 0, Estilo.EspacioCampos);
            c4.Margin = new Padding(0, 0, Estilo.EspacioCampos, Estilo.EspacioCampos);
            c5.Margin = new Padding(0, 0, Estilo.EspacioCampos, Estilo.EspacioCampos);
            fila.Controls.Add(c1, 0, 0);
            fila.Controls.Add(c2, 1, 0);
            fila.Controls.Add(c3, 2, 0);
            fila.Controls.Add(c4, 0, 1);
            fila.Controls.Add(c5, 1, 1);
            fila.Controls.Add(_btnMovimiento, 3, 1);

            _cboTipo.SelectedIndexChanged += delegate { ActualizarCamposIngreso(); };
            _cboProveedor.Enter += delegate { CargarProveedores(); };   // al ir a elegirlo se vuelve a leer la lista (pudo cambiar en Proveedores)
            ActualizarCamposIngreso();

            GroupBox g = Estilo.CrearRecuadro("Movimiento de stock");
            g.Margin = new Padding(Estilo.MargenInterior, 0, Estilo.MargenInterior, Estilo.MargenInterior);
            g.Controls.Add(fila);
            return g;
        }

        /// <summary>Proveedor y documento solo se piden (y solo se pueden usar) en los ingresos.</summary>
        private void ActualizarCamposIngreso()
        {
            bool esIngreso = _cboTipo.SelectedIndex == 0;
            _cboProveedor.Enabled = esIngreso;
            _txtDocumento.Enabled = esIngreso;
            if (!esIngreso)
            {
                _cboProveedor.SelectedIndex = -1;
                _txtDocumento.Clear();
                Estilo.MostrarErrorCampo(_errProveedor, null);
                Estilo.MostrarErrorCampo(_errDocumento, null);
            }
        }

        /// <summary>Lee los proveedores activos y conserva el que estaba elegido (si sigue activo).</summary>
        private void CargarProveedores()
        {
            try
            {
                int idElegido = IdProveedorElegido();
                List<Proveedor> lista = _servicio.ListarProveedoresActivos();
                _cboProveedor.BeginUpdate();
                try
                {
                    _cboProveedor.Items.Clear();
                    foreach (Proveedor p in lista) _cboProveedor.Items.Add(p);
                    for (int i = 0; i < _cboProveedor.Items.Count; i++)
                    {
                        if (((Proveedor)_cboProveedor.Items[i]).IdProveedor == idElegido) { _cboProveedor.SelectedIndex = i; break; }
                    }
                }
                finally
                {
                    _cboProveedor.EndUpdate();
                }
            }
            catch (AccesoDenegadoException ex)
            {
                Estilo.MostrarError(this, ex.Message);
            }
            catch (Exception)
            {
                Estilo.MostrarErrorCampo(_errProveedor, "No se pudo cargar la lista de proveedores.");
            }
        }

        private int IdProveedorElegido()
        {
            Proveedor p = _cboProveedor.SelectedItem as Proveedor;
            return p == null ? 0 : p.IdProveedor;
        }

        private GroupBox ConstruirInventario()
        {
            _txtBuscar = Estilo.CrearTextBox();
            _cboFiltroForma = Estilo.CrearCombo();
            _chkCriticos = Estilo.CrearCasilla("Solo críticos");
            _chkCriticos.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            _chkCriticos.Margin = new Padding(Estilo.MargenInterior, 0, Estilo.MargenInterior, Estilo.EspacioCampos + 4);
            _btnHistorial = Estilo.CrearBotonSecundario("Ver movimientos");
            _btnHistorial.Dock = DockStyle.None;
            _btnHistorial.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _btnHistorial.Width = 190;
            _btnHistorial.Margin = new Padding(0, 0, 0, Estilo.EspacioCampos);

            TableLayoutPanel fila = new TableLayoutPanel();
            fila.ColumnCount = 4;
            fila.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70f));
            fila.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30f));
            fila.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            fila.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            fila.RowCount = 1;
            fila.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            fila.Dock = DockStyle.Fill;
            fila.AutoSize = true;
            fila.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            fila.Margin = Padding.Empty;
            TableLayoutPanel cBuscar = Estilo.CrearCampo("Buscar medicamento (escriba el inicio del nombre)", _txtBuscar);
            TableLayoutPanel cForma = Estilo.CrearCampo("Forma farmacéutica", _cboFiltroForma);
            cBuscar.Margin = new Padding(0, 0, Estilo.EspacioCampos, Estilo.EspacioCampos);
            cForma.Margin = new Padding(0, 0, 0, Estilo.EspacioCampos);
            fila.Controls.Add(cBuscar, 0, 0);
            fila.Controls.Add(cForma, 1, 0);
            fila.Controls.Add(_chkCriticos, 2, 0);
            fila.Controls.Add(_btnHistorial, 3, 0);

            _grid = new DataGridView();
            Estilo.EstilizarGrid(_grid);
            _grid.Columns.Add("Medicamento", "Medicamento");
            _grid.Columns.Add("Stock", "Stock");
            _grid.Columns.Add("Minimo", "Mínimo");
            _grid.Columns.Add("Vence", "Vence");
            _grid.Columns.Add("Alerta", "Alerta");
            _grid.Columns["Medicamento"].FillWeight = 45;
            _grid.Columns["Stock"].FillWeight = 8;
            _grid.Columns["Minimo"].FillWeight = 8;
            _grid.Columns["Vence"].FillWeight = 13;
            _grid.Columns["Alerta"].FillWeight = 24;
            _grid.Columns["Stock"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.Columns["Minimo"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            TableLayoutPanel paginador = Estilo.CrearPaginador(out _btnAnterior, out _btnSiguiente, out _lblPagina);

            TableLayoutPanel contenido = Estilo.CrearColumna();
            contenido.AutoSize = false;
            contenido.RowCount = 3;
            contenido.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            contenido.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            contenido.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            contenido.Controls.Add(fila, 0, 0);
            contenido.Controls.Add(_grid, 0, 1);
            contenido.Controls.Add(paginador, 0, 2);

            GroupBox g = Estilo.CrearRecuadro("Inventario");
            g.AutoSize = false; // el inventario ocupa todo el espacio que sobra
            g.Margin = new Padding(Estilo.MargenInterior, 0, Estilo.MargenInterior, Estilo.MargenInterior);
            g.Controls.Add(contenido);
            return g;
        }

        private static TableLayoutPanel CrearCuadricula(int columnas, int filas)
        {
            TableLayoutPanel t = new TableLayoutPanel();
            t.ColumnCount = columnas;
            for (int i = 0; i < columnas; i++) t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columnas));
            t.RowCount = filas;
            for (int i = 0; i < filas; i++) t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Dock = DockStyle.Fill;
            t.AutoSize = true;
            t.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            t.Margin = Padding.Empty;
            return t;
        }

        /// <summary>Agrega un campo (con su error debajo) a la cuadrícula; separa 12 px de la columna vecina.</summary>
        private static void AgregarCampo(TableLayoutPanel cuadricula, string etiqueta, Control control, out Label error,
                                         int col, int fila, int totalColumnas)
        {
            TableLayoutPanel campo = Estilo.CrearCampoConError(etiqueta, control, out error);
            campo.Margin = new Padding(0, 0, col < totalColumnas - 1 ? Estilo.EspacioCampos : 0, Estilo.EspacioCampos);
            cuadricula.Controls.Add(campo, col, fila);
        }

        // ================== Carga de datos ==================

        private void FrmInventario_Load(object sender, EventArgs e)
        {
            try
            {
                CargarFormasYUnidades();
                RecargarLista();
                Limpiar();
                CargarProveedores();
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

        /// <summary>Vuelve a consultar la página actual (tras guardar, dar de baja, un movimiento, etc.).</summary>
        private void RecargarLista()
        {
            if (_resultado == null) _busqueda.BuscarYa(); else _busqueda.Recargar();
        }

        /// <summary>Muestra UNA página de medicamentos (llega del servicio ya paginada) y pinta las filas según su alerta.</summary>
        private void Mostrar(ResultadoPagina<Medicamento> r)
        {
            _resultado = r;

            _cargando = true;
            try
            {
                _grid.Rows.Clear();
                foreach (Medicamento m in r.Items)
                {
                    string vence = m.FechaVencimiento.HasValue ? m.FechaVencimiento.Value.ToString("dd/MM/yyyy") : "—";
                    int i = _grid.Rows.Add(m.NombreCompleto,   // "Paracetamol 500 mg - Tableta"
                        m.StockTexto, m.StockMinimo, vence, InventarioServicio.TextoAlerta(m));   // "10 tabletas" / "1 tableta"
                    _grid.Rows[i].Tag = m;
                    PintarFila(_grid.Rows[i], m);
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

            TerminarMovimientoPendiente();
        }

        /// <summary>
        /// Tras registrar un movimiento, cuando llega la lista actualizada se vuelve a seleccionar el medicamento
        /// y se muestra el aviso (con la advertencia de stock bajo, ya con el stock nuevo).
        /// </summary>
        private void TerminarMovimientoPendiente()
        {
            if (_avisoTrasRecarga == null) return;
            string aviso = _avisoTrasRecarga;
            int id = _idPorSeleccionar;
            _avisoTrasRecarga = null;
            _idPorSeleccionar = 0;

            SeleccionarFila(id);
            Medicamento m = _resultado.Items.FirstOrDefault(x => x.IdMedicamento == id);
            if (m != null && m.EstaBajoStock)
                aviso += "\nAtención: el stock quedó en " + m.StockTexto + " (mínimo " + m.StockMinimo + ").";
            Estilo.MostrarExito(this, aviso);
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

        /// <summary>Rojo para VENCIDO, ámbar para STOCK BAJO o POR VENCER; sin alerta queda con el estilo normal.</summary>
        private static void PintarFila(DataGridViewRow fila, Medicamento m)
        {
            Estilo.PintarFilaAlerta(fila, m); // la misma regla de colores se comparte con el panel de inicio
        }

        private Medicamento MedicamentoSeleccionado()
        {
            if (_grid.SelectedRows.Count == 0) return null;
            return _grid.SelectedRows[0].Tag as Medicamento;
        }

        private void Grid_SelectionChanged(object sender, EventArgs e)
        {
            if (_cargando) return;
            Medicamento m = MedicamentoSeleccionado();
            if (m == null) return;

            LimpiarErrores();
            _idSeleccionado = m.IdMedicamento;
            _txtNombre.Text = m.Nombre;
            _txtConcentracion.Text = m.Concentracion;
            Elegir(_cboForma, m.IdForma);     // un medicamento antiguo en "Sin definir" queda sin elegir: hay que escoger forma y unidad para guardarlo
            Elegir(_cboUnidad, m.IdUnidad);
            _txtMinimo.Text = m.StockMinimo.ToString();
            if (m.FechaVencimiento.HasValue)
            {
                _dtpVence.Value = m.FechaVencimiento.Value;
                _dtpVence.Checked = true;
            }
            else
            {
                _dtpVence.Checked = false;
            }
            // El stock solo cambia con movimientos: el stock inicial solo se usa al crear.
            _txtInicial.Clear();
            _txtInicial.Enabled = false;
            _btnEliminar.Enabled = true;
            _btnMovimiento.Enabled = true;
            _btnHistorial.Enabled = true;
        }

        private void SeleccionarFila(int idMedicamento)
        {
            foreach (DataGridViewRow fila in _grid.Rows)
            {
                Medicamento m = fila.Tag as Medicamento;
                if (m != null && m.IdMedicamento == idMedicamento)
                {
                    fila.Selected = true;
                    _grid.FirstDisplayedScrollingRowIndex = fila.Index;
                    return;
                }
            }
        }

        // ================== Acciones ==================

        private void Limpiar()
        {
            _idSeleccionado = 0;
            _txtNombre.Clear();
            _txtConcentracion.Clear();
            _cboForma.SelectedIndex = -1;
            _cboUnidad.SelectedIndex = -1;
            _txtMinimo.Clear();
            _dtpVence.Value = DateTime.Today;
            _dtpVence.Checked = false;
            _txtInicial.Clear();
            _txtInicial.Enabled = true;
            _txtCantidad.Clear();
            _txtMotivo.Clear();
            _txtDocumento.Clear();
            _cboProveedor.SelectedIndex = -1;
            _cboTipo.SelectedIndex = 0;
            ActualizarCamposIngreso();
            _grid.ClearSelection();
            LimpiarErrores();
            _btnEliminar.Enabled = false;
            _btnMovimiento.Enabled = false;
            _btnHistorial.Enabled = false;
            _txtNombre.Focus();
        }

        private void LimpiarErrores()
        {
            foreach (Label l in new[] { _errNombre, _errConcentracion, _errForma, _errUnidad, _errMinimo, _errVence, _errInicial,
                                        _errTipo, _errCantidad, _errMotivo, _errProveedor, _errDocumento })
            {
                Estilo.MostrarErrorCampo(l, null);
            }
        }

        private void MostrarErrores(Dictionary<string, string> errores)
        {
            string m;
            if (errores.TryGetValue("Nombre", out m)) Estilo.MostrarErrorCampo(_errNombre, m);
            if (errores.TryGetValue("Concentracion", out m)) Estilo.MostrarErrorCampo(_errConcentracion, m);
            if (errores.TryGetValue("Forma", out m)) Estilo.MostrarErrorCampo(_errForma, m);
            if (errores.TryGetValue("Unidad", out m)) Estilo.MostrarErrorCampo(_errUnidad, m);
            if (errores.TryGetValue("StockMinimo", out m)) Estilo.MostrarErrorCampo(_errMinimo, m);
            if (errores.TryGetValue("FechaVencimiento", out m)) Estilo.MostrarErrorCampo(_errVence, m);
            if (errores.TryGetValue("StockInicial", out m)) Estilo.MostrarErrorCampo(_errInicial, m);
            if (errores.TryGetValue("Tipo", out m)) Estilo.MostrarErrorCampo(_errTipo, m);
            if (errores.TryGetValue("Cantidad", out m)) Estilo.MostrarErrorCampo(_errCantidad, m);
            if (errores.TryGetValue("Motivo", out m)) Estilo.MostrarErrorCampo(_errMotivo, m);
            if (errores.TryGetValue("Proveedor", out m)) Estilo.MostrarErrorCampo(_errProveedor, m);
            if (errores.TryGetValue("NumeroDocumento", out m)) Estilo.MostrarErrorCampo(_errDocumento, m);
        }

        private DateTime? FechaElegida()
        {
            return _dtpVence.Checked ? (DateTime?)_dtpVence.Value.Date : null;
        }

        /// <summary>Convierte el texto a entero; si está vacío o no es número, anota el error y devuelve false.</summary>
        private static bool LeerEntero(TextBox caja, string campo, Dictionary<string, string> errores, out int valor)
        {
            if (int.TryParse(caja.Text.Trim(), out valor)) return true;
            errores[campo] = "Ingrese un número entero.";
            return false;
        }

        private void BtnGuardar_Click(object sender, EventArgs e)
        {
            LimpiarErrores();
            bool esNuevo = _idSeleccionado == 0;
            Dictionary<string, string> errores = new Dictionary<string, string>();

            int minimo, inicial = 0;
            bool minimoOk = LeerEntero(_txtMinimo, "StockMinimo", errores, out minimo);
            bool inicialOk = !esNuevo || LeerEntero(_txtInicial, "StockInicial", errores, out inicial);

            Dictionary<string, string> delServicio = _servicio.Validar(_txtNombre.Text, _txtConcentracion.Text, IdElegido(_cboForma), IdElegido(_cboUnidad),
                minimoOk ? minimo : 0, FechaElegida(), inicialOk ? inicial : 0, esNuevo && inicialOk);
            foreach (KeyValuePair<string, string> par in delServicio)
            {
                if (!errores.ContainsKey(par.Key)) errores[par.Key] = par.Value;
            }
            if (errores.Count > 0)
            {
                MostrarErrores(errores);
                return;
            }

            try
            {
                if (esNuevo)
                {
                    _servicio.Registrar(_txtNombre.Text, _txtConcentracion.Text, IdElegido(_cboForma), IdElegido(_cboUnidad), minimo, FechaElegida(), inicial);   // la posta la asigna el servicio
                    RecargarLista();
                    Limpiar();
                    Estilo.MostrarExito(this, "Medicamento registrado.");
                }
                else
                {
                    _servicio.Actualizar(_idSeleccionado, _txtNombre.Text, _txtConcentracion.Text, IdElegido(_cboForma), IdElegido(_cboUnidad), minimo, FechaElegida());
                    RecargarLista();
                    Limpiar();
                    Estilo.MostrarExito(this, "Datos del medicamento actualizados.");
                }
            }
            catch (ValidacionException ex)
            {
                Dictionary<string, string> uno = new Dictionary<string, string>();
                uno[ex.Campo ?? string.Empty] = ex.Message;
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
                Estilo.MostrarError(this, "No se pudo guardar el medicamento. Verifique la conexión con la base de datos.");
            }
        }

        private void BtnMovimiento_Click(object sender, EventArgs e)
        {
            Estilo.MostrarErrorCampo(_errTipo, null);
            Estilo.MostrarErrorCampo(_errCantidad, null);
            Estilo.MostrarErrorCampo(_errMotivo, null);
            Estilo.MostrarErrorCampo(_errProveedor, null);
            Estilo.MostrarErrorCampo(_errDocumento, null);

            if (_idSeleccionado == 0)
            {
                Estilo.MostrarErrorCampo(_errTipo, "Seleccione un medicamento de la lista.");
                return;
            }

            Dictionary<string, string> errores = new Dictionary<string, string>();
            int cantidad;
            if (!LeerEntero(_txtCantidad, "Cantidad", errores, out cantidad))
            {
                MostrarErrores(errores);
                return;
            }
            bool esIngreso = _cboTipo.SelectedIndex == 0;
            errores = esIngreso
                ? _servicio.ValidarIngreso(cantidad, _txtMotivo.Text, IdProveedorElegido(), _txtDocumento.Text)
                : _servicio.ValidarMovimiento(cantidad, _txtMotivo.Text);
            if (errores.Count > 0)
            {
                MostrarErrores(errores);
                return;
            }

            int id = _idSeleccionado;
            try
            {
                if (esIngreso) _servicio.RegistrarIngreso(id, cantidad, _txtMotivo.Text, IdProveedorElegido(), _txtDocumento.Text);
                else _servicio.RegistrarSalida(id, cantidad, _txtMotivo.Text);

                _txtCantidad.Clear();
                _txtMotivo.Clear();
                _txtDocumento.Clear();
                // La lista se actualiza en segundo plano: el aviso se muestra cuando llegue (ver TerminarMovimientoPendiente).
                _idPorSeleccionar = id;
                _avisoTrasRecarga = esIngreso ? "Ingreso registrado." : "Salida registrada.";
                RecargarLista();
            }
            catch (ValidacionException ex)
            {
                Dictionary<string, string> uno = new Dictionary<string, string>();
                uno[ex.Campo ?? string.Empty] = ex.Message; // p. ej. "Stock insuficiente..." bajo Cantidad
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
                Estilo.MostrarError(this, "No se pudo registrar el movimiento. Verifique la conexión con la base de datos.");
            }
        }

        private void BtnEliminar_Click(object sender, EventArgs e)
        {
            if (_idSeleccionado == 0) return;

            DialogResult r = MessageBox.Show(this,
                "¿Dar de baja este medicamento?\nSu historial de movimientos se conserva; solo dejará de aparecer en el inventario.",
                "SIGEPOSTA", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (r != DialogResult.Yes) return;

            try
            {
                _servicio.DarDeBaja(_idSeleccionado);
                RecargarLista();
                Limpiar();
                Estilo.MostrarExito(this, "Medicamento dado de baja.");
            }
            catch (AccesoDenegadoException ex)
            {
                Estilo.MostrarError(this, ex.Message);
            }
            catch (Exception)
            {
                Estilo.MostrarError(this, "No se pudo dar de baja el medicamento. Verifique la conexión con la base de datos.");
            }
        }

        private void AbrirHistorial()
        {
            Medicamento m = MedicamentoSeleccionado();
            if (m == null)
            {
                Estilo.MostrarInfo(this, "Seleccione un medicamento de la lista.");
                return;
            }
            try
            {
                using (FrmHistorialMovimientos f = new FrmHistorialMovimientos(_servicio, m))
                {
                    f.ShowDialog(this);
                }
            }
            catch (AccesoDenegadoException ex)
            {
                Estilo.MostrarError(this, ex.Message);
            }
        }

        // ================== Forma farmacéutica y unidad ==================

        /// <summary>
        /// Llena las listas con los elementos ACTIVOS. En el medicamento no se ofrece "Sin definir" (forma y unidad son
        /// obligatorias); el filtro de la lista sí lo incluye para encontrar los medicamentos antiguos.
        /// </summary>
        private void CargarFormasYUnidades()
        {
            try
            {
                List<ItemCatalogo> formas = _servicio.ListarFormasActivas();
                List<ItemCatalogo> unidades = _servicio.ListarUnidadesActivas();
                _cboForma.Items.Clear();
                _cboUnidad.Items.Clear();
                foreach (ItemCatalogo i in formas.Where(x => !x.EsSinDefinir)) _cboForma.Items.Add(i);
                foreach (ItemCatalogo i in unidades.Where(x => !x.EsSinDefinir)) _cboUnidad.Items.Add(i);

                _cboFiltroForma.Items.Clear();
                _cboFiltroForma.Items.Add(ItemCatalogo.Cargar(0, "(Todas las formas)", true, 0));
                foreach (ItemCatalogo i in formas) _cboFiltroForma.Items.Add(i);
                _cboFiltroForma.SelectedIndex = 0;
            }
            catch (AccesoDenegadoException ex)
            {
                Estilo.MostrarError(this, ex.Message);
            }
            catch (Exception)
            {
                Estilo.MostrarErrorCampo(_errForma, "No se pudieron cargar las formas y unidades.");
            }
        }

        private static int IdElegido(ComboBox c)
        {
            ItemCatalogo i = c.SelectedItem as ItemCatalogo;
            return i == null ? 0 : i.Id;
        }

        /// <summary>Selecciona el elemento con ese Id; si no está en la lista (antiguo "Sin definir" o dado de baja), la deja sin elegir.</summary>
        private static void Elegir(ComboBox c, int? id)
        {
            c.SelectedIndex = -1;
            if (!id.HasValue) return;
            for (int i = 0; i < c.Items.Count; i++)
            {
                if (((ItemCatalogo)c.Items[i]).Id == id.Value) { c.SelectedIndex = i; return; }
            }
        }

        private void CambiarFiltro()
        {
            _busqueda.Filtro = new FiltroInventario(_chkCriticos.Checked, IdElegido(_cboFiltroForma));
            _busqueda.BuscarYa();
        }

        private static void SoloDigitos(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true;
        }
    }
}
