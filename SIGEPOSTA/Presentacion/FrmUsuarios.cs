using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using SIGEPOSTA.Negocio;

namespace SIGEPOSTA.Presentacion
{
    /// <summary>
    /// Gestión de usuarios (solo Administrador): crear, cambiar rol, activar/desactivar y restablecer contraseña.
    /// Sin usuario elegido en la tabla, "Guardar" CREA uno nuevo. Con un usuario elegido, "Guardar" aplica
    /// el cambio de rol y/o la nueva contraseña (si se escribió una).
    /// Las reglas (política de contraseña, último administrador, etc.) están en UsuarioServicio.
    /// </summary>
    public class FrmUsuarios : Form
    {
        // Orden de la lista desplegable de roles = valor del enum menos 1.
        private static readonly string[] NombresRol = { "Administrador", "Médico", "Farmacia" };

        private readonly UsuarioServicio _servicio = new UsuarioServicio();
        private List<Usuario> _usuarios = new List<Usuario>();
        private Usuario _seleccionado; // null = usuario nuevo
        private bool _cargando;

        private TextBox _txtUsuario, _txtNombre, _txtClave, _txtRepetir;
        private ComboBox _cboRol;
        private Label _errUsuario, _errNombre, _errRol, _errClave, _errRepetir;
        private Button _btnLimpiar, _btnEstado, _btnGuardar;
        private ToolStripStatusLabel _lblConsulta;
        private DataGridView _grid;

        public FrmUsuarios()
        {
            // Si no es Administrador, ni siquiera se construye la pantalla.
            Permisos.ExigirAdministrador();
            Construir();
            Load += FrmUsuarios_Load;
        }

        // ================== Construcción de la pantalla ==================

        private void Construir()
        {
            Text = "SIGEPOSTA - Usuarios";
            Font = Estilo.Fuente;
            BackColor = Estilo.Fondo;
            ForeColor = Estilo.Texto;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1100, 700);
            MinimumSize = new Size(900, 580);

            TableLayoutPanel raiz = new TableLayoutPanel();
            raiz.Dock = DockStyle.Fill;
            raiz.ColumnCount = 1;
            raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            raiz.RowCount = 3;
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // barra de título
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // datos del usuario
            raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // tabla

            raiz.Controls.Add(Estilo.CrearBarraTitulo("Gestión de usuarios"), 0, 0);
            GroupBox datos = ConstruirDatos();
            raiz.Controls.Add(datos, 0, 1);
            raiz.Controls.Add(ConstruirTabla(), 0, 2);
            Controls.Add(raiz);

            // Barra de estado: "Consulta en X ms" (se agrega después del contenido para acoplarse abajo).
            Controls.Add(Estilo.CrearBarraEstado(out _lblConsulta));

            _btnLimpiar.Click += delegate { Limpiar(); };
            _btnEstado.Click += BtnEstado_Click;
            _btnGuardar.Click += BtnGuardar_Click;
            _grid.SelectionChanged += Grid_SelectionChanged;

            Estilo.VincularEnter(this, datos, _btnGuardar); // Enter en los campos = Guardar
            AcceptButton = _btnGuardar;
            Estilo.EscCierra(this);
        }

        private GroupBox ConstruirDatos()
        {
            _txtUsuario = Estilo.CrearTextBox(false, 50);
            _txtNombre = Estilo.CrearTextBox(false, 120);
            _cboRol = Estilo.CrearCombo();
            foreach (string r in NombresRol) _cboRol.Items.Add(r);
            _cboRol.SelectedIndex = -1;
            _txtClave = Estilo.CrearTextBox(true, 100);
            _txtRepetir = Estilo.CrearTextBox(true, 100);

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

            AgregarCampo(cuadricula, "Usuario", _txtUsuario, out _errUsuario, 0, 0);
            AgregarCampo(cuadricula, "Nombre completo", _txtNombre, out _errNombre, 1, 0);
            AgregarCampo(cuadricula, "Rol", _cboRol, out _errRol, 2, 0);
            AgregarCampo(cuadricula, "Contraseña", _txtClave, out _errClave, 0, 1);
            AgregarCampo(cuadricula, "Repetir contraseña", _txtRepetir, out _errRepetir, 1, 1);

            // Tercera celda de la segunda fila: ayuda para el modo "editar".
            Label ayuda = Estilo.CrearTexto(
                "Mínimo 8 caracteres, con mayúscula, minúscula y número. Al elegir un usuario de la tabla, " +
                "puede cambiar su rol o escribir una contraseña nueva para restablecerla.", 300);
            ayuda.Font = Estilo.FuentePequena;
            ayuda.Margin = new Padding(0, 24, 0, Estilo.EspacioCampos);
            cuadricula.Controls.Add(ayuda, 2, 1);

            // Botones a la derecha, de arriba hacia abajo: Limpiar, Desactivar/Activar, Guardar.
            _btnLimpiar = Estilo.CrearBotonSecundario("Limpiar");
            _btnEstado = Estilo.CrearBotonPeligro("Desactivar");
            _btnGuardar = Estilo.CrearBotonPrimario("Guardar");
            TableLayoutPanel botones = new TableLayoutPanel();
            botones.ColumnCount = 1;
            botones.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            botones.Dock = DockStyle.Top;
            botones.AutoSize = true;
            botones.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            botones.Margin = new Padding(Estilo.MargenInterior, 0, 0, 0);
            _btnLimpiar.Margin = new Padding(0, 0, 0, Estilo.EspacioCampos);
            _btnEstado.Margin = new Padding(0, 0, 0, Estilo.EspacioCampos);
            Estilo.AgregarFila(botones, _btnLimpiar);
            Estilo.AgregarFila(botones, _btnEstado);
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

            GroupBox g = Estilo.CrearRecuadro("Datos del usuario");
            g.Controls.Add(contenido);
            return g;
        }

        private static void AgregarCampo(TableLayoutPanel cuadricula, string etiqueta, Control control, out Label error, int col, int fila)
        {
            TableLayoutPanel campo = Estilo.CrearCampoConError(etiqueta, control, out error);
            campo.Margin = new Padding(0, 0, col < 2 ? Estilo.EspacioCampos : 0, Estilo.EspacioCampos);
            cuadricula.Controls.Add(campo, col, fila);
        }

        private GroupBox ConstruirTabla()
        {
            _grid = new DataGridView();
            Estilo.EstilizarGrid(_grid);
            _grid.Columns.Add("Usuario", "Usuario");
            _grid.Columns.Add("Nombre", "Nombre completo");
            _grid.Columns.Add("Rol", "Rol");
            _grid.Columns.Add("Estado", "Estado");
            _grid.Columns["Usuario"].FillWeight = 22;
            _grid.Columns["Nombre"].FillWeight = 38;
            _grid.Columns["Rol"].FillWeight = 20;
            _grid.Columns["Estado"].FillWeight = 20;

            GroupBox g = Estilo.CrearRecuadro("Usuarios");
            g.AutoSize = false; // la tabla ocupa todo el espacio que sobra
            g.Margin = new Padding(Estilo.MargenInterior, 0, Estilo.MargenInterior, Estilo.MargenInterior);
            g.Controls.Add(_grid);
            return g;
        }

        // ================== Carga de datos ==================

        private void FrmUsuarios_Load(object sender, EventArgs e)
        {
            try
            {
                Recargar();
                Limpiar();
            }
            catch (AccesoDenegadoException ex)
            {
                Estilo.MostrarError(this, ex.Message);
                Close();
            }
            catch (Exception)
            {
                Estilo.MostrarError(this, "No se pudo cargar la lista de usuarios. Verifique la conexión con la base de datos.");
            }
        }

        private void Recargar()
        {
            Stopwatch reloj = Stopwatch.StartNew();
            _usuarios = _servicio.Listar();
            reloj.Stop();
            Estilo.MostrarConsulta(_lblConsulta, reloj.ElapsedMilliseconds);

            _cargando = true;
            try
            {
                _grid.Rows.Clear();
                foreach (Usuario u in _usuarios)
                {
                    string estado = u.Activo ? (u.EstaBloqueado ? "Activo (bloqueado)" : "Activo") : "Inactivo";
                    int i = _grid.Rows.Add(u.NombreUsuario, u.NombreCompleto, NombreDeRol(u.Rol), estado);
                    _grid.Rows[i].Tag = u;
                    if (!u.Activo) _grid.Rows[i].DefaultCellStyle.ForeColor = Estilo.Gris; // inactivos en gris
                }
                _grid.ClearSelection();
            }
            finally
            {
                _cargando = false;
            }
        }

        private static string NombreDeRol(Rol rol)
        {
            int i = (int)rol - 1;
            return i >= 0 && i < NombresRol.Length ? NombresRol[i] : rol.ToString();
        }

        private void Grid_SelectionChanged(object sender, EventArgs e)
        {
            if (_cargando) return;
            if (_grid.SelectedRows.Count == 0) return;
            Usuario u = _grid.SelectedRows[0].Tag as Usuario;
            if (u == null) return;

            LimpiarErrores();
            _seleccionado = u;
            _txtUsuario.Text = u.NombreUsuario;
            _txtNombre.Text = u.NombreCompleto;
            _txtUsuario.Enabled = false;   // el nombre de usuario es la identidad: no se cambia
            _txtNombre.Enabled = false;
            _cboRol.SelectedIndex = (int)u.Rol - 1;
            _txtClave.Clear();
            _txtRepetir.Clear();
            ConfigurarBotonEstado(u);
        }

        /// <summary>El botón dice "Desactivar" o "Activar" según el usuario elegido (rojo / verde).</summary>
        private void ConfigurarBotonEstado(Usuario u)
        {
            _btnEstado.Enabled = u != null;
            if (u == null)
            {
                _btnEstado.Text = "Desactivar";
                PintarBoton(_btnEstado, Estilo.Error);
                return;
            }
            _btnEstado.Text = u.Activo ? "Desactivar" : "Activar";
            PintarBoton(_btnEstado, u.Activo ? Estilo.Error : Estilo.Exito);
        }

        private static void PintarBoton(Button b, Color color)
        {
            b.BackColor = color;
            b.FlatAppearance.MouseOverBackColor = ControlPaint.Dark(color, 0.1f);
            b.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(color, 0.2f);
        }

        // ================== Acciones ==================

        private void Limpiar()
        {
            _seleccionado = null;
            _txtUsuario.Enabled = true;
            _txtNombre.Enabled = true;
            _txtUsuario.Clear();
            _txtNombre.Clear();
            _cboRol.SelectedIndex = -1;
            _txtClave.Clear();
            _txtRepetir.Clear();
            _grid.ClearSelection();
            LimpiarErrores();
            ConfigurarBotonEstado(null);
            _txtUsuario.Focus();
        }

        private void LimpiarErrores()
        {
            foreach (Label l in new[] { _errUsuario, _errNombre, _errRol, _errClave, _errRepetir })
                Estilo.MostrarErrorCampo(l, null);
        }

        private void MostrarErrores(Dictionary<string, string> errores)
        {
            string m;
            if (errores.TryGetValue("NombreUsuario", out m)) Estilo.MostrarErrorCampo(_errUsuario, m);
            if (errores.TryGetValue("NombreCompleto", out m)) Estilo.MostrarErrorCampo(_errNombre, m);
            if (errores.TryGetValue("Rol", out m)) Estilo.MostrarErrorCampo(_errRol, m);
            if (errores.TryGetValue("Contrasena", out m)) Estilo.MostrarErrorCampo(_errClave, m);
            if (errores.TryGetValue("Confirmacion", out m)) Estilo.MostrarErrorCampo(_errRepetir, m);
        }

        private Rol RolElegido()
        {
            return (Rol)(_cboRol.SelectedIndex + 1);
        }

        /// <summary>Revisa lo que se puede revisar en pantalla (vacíos, política, coincidencia) y muestra TODOS los errores a la vez.</summary>
        private Dictionary<string, string> ValidarEnPantalla(bool esNuevo, bool hayClave)
        {
            Dictionary<string, string> errores = new Dictionary<string, string>();
            if (esNuevo)
            {
                if (string.IsNullOrWhiteSpace(_txtUsuario.Text)) errores["NombreUsuario"] = "El usuario es obligatorio.";
                if (string.IsNullOrWhiteSpace(_txtNombre.Text)) errores["NombreCompleto"] = "El nombre completo es obligatorio.";
            }
            if (_cboRol.SelectedIndex < 0) errores["Rol"] = "Seleccione un rol.";

            if (esNuevo || hayClave)
            {
                string politica = AutenticacionServicio.ValidarPolitica(_txtClave.Text);
                if (politica != null) errores["Contrasena"] = politica;
                if (_txtClave.Text != _txtRepetir.Text) errores["Confirmacion"] = "La contraseña y su confirmación no coinciden.";
            }
            return errores;
        }

        private void BtnGuardar_Click(object sender, EventArgs e)
        {
            LimpiarErrores();
            bool esNuevo = _seleccionado == null;
            bool hayClave = !string.IsNullOrEmpty(_txtClave.Text) || !string.IsNullOrEmpty(_txtRepetir.Text);

            Dictionary<string, string> errores = ValidarEnPantalla(esNuevo, hayClave);
            if (errores.Count > 0)
            {
                MostrarErrores(errores);
                return;
            }

            Cursor = Cursors.WaitCursor; // PBKDF2 con 100000 iteraciones tarda una fracción de segundo
            try
            {
                if (esNuevo)
                {
                    _servicio.Crear(_txtUsuario.Text, _txtNombre.Text, _txtClave.Text, _txtRepetir.Text, RolElegido());
                    Recargar();
                    Limpiar();
                    Estilo.MostrarExito(this, "Usuario creado.");
                    return;
                }

                bool cambioRol = RolElegido() != _seleccionado.Rol;
                if (!cambioRol && !hayClave)
                {
                    Estilo.MostrarInfo(this, "No hay cambios para guardar: cambie el rol o escriba una contraseña nueva.");
                    return;
                }

                if (cambioRol) _servicio.CambiarRol(_seleccionado.IdUsuario, RolElegido());
                if (hayClave) _servicio.RestablecerContrasena(_seleccionado.IdUsuario, _txtClave.Text, _txtRepetir.Text);

                Recargar();
                Limpiar();
                Estilo.MostrarExito(this, "Usuario actualizado.");
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
                Estilo.MostrarError(this, "No se pudo guardar el usuario. Verifique la conexión con la base de datos.");
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void BtnEstado_Click(object sender, EventArgs e)
        {
            if (_seleccionado == null) return;
            bool nuevoEstado = !_seleccionado.Activo;

            if (!nuevoEstado)
            {
                DialogResult r = MessageBox.Show(this,
                    "¿Desactivar al usuario '" + _seleccionado.NombreUsuario + "'?\nYa no podrá iniciar sesión.",
                    "SIGEPOSTA", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                if (r != DialogResult.Yes) return;
            }

            try
            {
                _servicio.CambiarActivo(_seleccionado.IdUsuario, nuevoEstado);
                Recargar();
                Limpiar();
                Estilo.MostrarExito(this, nuevoEstado ? "Usuario activado." : "Usuario desactivado.");
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
                Estilo.MostrarError(this, "No se pudo cambiar el estado del usuario. Verifique la conexión con la base de datos.");
            }
        }
    }
}
