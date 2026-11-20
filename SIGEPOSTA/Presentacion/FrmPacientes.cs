using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SIGEPOSTA.Negocio;

namespace SIGEPOSTA.Presentacion
{
    /// <summary>
    /// Registro y consulta de pacientes. Arriba el recuadro "Datos personales" (cuadrícula de 3 columnas
    /// y botones a la derecha); abajo "Lista de pacientes" con búsqueda y tabla.
    /// La pantalla solo recoge datos y muestra errores; las reglas y los permisos están en PacienteServicio.
    /// </summary>
    public class FrmPacientes : Form
    {
        private readonly PacienteServicio _servicio = new PacienteServicio();
        // Búsqueda mientras se escribe (espera de 300 ms), en segundo plano y paginada de a 50.
        private ControladorBusqueda<Paciente> _busqueda;
        private ResultadoPagina<Paciente> _resultado;
        private Button _btnAnterior, _btnSiguiente;
        private Label _lblPagina;
        private ToolStripStatusLabel _lblConsulta;
        private int _idSeleccionado; // 0 = paciente nuevo
        private bool _cargando;      // evita reaccionar a eventos mientras se llena la tabla

        private TextBox _txtDni, _txtNombres, _txtApellidos, _txtTelefono, _txtDireccion, _txtBuscar;
        private DateTimePicker _dtpNacimiento;
        private Label _errDni, _errNombres, _errApellidos, _errNacimiento, _errTelefono, _errDireccion;
        private Button _btnLimpiar, _btnEliminar, _btnGuardar, _btnHistoria;
        private DataGridView _grid;

        public FrmPacientes()
        {
            // Si el rol no corresponde, ni siquiera se construye la pantalla.
            Permisos.ExigirAccesoPacientes();
            Construir();
            Load += FrmPacientes_Load;
        }

        // ================== Construcción de la pantalla ==================

        private void Construir()
        {
            Text = "SIGEPOSTA - Pacientes";
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
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // datos personales
            raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // lista (ocupa el resto)
            raiz.RowCount = 3;

            raiz.Controls.Add(Estilo.CrearBarraTitulo("Pacientes e historia clínica"), 0, 0);
            GroupBox datos = ConstruirDatosPersonales();
            raiz.Controls.Add(datos, 0, 1);
            raiz.Controls.Add(ConstruirLista(), 0, 2);
            Controls.Add(raiz);

            // Barra de estado: "Consulta en X ms" (se agrega después del contenido para acoplarse abajo).
            Controls.Add(Estilo.CrearBarraEstado(out _lblConsulta));

            _btnLimpiar.Click += delegate { Limpiar(); };
            _btnEliminar.Click += BtnEliminar_Click;
            _btnGuardar.Click += BtnGuardar_Click;
            _btnHistoria.Click += delegate { AbrirHistoria(); };

            // Enter en los campos de datos = Guardar; en el cuadro de búsqueda no hace nada; Esc cierra.
            Estilo.VincularEnter(this, datos, _btnGuardar);
            Estilo.SinEnter(this, _txtBuscar);
            AcceptButton = _btnGuardar;
            Estilo.EscCierra(this);
            _busqueda = new ControladorBusqueda<Paciente>(
                delegate (string texto, int pagina, object filtro, Func<bool> cancelado) { return _servicio.BuscarPagina(texto, pagina, cancelado); },
                Mostrar, Fallar, CambiarEstado);
            _txtBuscar.TextChanged += delegate { _busqueda.TextoCambio(_txtBuscar.Text); };   // consulta tras 300 ms sin escribir
            _btnAnterior.Click += delegate { if (_resultado != null) _busqueda.IrAPagina(_resultado.Pagina - 1); };
            _btnSiguiente.Click += delegate { if (_resultado != null) _busqueda.IrAPagina(_resultado.Pagina + 1); };
            FormClosed += delegate { _busqueda.Dispose(); };
            _grid.SelectionChanged += Grid_SelectionChanged;
            _grid.CellDoubleClick += delegate (object s, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0) AbrirHistoria(); };
        }

        private GroupBox ConstruirDatosPersonales()
        {
            _txtDni = Estilo.CrearTextBox(false, 8);
            _txtNombres = Estilo.CrearTextBox(false, 80);
            _txtApellidos = Estilo.CrearTextBox(false, 80);
            _txtTelefono = Estilo.CrearTextBox(false, 9);
            _dtpNacimiento = Estilo.CrearSelectorFecha();
            _txtDireccion = Estilo.CrearTextBox(false, 150);   // opcional; ocupa el lugar que antes tenía la lista de postas
            _txtDni.KeyPress += SoloDigitos;
            _txtTelefono.KeyPress += SoloDigitos;

            // Cuadrícula de 3 columnas iguales, 2 filas.
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

            AgregarCampo(cuadricula, "DNI", _txtDni, out _errDni, 0, 0);
            AgregarCampo(cuadricula, "Nombres", _txtNombres, out _errNombres, 1, 0);
            AgregarCampo(cuadricula, "Apellidos", _txtApellidos, out _errApellidos, 2, 0);
            AgregarCampo(cuadricula, "Fecha de nacimiento", _dtpNacimiento, out _errNacimiento, 0, 1);
            AgregarCampo(cuadricula, "Teléfono", _txtTelefono, out _errTelefono, 1, 1);
            AgregarCampo(cuadricula, "Dirección (opcional)", _txtDireccion, out _errDireccion, 2, 1);

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

            GroupBox g = Estilo.CrearRecuadro("Datos personales");
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
            _btnHistoria = Estilo.CrearBotonSecundario("Ver historia clínica");
            _btnHistoria.Dock = DockStyle.None;
            _btnHistoria.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _btnHistoria.Width = 190;
            _btnHistoria.Margin = new Padding(Estilo.EspacioCampos, 0, 0, Estilo.EspacioCampos);

            TableLayoutPanel fila = new TableLayoutPanel();
            fila.ColumnCount = 2;
            fila.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            fila.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            fila.RowCount = 1;
            fila.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            fila.Dock = DockStyle.Fill;
            fila.AutoSize = true;
            fila.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            fila.Margin = Padding.Empty;
            fila.Controls.Add(Estilo.CrearCampo("Buscar por apellido, nombre o N.º de historia (escriba el inicio)", _txtBuscar), 0, 0);
            fila.Controls.Add(_btnHistoria, 1, 0);

            _grid = new DataGridView();
            Estilo.EstilizarGrid(_grid);
            _grid.Columns.Add("NumeroHistoria", "N.º historia");
            _grid.Columns.Add("Paciente", "Paciente");
            _grid.Columns.Add("Telefono", "Teléfono");
            _grid.Columns["NumeroHistoria"].FillWeight = 25;
            _grid.Columns["Paciente"].FillWeight = 50;
            _grid.Columns["Telefono"].FillWeight = 25;

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

            GroupBox g = Estilo.CrearRecuadro("Lista de pacientes");
            g.AutoSize = false; // la lista ocupa todo el espacio que sobra
            g.Margin = new Padding(Estilo.MargenInterior, 0, Estilo.MargenInterior, Estilo.MargenInterior);
            g.Controls.Add(contenido);
            return g;
        }

        // ================== Carga de datos ==================

        private void FrmPacientes_Load(object sender, EventArgs e)
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

        /// <summary>Muestra UNA página de pacientes (llega del servicio ya paginada: nunca se traen todas las filas).</summary>
        private void Mostrar(ResultadoPagina<Paciente> r)
        {
            _resultado = r;

            _cargando = true;
            try
            {
                _grid.Rows.Clear();
                foreach (Paciente p in r.Items)
                {
                    int i = _grid.Rows.Add(p.NumeroHistoria, p.NombreCompleto, p.Telefono);
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

        private Paciente PacienteSeleccionado()
        {
            if (_grid.SelectedRows.Count == 0) return null;
            return _grid.SelectedRows[0].Tag as Paciente;
        }

        private void Grid_SelectionChanged(object sender, EventArgs e)
        {
            if (_cargando) return;
            Paciente p = PacienteSeleccionado();
            if (p == null) return;

            LimpiarErrores();
            _idSeleccionado = p.IdPaciente;
            try
            {
                // El DNI se descifra solo ahora, porque el usuario eligió editar este paciente.
                _txtDni.Text = _servicio.DescifrarDni(p.IdPaciente);
            }
            catch (Exception)
            {
                _txtDni.Text = string.Empty;
            }
            _txtDni.Enabled = false; // el DNI es la identidad del paciente: no se edita
            _txtNombres.Text = p.Nombres;
            _txtApellidos.Text = p.Apellidos;
            _txtTelefono.Text = p.Telefono;
            _dtpNacimiento.Value = FechaValida(p.FechaNacimiento);
            _txtDireccion.Text = p.Direccion;
            _btnEliminar.Enabled = true;
            _btnHistoria.Enabled = true;
        }

        private DateTime FechaValida(DateTime f)
        {
            if (f > _dtpNacimiento.MaxDate) return _dtpNacimiento.MaxDate;
            if (f < _dtpNacimiento.MinDate) return _dtpNacimiento.MinDate;
            return f;
        }

        // ================== Acciones ==================

        private void Limpiar()
        {
            _idSeleccionado = 0;
            _txtDni.Enabled = true;
            _txtDni.Clear();
            _txtNombres.Clear();
            _txtApellidos.Clear();
            _txtTelefono.Clear();
            _dtpNacimiento.Value = DateTime.Today;
            _txtDireccion.Clear();
            _grid.ClearSelection();
            LimpiarErrores();
            _btnEliminar.Enabled = false;
            _btnHistoria.Enabled = false;
            _txtDni.Focus();
        }

        private void LimpiarErrores()
        {
            Estilo.MostrarErrorCampo(_errDni, null);
            Estilo.MostrarErrorCampo(_errNombres, null);
            Estilo.MostrarErrorCampo(_errApellidos, null);
            Estilo.MostrarErrorCampo(_errNacimiento, null);
            Estilo.MostrarErrorCampo(_errTelefono, null);
            Estilo.MostrarErrorCampo(_errDireccion, null);
        }

        private void MostrarErrores(Dictionary<string, string> errores)
        {
            string m;
            if (errores.TryGetValue("Dni", out m)) Estilo.MostrarErrorCampo(_errDni, m);
            if (errores.TryGetValue("Nombres", out m)) Estilo.MostrarErrorCampo(_errNombres, m);
            if (errores.TryGetValue("Apellidos", out m)) Estilo.MostrarErrorCampo(_errApellidos, m);
            if (errores.TryGetValue("FechaNacimiento", out m)) Estilo.MostrarErrorCampo(_errNacimiento, m);
            if (errores.TryGetValue("Telefono", out m)) Estilo.MostrarErrorCampo(_errTelefono, m);
            if (errores.TryGetValue("Direccion", out m)) Estilo.MostrarErrorCampo(_errDireccion, m);
        }

        private void BtnGuardar_Click(object sender, EventArgs e)
        {
            LimpiarErrores();
            bool esNuevo = _idSeleccionado == 0;

            Dictionary<string, string> errores = _servicio.Validar(_txtDni.Text, _txtNombres.Text, _txtApellidos.Text,
                _dtpNacimiento.Value, _txtTelefono.Text, _txtDireccion.Text, esNuevo);
            if (errores.Count > 0)
            {
                MostrarErrores(errores);
                return;
            }

            try
            {
                if (esNuevo)
                {
                    Paciente p = _servicio.Registrar(_txtDni.Text, _txtNombres.Text, _txtApellidos.Text,
                        _dtpNacimiento.Value, _txtTelefono.Text, _txtDireccion.Text);   // la posta la asigna el servicio (establecimiento)
                    RecargarLista();
                    Limpiar();
                    Estilo.MostrarExito(this, "Paciente registrado. Historia clínica: " + p.NumeroHistoria);
                }
                else
                {
                    _servicio.Actualizar(_idSeleccionado, _txtNombres.Text, _txtApellidos.Text,
                        _dtpNacimiento.Value, _txtTelefono.Text, _txtDireccion.Text);
                    RecargarLista();
                    Limpiar();
                    Estilo.MostrarExito(this, "Datos del paciente actualizados.");
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
                Estilo.MostrarError(this, "No se pudo guardar el paciente. Verifique la conexión con la base de datos.");
            }
        }

        private void BtnEliminar_Click(object sender, EventArgs e)
        {
            if (_idSeleccionado == 0) return;

            DialogResult r = MessageBox.Show(this,
                "¿Dar de baja a este paciente?\nSu historia clínica se conserva; solo dejará de aparecer en la lista.",
                "SIGEPOSTA", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (r != DialogResult.Yes) return;

            try
            {
                _servicio.DarDeBaja(_idSeleccionado);
                RecargarLista();
                Limpiar();
                Estilo.MostrarExito(this, "Paciente dado de baja.");
            }
            catch (AccesoDenegadoException ex)
            {
                Estilo.MostrarError(this, ex.Message);
            }
            catch (Exception)
            {
                Estilo.MostrarError(this, "No se pudo dar de baja al paciente. Verifique la conexión con la base de datos.");
            }
        }

        private void AbrirHistoria()
        {
            Paciente p = PacienteSeleccionado();
            if (p == null)
            {
                Estilo.MostrarInfo(this, "Seleccione un paciente de la lista.");
                return;
            }
            try
            {
                using (FrmHistoriaClinica f = new FrmHistoriaClinica(_servicio, p))
                {
                    f.ShowDialog(this);
                }
            }
            catch (AccesoDenegadoException ex)
            {
                Estilo.MostrarError(this, ex.Message);
            }
        }

        private static void SoloDigitos(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true;
        }
    }
}
