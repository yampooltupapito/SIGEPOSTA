using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SIGEPOSTA.Negocio;
using SIGEPOSTA.Seguridad;

namespace SIGEPOSTA.Presentacion
{
    /// <summary>
    /// Respaldo (.sigebak) de historias clínicas y verificación de respaldos existentes.
    /// Decisiones de diseño:
    ///  - Todo el trabajo pesado (leer la BD, comprimir, cifrar, descifrar) corre en un BackgroundWorker
    ///    con barra de progreso: la ventana no se congela y se puede cancelar un respaldo de posta.
    ///  - El worker NO toca controles: solo informa progreso; la pantalla se actualiza en los eventos
    ///    ProgressChanged y RunWorkerCompleted, que se ejecutan en el hilo de la interfaz.
    ///  - Mientras corre una tarea se PAUSA el contador de inactividad, para que el cierre automático de
    ///    sesión no la interrumpa.
    ///  - Los errores se traducen a mensajes amables; el detalle técnico va solo al registro de errores.
    /// </summary>
    public class FrmRespaldo : Form
    {
        private const string FiltroArchivo = "Respaldo SIGEPOSTA (*.sigebak)|*.sigebak";

        /// <summary>Datos que se le pasan al BackgroundWorker.</summary>
        private class TareaRespaldo
        {
            public string Tipo;      // "paciente", "establecimiento" o "verificar"
            public Paciente Paciente;
            public string Ruta;
        }

        private readonly PacienteServicio _pacientesServicio = new PacienteServicio();
        private readonly RespaldoServicio _servicio = new RespaldoServicio();
        private readonly BackgroundWorker _worker = new BackgroundWorker();
        private RespaldoArchivo _abierto;
        private bool _ocupado;

        private ComboBox _cboAlcance, _cboElemento;
        private TextBox _txtBuscar;
        private Label _errElemento, _errBuscar, _lblResumen, _lblEstado;
        private Button _btnRespaldar, _btnAbrir, _btnCancelar, _btnBuscar;
        private ProgressBar _barra;
        private DataGridView _gridPacientes, _gridAtenciones;

        public FrmRespaldo()
        {
            Permisos.ExigirAccesoPacientes(); // Administrador y Médico
            Construir();
            ConfigurarWorker();
            Load += FrmRespaldo_Load;
            FormClosing += FrmRespaldo_FormClosing;
        }

        // ================== Construcción de la pantalla ==================

        private void Construir()
        {
            Text = "SIGEPOSTA - Respaldo de historias clínicas";
            Font = Estilo.Fuente;
            BackColor = Estilo.Fondo;
            ForeColor = Estilo.Texto;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1100, 780);
            MinimumSize = new Size(900, 660);
            AutoScroll = true;                              // si la pantalla es pequeña, aparece barra en vez de recortar
            AutoScrollMinSize = new Size(860, 735);         // alto mínimo para que la tabla de atenciones tenga espacio

            TableLayoutPanel raiz = new TableLayoutPanel();
            raiz.Dock = DockStyle.Fill;
            raiz.ColumnCount = 1;
            raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            raiz.RowCount = 4;
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // barra de título
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // crear respaldo
            raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // verificar respaldo
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // progreso

            raiz.Controls.Add(Estilo.CrearBarraTitulo("Respaldo de historias clínicas"), 0, 0);
            raiz.Controls.Add(ConstruirCrear(), 0, 1);
            raiz.Controls.Add(ConstruirVerificar(), 0, 2);
            raiz.Controls.Add(ConstruirProgreso(), 0, 3);
            Controls.Add(raiz);

            _cboAlcance.SelectedIndexChanged += delegate { CargarElementos(); };
            _btnRespaldar.Click += BtnRespaldar_Click;
            _btnBuscar.Click += delegate { BuscarPaciente(); };
            _txtBuscar.KeyDown += delegate (object s, KeyEventArgs e)
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;   // sin "pitido" de Windows
                BuscarPaciente();
            };
            _btnAbrir.Click += BtnAbrir_Click;
            _btnCancelar.Click += delegate { _worker.CancelAsync(); _btnCancelar.Enabled = false; };
            _gridPacientes.SelectionChanged += delegate { MostrarAtenciones(); };

            Estilo.EscCierra(this);
        }

        private GroupBox ConstruirCrear()
        {
            _cboAlcance = Estilo.CrearCombo();
            _cboAlcance.Items.Add("Un paciente");
            _cboAlcance.Items.Add("Todas las historias del establecimiento");
            _cboElemento = Estilo.CrearCombo();
            _txtBuscar = Estilo.CrearTextBox(false, 100);
            Estilo.PonerAyuda(_txtBuscar, "DNI, apellido, nombre o N.º de historia");

            _btnBuscar = Estilo.CrearBotonSecundario("Buscar paciente");
            _btnRespaldar = Estilo.CrearBotonPrimario("Respaldar…");
            foreach (Button b in new[] { _btnBuscar, _btnRespaldar })
            {
                b.Dock = DockStyle.None;
                b.AutoSize = false;
                b.Size = new Size(170, Estilo.AltoBoton);
                b.MinimumSize = new Size(170, Estilo.AltoBoton);   // alto completo: el texto nunca se corta
                b.Margin = new Padding(Estilo.EspacioCampos, 0, 0, 0);
            }

            // Dos columnas iguales. Fila 1: "Qué respaldar" | "Buscar paciente".
            // Fila 2: "Paciente elegido" | botones a la derecha. Fila 3: texto de ayuda.
            TableLayoutPanel cuadricula = new TableLayoutPanel();
            cuadricula.ColumnCount = 2;
            cuadricula.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            cuadricula.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            cuadricula.RowCount = 3;
            cuadricula.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            cuadricula.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            cuadricula.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            cuadricula.Dock = DockStyle.Fill;
            cuadricula.AutoSize = true;
            cuadricula.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            cuadricula.Margin = Padding.Empty;

            Label sinError, errBuscar, errElegido;
            TableLayoutPanel c1 = Estilo.CrearCampoConError("Qué respaldar", _cboAlcance, out sinError);
            TableLayoutPanel c2 = Estilo.CrearCampoConError("Buscar paciente", _txtBuscar, out errBuscar);
            TableLayoutPanel c3 = Estilo.CrearCampoConError("Paciente elegido", _cboElemento, out errElegido);
            _errBuscar = errBuscar;
            _errElemento = errElegido;
            c1.Margin = new Padding(0, 0, Estilo.EspacioCampos, Estilo.EspacioCampos);
            c2.Margin = new Padding(0, 0, 0, Estilo.EspacioCampos);
            c3.Margin = new Padding(0, 0, Estilo.EspacioCampos, Estilo.EspacioCampos);

            // Botones juntos y alineados a la derecha; su centro vertical coincide con el de la lista "Paciente elegido"
            // (la etiqueta y los 4 px de separación quedan arriba del cuadro).
            FlowLayoutPanel botones = new FlowLayoutPanel();
            botones.FlowDirection = FlowDirection.RightToLeft;   // el primero que se agrega queda a la derecha
            botones.WrapContents = false;
            botones.AutoSize = true;
            botones.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            botones.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            int arriba = Math.Max(0, Estilo.Fuente.Height + Estilo.EspacioEtiqueta + Estilo.AltoCampo / 2 - Estilo.AltoBoton / 2);
            botones.Margin = new Padding(0, arriba, 0, Estilo.EspacioCampos);
            botones.Controls.Add(_btnRespaldar);
            botones.Controls.Add(_btnBuscar);

            Label ayuda = Estilo.CrearTexto("El archivo se comprime y se cifra (AES) antes de guardarse. " +
                "Solo se puede abrir con este mismo usuario de Windows en este equipo.", 900);
            ayuda.ForeColor = Estilo.Gris;      // gris y en el tamaño normal de la aplicación (legible)
            ayuda.Margin = new Padding(0, 0, 0, 0);

            // El texto de ayuda se parte en varias líneas según el ancho REAL disponible (si no, se corta al achicar la ventana).
            cuadricula.SizeChanged += delegate
            {
                int ancho = Math.Max(200, cuadricula.ClientSize.Width);
                if (ayuda.MaximumSize.Width != ancho) ayuda.MaximumSize = new Size(ancho, 0);
            };

            cuadricula.Controls.Add(c1, 0, 0);
            cuadricula.Controls.Add(c2, 1, 0);
            cuadricula.Controls.Add(c3, 0, 1);
            cuadricula.Controls.Add(botones, 1, 1);
            cuadricula.Controls.Add(ayuda, 0, 2);
            cuadricula.SetColumnSpan(ayuda, 2);

            GroupBox g = Estilo.CrearRecuadro("Crear respaldo");
            g.Controls.Add(cuadricula);
            return g;
        }

        private GroupBox ConstruirVerificar()
        {
            _btnAbrir = Estilo.CrearBotonSecundario("Abrir respaldo…");
            _btnAbrir.Dock = DockStyle.None;
            _btnAbrir.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            _btnAbrir.Size = new Size(190, Estilo.AltoBoton);
            _btnAbrir.MinimumSize = new Size(190, Estilo.AltoBoton);
            _btnAbrir.Margin = new Padding(Estilo.EspacioCampos, 0, 0, Estilo.EspacioCampos);

            _lblResumen = Estilo.CrearTexto(
                "Elija un archivo .sigebak para verificarlo y ver su contenido. Esta opción no modifica la base de datos.", 700);
            _lblResumen.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            _lblResumen.Margin = new Padding(0, 8, 0, Estilo.EspacioCampos);

            TableLayoutPanel arriba = new TableLayoutPanel();
            arriba.ColumnCount = 2;
            arriba.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            arriba.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            arriba.RowCount = 1;
            arriba.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            arriba.Dock = DockStyle.Fill;
            arriba.AutoSize = true;
            arriba.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            arriba.Margin = Padding.Empty;
            arriba.Controls.Add(_lblResumen, 0, 0);
            arriba.Controls.Add(_btnAbrir, 1, 0);

            // Tabla de resumen: altura fija de ~3 filas (encabezado + 3 filas).
            _gridPacientes = new DataGridView();
            Estilo.EstilizarGrid(_gridPacientes);
            _gridPacientes.Columns.Add("Historia", "N.º historia");
            _gridPacientes.Columns.Add("Paciente", "Paciente");
            _gridPacientes.Columns.Add("Dni", "DNI");
            _gridPacientes.Columns.Add("Atenciones", "Atenciones");
            _gridPacientes.Columns["Historia"].FillWeight = 20;
            _gridPacientes.Columns["Paciente"].FillWeight = 40;
            _gridPacientes.Columns["Dni"].FillWeight = 20;
            _gridPacientes.Columns["Atenciones"].FillWeight = 20;
            _gridPacientes.Columns["Historia"].MinimumWidth = 110;
            _gridPacientes.Columns["Atenciones"].MinimumWidth = 110;
            int altoResumen = _gridPacientes.ColumnHeadersHeight + 3 * _gridPacientes.RowTemplate.Height + 2;
            _gridPacientes.Dock = DockStyle.Fill;
            _gridPacientes.Margin = new Padding(0, 0, 0, Estilo.EspacioCampos);

            Label titulo = Estilo.CrearTexto("Atenciones del paciente seleccionado", 600);
            titulo.Font = Estilo.FuenteNegrita;
            titulo.Margin = new Padding(0, 0, 0, Estilo.EspacioEtiqueta);

            // Tabla de atenciones: ocupa TODO el espacio restante. "Motivo" y "Diagnóstico" son las más anchas.
            _gridAtenciones = new DataGridView();
            Estilo.EstilizarGrid(_gridAtenciones);
            _gridAtenciones.Columns.Add("Fecha", "Fecha");
            _gridAtenciones.Columns.Add("Motivo", "Motivo");
            _gridAtenciones.Columns.Add("Cie10", "CIE-10");
            _gridAtenciones.Columns.Add("Diagnostico", "Diagnóstico");
            _gridAtenciones.Columns.Add("Signos", "Signos");
            _gridAtenciones.Columns.Add("Receta", "Receta");
            _gridAtenciones.Columns["Fecha"].FillWeight = 12;
            _gridAtenciones.Columns["Motivo"].FillWeight = 18;
            _gridAtenciones.Columns["Cie10"].FillWeight = 22;
            _gridAtenciones.Columns["Diagnostico"].FillWeight = 20;
            _gridAtenciones.Columns["Signos"].FillWeight = 16;
            _gridAtenciones.Columns["Receta"].FillWeight = 12;
            _gridAtenciones.Columns["Fecha"].MinimumWidth = 135;
            _gridAtenciones.Columns["Receta"].MinimumWidth = 90;
            _gridAtenciones.Dock = DockStyle.Fill;
            _gridAtenciones.Margin = Padding.Empty;   // mismo ancho que la tabla de resumen

            TableLayoutPanel contenido = new TableLayoutPanel();
            contenido.ColumnCount = 1;
            contenido.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            contenido.RowCount = 4;
            contenido.RowStyles.Add(new RowStyle(SizeType.AutoSize));                 // texto + botón "Abrir respaldo"
            contenido.RowStyles.Add(new RowStyle(SizeType.Absolute, altoResumen + Estilo.EspacioCampos)); // resumen (≈3 filas)
            contenido.RowStyles.Add(new RowStyle(SizeType.AutoSize));                 // título de la tabla de atenciones
            contenido.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));            // atenciones: todo lo que sobra
            contenido.Dock = DockStyle.Fill;
            contenido.Margin = Padding.Empty;
            contenido.Controls.Add(arriba, 0, 0);
            contenido.Controls.Add(_gridPacientes, 0, 1);
            contenido.Controls.Add(titulo, 0, 2);
            contenido.Controls.Add(_gridAtenciones, 0, 3);

            GroupBox g = Estilo.CrearRecuadro("Verificar o restaurar un respaldo");
            g.AutoSize = false;
            g.Margin = new Padding(Estilo.MargenInterior, 0, Estilo.MargenInterior, Estilo.MargenInterior);
            g.Controls.Add(contenido);
            return g;
        }

        private Control ConstruirProgreso()
        {
            // La barra y el botón Cancelar tienen EXACTAMENTE el mismo alto; el texto de estado va debajo, a la izquierda.
            _barra = Estilo.CrearBarraProgreso();
            _barra.Height = Estilo.AltoBoton;
            _barra.Dock = DockStyle.Fill;
            _barra.Margin = new Padding(0, 0, Estilo.EspacioCampos, 0);

            _lblEstado = new Label();
            _lblEstado.Text = "Listo.";
            _lblEstado.Font = Estilo.Fuente;
            _lblEstado.ForeColor = Estilo.Texto;
            _lblEstado.AutoSize = true;
            _lblEstado.Margin = new Padding(0, Estilo.EspacioEtiqueta, 0, 0);

            _btnCancelar = Estilo.CrearBotonSecundario("Cancelar");
            _btnCancelar.Enabled = false;
            _btnCancelar.Dock = DockStyle.None;
            _btnCancelar.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            _btnCancelar.Size = new Size(150, Estilo.AltoBoton);
            _btnCancelar.MinimumSize = new Size(150, Estilo.AltoBoton);
            _btnCancelar.Margin = Padding.Empty;

            TableLayoutPanel t = new TableLayoutPanel();
            t.ColumnCount = 2;
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.RowCount = 2;
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, Estilo.AltoBoton));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Dock = DockStyle.Fill;
            t.AutoSize = true;
            t.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            // Mismos márgenes laterales que los recuadros de arriba: queda alineado con su borde izquierdo.
            t.Margin = new Padding(Estilo.MargenInterior, 0, Estilo.MargenInterior, Estilo.MargenInterior);
            t.Controls.Add(_barra, 0, 0);
            t.Controls.Add(_btnCancelar, 1, 0);
            t.Controls.Add(_lblEstado, 0, 1);
            t.SetColumnSpan(_lblEstado, 2);
            return t;
        }

        // ================== Datos y BackgroundWorker ==================

        private void ConfigurarWorker()
        {
            _worker.WorkerReportsProgress = true;
            _worker.WorkerSupportsCancellation = true;
            _worker.DoWork += Worker_DoWork;
            _worker.ProgressChanged += Worker_ProgressChanged;
            _worker.RunWorkerCompleted += Worker_Completed;
        }

        private void FrmRespaldo_Load(object sender, EventArgs e)
        {
            // Ya no se carga la lista completa de pacientes al abrir: la ventana abre al instante y el paciente se busca.
            _cboAlcance.SelectedIndex = 0; // dispara CargarElementos
            _txtBuscar.Focus();
        }

        private void CargarElementos()
        {
            _cboElemento.DataSource = null;
            bool unPaciente = _cboAlcance.SelectedIndex == 0;
            // Un paciente: se busca por DNI, apellido, nombre o N.º de historia.
            // Todo el establecimiento: no hay nada que buscar (se respaldan todas las historias activas).
            _txtBuscar.Enabled = unPaciente && !_ocupado;
            _btnBuscar.Enabled = unPaciente && !_ocupado;
            _cboElemento.Enabled = false;
            Estilo.MostrarErrorCampo(_errElemento, null);
            Estilo.MostrarErrorCampo(_errBuscar, null);
        }

        /// <summary>
        /// Busca pacientes: 8 dígitos = DNI (se compara por HMAC, sin descifrar nada); cualquier otro texto = apellido,
        /// nombre o N.º de historia (primera página de 50 resultados). Si hay un solo resultado queda elegido.
        /// </summary>
        private void BuscarPaciente()
        {
            Estilo.MostrarErrorCampo(_errBuscar, null);
            Estilo.MostrarErrorCampo(_errElemento, null);
            _cboElemento.DataSource = null;
            _cboElemento.Enabled = false;

            string texto = _txtBuscar.Text.Trim();
            if (texto.Length == 0)
            {
                Estilo.MostrarErrorCampo(_errBuscar, "Escriba un DNI, apellido, nombre o N.º de historia.");
                return;
            }

            Cursor = Cursors.WaitCursor;
            try
            {
                List<Paciente> encontrados = new List<Paciente>();
                bool soloDigitos = texto.All(char.IsDigit);
                if (soloDigitos)
                {
                    if (texto.Length != 8)
                    {
                        Estilo.MostrarErrorCampo(_errBuscar, "Un DNI tiene 8 dígitos.");
                        return;
                    }
                    Paciente p = _pacientesServicio.BuscarPorDni(texto);
                    if (p != null && p.Activo) encontrados.Add(p);
                }
                else
                {
                    ResultadoPagina<Paciente> r = _pacientesServicio.BuscarPagina(texto, 1, null);
                    if (r != null) encontrados.AddRange(r.Items);
                    if (r != null && r.Total > r.Items.Count)
                        Estilo.MostrarErrorCampo(_errBuscar, "Hay " + r.Total + " coincidencias; se muestran las primeras " +
                                                              r.Items.Count + ". Escriba más letras para acotar.");
                }

                if (encontrados.Count == 0)
                {
                    Estilo.MostrarErrorCampo(_errBuscar, "No se encontró ningún paciente activo con ese dato.");
                    return;
                }

                _cboElemento.DisplayMember = "NombreCompleto";
                _cboElemento.DataSource = encontrados;
                _cboElemento.Enabled = !_ocupado;
                _cboElemento.SelectedIndex = encontrados.Count == 1 ? 0 : -1;   // uno solo: ya queda elegido
                if (encontrados.Count > 1) _cboElemento.DroppedDown = true;
            }
            catch (ValidacionException ex)
            {
                Estilo.MostrarErrorCampo(_errBuscar, ex.Message);
            }
            catch (AccesoDenegadoException ex)
            {
                Estilo.MostrarError(this, ex.Message);
            }
            catch (Exception)
            {
                Estilo.MostrarError(this, "No se pudo buscar al paciente. Verifique la conexión con la base de datos.");
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        /// <summary>Corre en OTRO hilo: aquí no se toca ningún control, solo se informa progreso.</summary>
        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            TareaRespaldo t = (TareaRespaldo)e.Argument;
            Action<int, string> progreso = delegate (int porcentaje, string texto) { _worker.ReportProgress(porcentaje, texto); };

            switch (t.Tipo)
            {
                case "paciente":
                    _servicio.ExportarPaciente(t.Paciente, t.Ruta, progreso);
                    e.Result = "Respaldo guardado correctamente en:\n" + t.Ruta;
                    break;

                case "establecimiento":
                    int cantidad = _servicio.ExportarEstablecimiento(t.Ruta, progreso, delegate { return _worker.CancellationPending; });
                    if (cantidad < 0)
                    {
                        e.Cancel = true;
                        return;
                    }
                    e.Result = "Respaldo guardado correctamente (" + cantidad + " historia(s)) en:\n" + t.Ruta;
                    break;

                default: // "verificar"
                    progreso(20, "Verificando la integridad del archivo…");
                    RespaldoArchivo archivo = _servicio.Verificar(t.Ruta);
                    progreso(100, "Respaldo verificado.");
                    e.Result = archivo;
                    break;
            }
        }

        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            _barra.Value = Math.Max(0, Math.Min(100, e.ProgressPercentage));
            string texto = e.UserState as string;
            if (texto != null) _lblEstado.Text = texto;
        }

        private void Worker_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            Ocupado(false);

            if (e.Error != null)
            {
                _lblEstado.Text = "La operación no se completó.";
                Estilo.MostrarError(this, MensajeAmable(e.Error));
                return;
            }
            if (e.Cancelled)
            {
                _barra.Value = 0;
                _lblEstado.Text = "Respaldo cancelado. No se creó ningún archivo.";
                return;
            }

            RespaldoArchivo archivo = e.Result as RespaldoArchivo;
            if (archivo != null)
            {
                MostrarContenido(archivo);
                _lblEstado.Text = "Respaldo verificado: la integridad y la llave son correctas.";
                return;
            }

            _lblEstado.Text = "Respaldo terminado.";
            Estilo.MostrarExito(this, Convert.ToString(e.Result));
        }

        /// <summary>Convierte cualquier error en un mensaje apto para el usuario (sin texto técnico).</summary>
        private static string MensajeAmable(Exception ex)
        {
            if (ex is RespaldoInvalidoException || ex is AccesoDenegadoException ||
                ex is ReglaNegocioException || ex is ValidacionException)
                return ex.Message;

            if (ex is IOException || ex is UnauthorizedAccessException)
                return "No se pudo leer o escribir el archivo. Verifique la ruta, que el archivo no esté en uso " +
                       "y que tenga permiso sobre esa carpeta.";

            string codigo = ManejadorErrores.Registrar(ex);
            return "No se pudo completar la operación. Si el problema continúa, avise al administrador e indique el código: " + codigo;
        }

        /// <summary>Activa o desactiva la pantalla mientras corre una tarea, y pausa el cierre por inactividad.</summary>
        private void Ocupado(bool ocupado)
        {
            if (ocupado == _ocupado) return;
            _ocupado = ocupado;
            if (ocupado) MonitorInactividad.Pausar(); else MonitorInactividad.Reanudar();

            _btnRespaldar.Enabled = !ocupado;
            _btnAbrir.Enabled = !ocupado;
            _cboAlcance.Enabled = !ocupado;
            _cboElemento.Enabled = !ocupado && _cboAlcance.SelectedIndex == 0 && _cboElemento.Items.Count > 0;
            _txtBuscar.Enabled = !ocupado && _cboAlcance.SelectedIndex == 0;
            _btnBuscar.Enabled = !ocupado && _cboAlcance.SelectedIndex == 0;
            _btnCancelar.Enabled = ocupado;
            UseWaitCursor = ocupado;
        }

        private void Iniciar(TareaRespaldo tarea)
        {
            _barra.Value = 0;
            _lblEstado.Text = "Iniciando…";
            Ocupado(true);
            _worker.RunWorkerAsync(tarea);
        }

        private void FrmRespaldo_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_ocupado) return;
            // No se cierra la ventana en medio de una tarea: primero se cancela.
            e.Cancel = true;
            _worker.CancelAsync();
            Estilo.MostrarInfo(this, "Hay una operación en curso. Espere a que termine o use Cancelar.");
        }

        // ================== Acciones ==================

        private void BtnRespaldar_Click(object sender, EventArgs e)
        {
            Estilo.MostrarErrorCampo(_errElemento, null);
            bool esPaciente = _cboAlcance.SelectedIndex == 0;
            Paciente paciente = esPaciente ? _cboElemento.SelectedItem as Paciente : null;

            if (esPaciente && paciente == null)
            {
                Estilo.MostrarErrorCampo(_errElemento, "Busque y elija un paciente.");
                return;
            }

            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Title = "Guardar respaldo";
                dlg.Filter = FiltroArchivo;
                dlg.DefaultExt = "sigebak";
                dlg.AddExtension = true;
                dlg.OverwritePrompt = true;
                // El nombre sugerido NO lleva datos personales: solo el N.º de historia o la palabra "Establecimiento".
                dlg.FileName = NombreSeguro(esPaciente ? paciente.NumeroHistoria : "Historias_Establecimiento") +
                               "_" + DateTime.Now.ToString("yyyyMMdd");
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                Iniciar(new TareaRespaldo
                {
                    Tipo = esPaciente ? "paciente" : "establecimiento",
                    Paciente = paciente,
                    Ruta = dlg.FileName
                });
            }
        }

        private void BtnAbrir_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "Abrir respaldo";
                dlg.Filter = FiltroArchivo;
                dlg.CheckFileExists = true;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                Iniciar(new TareaRespaldo { Tipo = "verificar", Ruta = dlg.FileName });
            }
        }

        private void MostrarContenido(RespaldoArchivo a)
        {
            _abierto = a;
            _lblResumen.Text = "Respaldo del " + a.FechaRespaldo.ToString("dd/MM/yyyy HH:mm") + " · " +
                // Cualquier alcance distinto de "Paciente" es el establecimiento (los respaldos antiguos decían "Posta").
                (a.Alcance == "Paciente" ? "Un paciente" : "Establecimiento: " + a.Posta) + " · " +
                a.Pacientes.Count + " historia(s) · generado por " + a.GeneradoPor;

            _gridPacientes.Rows.Clear();
            foreach (PacienteRespaldo p in a.Pacientes.OrderBy(x => x.Apellidos).ThenBy(x => x.Nombres))
            {
                int i = _gridPacientes.Rows.Add(p.NumeroHistoria, p.Apellidos + ", " + p.Nombres, p.Dni, p.Atenciones.Count);
                _gridPacientes.Rows[i].Tag = p;
            }
            _gridPacientes.ClearSelection();
            _gridAtenciones.Rows.Clear();
            if (_gridPacientes.Rows.Count > 0) _gridPacientes.Rows[0].Selected = true;
        }

        private void MostrarAtenciones()
        {
            _gridAtenciones.Rows.Clear();
            if (_gridPacientes.SelectedRows.Count == 0) return;
            PacienteRespaldo p = _gridPacientes.SelectedRows[0].Tag as PacienteRespaldo;
            if (p == null) return;
            foreach (AtencionRespaldo a in p.Atenciones)
                _gridAtenciones.Rows.Add(a.Fecha.ToString("dd/MM/yyyy HH:mm"), a.Motivo, a.Cie10, a.Diagnostico, a.Signos == null ? string.Empty : a.Signos.Resumen, a.Receta);
            _gridAtenciones.ClearSelection();
        }

        private static string NombreSeguro(string texto)
        {
            char[] invalidos = Path.GetInvalidFileNameChars();
            string limpio = new string((texto ?? "respaldo").Select(c => invalidos.Contains(c) || c == ' ' ? '_' : c).ToArray());
            return limpio.Length == 0 ? "respaldo" : limpio;
        }
    }
}
