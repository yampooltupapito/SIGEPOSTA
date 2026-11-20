using System;
using System.Windows.Forms;
using SIGEPOSTA.Negocio;

namespace SIGEPOSTA.Presentacion
{
    /// <summary>
    /// Ventana de inicio de sesión. Toda la lógica (verificar clave, bloqueos) está en AutenticacionServicio;
    /// aquí solo se recogen los datos y se muestra el resultado.
    /// </summary>
    public class FrmLogin : Form
    {
        private readonly AutenticacionServicio _servicio;
        private TextBox _txtUsuario;
        private TextBox _txtClave;
        private Label _lblMensaje;
        private Button _btnIngresar;
        private Button _btnSalir;

        public FrmLogin(AutenticacionServicio servicio) : this(servicio, null)
        {
        }

        /// <param name="aviso">Texto informativo opcional (ej. la sesión se cerró por inactividad).</param>
        public FrmLogin(AutenticacionServicio servicio, string aviso)
        {
            _servicio = servicio;
            Construir();
            if (!string.IsNullOrEmpty(aviso))
            {
                _lblMensaje.ForeColor = Estilo.Morado; // aviso informativo, no es un error
                _lblMensaje.Text = aviso;
            }
        }

        private void Construir()
        {
            Estilo.PrepararDialogo(this, "SIGEPOSTA — " + Establecimiento.Nombre + " — Iniciar sesión");

            _txtUsuario = Estilo.CrearTextBox(false, 50);
            _txtClave = Estilo.CrearTextBox(true, 100);
            _lblMensaje = Estilo.CrearEtiquetaMensaje(Estilo.AnchoTextoDialogo);
            _btnIngresar = Estilo.CrearBotonPrimario("Ingresar");
            _btnSalir = Estilo.CrearBotonSecundario("Salir");

            TableLayoutPanel columna = Estilo.CrearColumna();
            Estilo.AgregarFila(columna, Estilo.CrearCampo("Usuario", _txtUsuario));
            Estilo.AgregarFila(columna, Estilo.CrearCampo("Contraseña", _txtClave));
            Estilo.AgregarFila(columna, _lblMensaje);
            Estilo.AgregarFila(columna, Estilo.CrearFilaBotones(_btnIngresar, _btnSalir));

            GroupBox recuadro = Estilo.CrearRecuadro("Acceso al sistema");
            recuadro.Controls.Add(columna);

            TableLayoutPanel raiz = Estilo.CrearRaizDialogo();
            Estilo.AgregarFila(raiz, Estilo.CrearBarraTitulo("SIGEPOSTA — " + Establecimiento.Nombre));
            Estilo.AgregarFila(raiz, recuadro);
            Controls.Add(raiz);

            AcceptButton = _btnIngresar;
            CancelButton = _btnSalir;
            _btnIngresar.Click += BtnIngresar_Click;
            _btnSalir.DialogResult = DialogResult.Cancel;
        }

        private void BtnIngresar_Click(object sender, EventArgs e)
        {
            _lblMensaje.Text = string.Empty;
            _btnIngresar.Enabled = false;
            Cursor = Cursors.WaitCursor; // PBKDF2 con 100000 iteraciones tarda una fracción de segundo
            try
            {
                ResultadoLogin r = _servicio.Login(_txtUsuario.Text, _txtClave.Text);
                if (r.Exitoso)
                {
                    DialogResult = DialogResult.OK;
                    return;
                }
                Estilo.Mensaje(_lblMensaje, r.Mensaje, true);
                _txtClave.Clear();
                _txtClave.Focus();
            }
            catch (Exception)
            {
                // No se muestran detalles técnicos (servidor, tablas) en pantalla.
                Estilo.Mensaje(_lblMensaje, "No se pudo iniciar sesión. Verifique la conexión con la base de datos.", true);
            }
            finally
            {
                Cursor = Cursors.Default;
                _btnIngresar.Enabled = true;
            }
        }
    }
}
