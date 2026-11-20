using System;
using System.Windows.Forms;
using SIGEPOSTA.Negocio;

namespace SIGEPOSTA.Presentacion
{
    /// <summary>
    /// Se muestra solo la primera vez (cuando no existe ningún usuario) para crear el administrador inicial.
    /// La contraseña la elige quien instala el sistema: no está escrita en ningún archivo del programa.
    /// </summary>
    public class FrmCrearAdmin : Form
    {
        private readonly AutenticacionServicio _servicio;
        private TextBox _txtUsuario;
        private TextBox _txtNombre;
        private TextBox _txtClave;
        private TextBox _txtConfirmar;
        private Label _lblMensaje;
        private Button _btnCrear;
        private Button _btnCancelar;

        public FrmCrearAdmin(AutenticacionServicio servicio)
        {
            _servicio = servicio;
            Construir();
        }

        private void Construir()
        {
            Estilo.PrepararDialogo(this, "SIGEPOSTA - Crear administrador");

            _txtUsuario = Estilo.CrearTextBox(false, 50);
            _txtNombre = Estilo.CrearTextBox(false, 120);
            _txtClave = Estilo.CrearTextBox(true, 100);
            _txtConfirmar = Estilo.CrearTextBox(true, 100);
            _lblMensaje = Estilo.CrearEtiquetaMensaje(Estilo.AnchoTextoDialogo);
            _btnCrear = Estilo.CrearBotonPrimario("Crear administrador");
            _btnCancelar = Estilo.CrearBotonSecundario("Cancelar");

            TableLayoutPanel columna = Estilo.CrearColumna();
            Estilo.AgregarFila(columna, Estilo.CrearTexto(
                "No hay usuarios registrados. Cree la cuenta del administrador inicial. " +
                "La contraseña debe tener al menos 8 caracteres, con mayúscula, minúscula y número.",
                Estilo.AnchoTextoDialogo));
            Estilo.AgregarFila(columna, Estilo.CrearCampo("Usuario", _txtUsuario));
            Estilo.AgregarFila(columna, Estilo.CrearCampo("Nombre completo", _txtNombre));
            Estilo.AgregarFila(columna, Estilo.CrearCampo("Contraseña", _txtClave));
            Estilo.AgregarFila(columna, Estilo.CrearCampo("Confirmar contraseña", _txtConfirmar));
            Estilo.AgregarFila(columna, _lblMensaje);
            Estilo.AgregarFila(columna, Estilo.CrearFilaBotones(_btnCrear, _btnCancelar));

            GroupBox recuadro = Estilo.CrearRecuadro("Administrador inicial");
            recuadro.Controls.Add(columna);

            TableLayoutPanel raiz = Estilo.CrearRaizDialogo();
            Estilo.AgregarFila(raiz, Estilo.CrearBarraTitulo("SIGEPOSTA - Primera vez"));
            Estilo.AgregarFila(raiz, recuadro);
            Controls.Add(raiz);

            AcceptButton = _btnCrear;
            CancelButton = _btnCancelar;
            _btnCrear.Click += BtnCrear_Click;
            _btnCancelar.DialogResult = DialogResult.Cancel;
        }

        private void BtnCrear_Click(object sender, EventArgs e)
        {
            _lblMensaje.Text = string.Empty;
            _btnCrear.Enabled = false;
            Cursor = Cursors.WaitCursor;
            try
            {
                _servicio.CrearAdministradorInicial(_txtUsuario.Text, _txtNombre.Text, _txtClave.Text, _txtConfirmar.Text);
                Estilo.MostrarExito(this, "Administrador creado correctamente. Ahora inicie sesión.");
                DialogResult = DialogResult.OK;
            }
            catch (ValidacionException ex)
            {
                Estilo.Mensaje(_lblMensaje, ex.Message, true); // errores de validación: seguros de mostrar
            }
            catch (ReglaNegocioException ex)
            {
                Estilo.Mensaje(_lblMensaje, ex.Message, true);
            }
            catch (Exception)
            {
                Estilo.Mensaje(_lblMensaje, "No se pudo crear el administrador. Verifique la conexión con la base de datos.", true);
            }
            finally
            {
                Cursor = Cursors.Default;
                _btnCrear.Enabled = true;
            }
        }
    }
}
