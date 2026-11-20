using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using SIGEPOSTA.Negocio;

namespace SIGEPOSTA.Presentacion
{
    /// <summary>
    /// Historia clínica de un paciente. Un aviso rojo arriba si tiene alergias GRAVES activas, sus datos, y cuatro pestañas:
    ///  - Atenciones: lista (diagnóstico descifrado solo para mostrarlo aquí) con los signos vitales de cada una.
    ///  - Nueva atención: motivo, diagnóstico, recuadro de signos vitales y receta (con aviso si coincide con una alergia).
    ///  - Alergias: listar, agregar y desactivar (nunca se borran).
    ///  - Antecedentes: texto libre que se guarda cifrado con AES.
    /// Solo Administrador y Médico entran aquí (Permisos.ExigirAccesoPacientes).
    /// </summary>
    public class FrmHistoriaClinica : Form
    {
        private const string MarcaIlegible = "[No se pudo descifrar]";

        private readonly PacienteServicio _servicio;
        private readonly Paciente _paciente;
        private HistoriaClinica _historia;
        private List<Alergia> _alergias = new List<Alergia>();
        private bool _antecedentesIlegibles;

        // Aviso de alergias graves
        private Panel _bannerAlergia;
        private Label _lblBanner;

        // Atenciones
        private DataGridView _grid;

        // Nueva atención
        private TextBox _txtMotivo, _txtDiagnostico, _txtCantidad;
        private CuadroCie10 _cuadroCie10;
        private Label _errCie10;
        private ComboBox _cboMedicamento;
        private PanelSignosVitales _signos;
        private Label _errMotivo, _errDiagnostico, _errMedicamento, _errCantidad;
        private Button _btnGuardar, _btnCerrar;

        // Alergias
        private TextBox _txtSustancia, _txtObservacion;
        private ComboBox _cboGravedad;
        private Label _errSustancia, _errGravedad, _errObservacion;
        private DataGridView _gridAlergias;
        private Button _btnAgregarAlergia, _btnDesactivarAlergia;
        private TabPage _tabAtenciones, _tabNueva, _tabAlergias, _tabAntecedentes;

        // Antecedentes
        private TextBox _txtAntecedentes;
        private Label _errAntecedentes, _lblAntecedentesAviso;
        private Button _btnGuardarAntecedentes;

        private ToolStripStatusLabel _lblConsulta;

        public FrmHistoriaClinica(PacienteServicio servicio, Paciente paciente)
        {
            if (servicio == null) throw new ArgumentNullException("servicio");
            if (paciente == null) throw new ArgumentNullException("paciente");
            Permisos.ExigirAccesoPacientes();

            _servicio = servicio;
            _paciente = paciente;
            Construir();
            Load += FrmHistoriaClinica_Load;
        }

        private void Construir()
        {
            Text = "SIGEPOSTA - Historia clínica";
            Font = Estilo.Fuente;
            BackColor = Estilo.Fondo;
            ForeColor = Estilo.Texto;
            StartPosition = FormStartPosition.CenterParent;
            int altoPantalla = Screen.PrimaryScreen.WorkingArea.Height;
            Size = new Size(1040, Math.Min(900, altoPantalla - 40));
            MinimumSize = new Size(860, 560);
            AutoScroll = true;
            AutoScrollMinSize = new Size(820, 640);

            TableLayoutPanel raiz = new TableLayoutPanel();
            raiz.Dock = DockStyle.Fill;
            raiz.ColumnCount = 1;
            raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            raiz.RowCount = 5;
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // barra de título
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // aviso de alergias graves
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // datos del paciente
            raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // pestañas
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // botón cerrar

            raiz.Controls.Add(Estilo.CrearBarraTitulo("Historia clínica"), 0, 0);
            raiz.Controls.Add(ConstruirBanner(), 0, 1);
            raiz.Controls.Add(ConstruirDatosPaciente(), 0, 2);
            raiz.Controls.Add(ConstruirPestanas(), 0, 3);

            _btnCerrar = Estilo.CrearBotonSecundario("Cerrar");
            _btnCerrar.Click += delegate { Close(); };
            TableLayoutPanel filaCerrar = Estilo.CrearFilaBotones(_btnCerrar);
            filaCerrar.Margin = new Padding(Estilo.MargenInterior, 0, Estilo.MargenInterior, Estilo.MargenInterior);
            raiz.Controls.Add(filaCerrar, 0, 4);
            Controls.Add(raiz);

            // Barra de estado: "Consulta en X ms" (se agrega después del contenido para acoplarse abajo).
            Controls.Add(Estilo.CrearBarraEstado(out _lblConsulta));

            _btnGuardar.Click += BtnGuardar_Click;
            _btnAgregarAlergia.Click += BtnAgregarAlergia_Click;
            _btnDesactivarAlergia.Click += BtnDesactivarAlergia_Click;
            _btnGuardarAntecedentes.Click += BtnGuardarAntecedentes_Click;

            // Esc cierra; Enter activa el botón principal de la pestaña en la que se está escribiendo.
            Estilo.EscCierra(this);
            Estilo.VincularEnter(this, _tabNueva, _btnGuardar);
            Estilo.VincularEnter(this, _tabAlergias, _btnAgregarAlergia);
            AcceptButton = _btnGuardar;
        }

        // ================== Construcción ==================

        private Panel ConstruirBanner()
        {
            _lblBanner = new Label();
            _lblBanner.Font = Estilo.FuenteNegrita;
            _lblBanner.ForeColor = Color.White;
            _lblBanner.AutoSize = true;
            _lblBanner.MaximumSize = new Size(900, 0);
            _lblBanner.Location = new Point(12, 8);

            _bannerAlergia = new Panel();
            _bannerAlergia.BackColor = Estilo.Error;
            _bannerAlergia.Dock = DockStyle.Fill;
            _bannerAlergia.AutoSize = true;
            _bannerAlergia.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _bannerAlergia.Padding = new Padding(0, 0, 0, 8);
            _bannerAlergia.Margin = new Padding(Estilo.MargenInterior, Estilo.MargenInterior, Estilo.MargenInterior, 0);
            _bannerAlergia.Visible = false;
            _bannerAlergia.Controls.Add(_lblBanner);
            return _bannerAlergia;
        }

        private GroupBox ConstruirDatosPaciente()
        {
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

            AgregarDato(cuadricula, "N.º de historia", "(cargando…)", 0, 0);
            AgregarDato(cuadricula, "DNI", "(cargando…)", 1, 0);
            AgregarDato(cuadricula, "Paciente", _paciente.NombreCompleto, 2, 0);
            AgregarDato(cuadricula, "Edad", _paciente.ObtenerEdad() + " años", 0, 1);
            AgregarDato(cuadricula, "Teléfono", string.IsNullOrEmpty(_paciente.Telefono) ? "—" : _paciente.Telefono, 1, 1);
            AgregarDato(cuadricula, "Dirección", string.IsNullOrEmpty(_paciente.Direccion) ? "—" : _paciente.Direccion, 2, 1);

            GroupBox g = Estilo.CrearRecuadro("Datos del paciente");
            g.Controls.Add(cuadricula);
            return g;
        }

        private void AgregarDato(TableLayoutPanel cuadricula, string etiqueta, string valor, int col, int fila)
        {
            TextBox t = Estilo.CrearTextoSoloLectura();
            t.Text = valor;
            t.Name = "dato_" + etiqueta;
            TableLayoutPanel campo = Estilo.CrearCampo(etiqueta, t);
            campo.Margin = new Padding(0, 0, col < 2 ? Estilo.EspacioCampos : 0, fila == 0 ? Estilo.EspacioCampos : 0);
            cuadricula.Controls.Add(campo, col, fila);
        }

        private TabControl ConstruirPestanas()
        {
            TabControl tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.Font = Estilo.FuenteNegrita;
            tabs.Margin = new Padding(Estilo.MargenInterior, 0, Estilo.MargenInterior, Estilo.MargenInterior);

            _tabAtenciones = NuevaPestana("Atenciones", ConstruirAtenciones());
            _tabNueva = NuevaPestana("Nueva atención", ConstruirNuevaAtencion());
            _tabAlergias = NuevaPestana("Alergias", ConstruirAlergias());
            _tabAntecedentes = NuevaPestana("Antecedentes", ConstruirAntecedentes());
            tabs.TabPages.Add(_tabAtenciones);
            tabs.TabPages.Add(_tabNueva);
            tabs.TabPages.Add(_tabAlergias);
            tabs.TabPages.Add(_tabAntecedentes);
            return tabs;
        }

        private static TabPage NuevaPestana(string titulo, Control contenido)
        {
            TabPage p = new TabPage(titulo);
            p.BackColor = Estilo.Fondo;
            p.Font = Estilo.Fuente;
            p.AutoScroll = true;
            contenido.Dock = DockStyle.Top;
            p.Controls.Add(contenido);
            return p;
        }

        private Control ConstruirAtenciones()
        {
            _grid = new DataGridView();
            Estilo.EstilizarGrid(_grid);
            _grid.Columns.Add("Fecha", "Fecha");
            _grid.Columns.Add("Motivo", "Motivo");
            _grid.Columns.Add("Cie10", "Diagnóstico CIE-10");
            _grid.Columns.Add("Diagnostico", "Detalle del diagnóstico");
            _grid.Columns.Add("Signos", "Signos vitales");
            _grid.Columns.Add("Receta", "Receta");
            _grid.Columns.Add("Medico", "Atendió");
            _grid.Columns["Fecha"].FillWeight = 11;
            _grid.Columns["Motivo"].FillWeight = 13;
            _grid.Columns["Cie10"].FillWeight = 20;
            _grid.Columns["Diagnostico"].FillWeight = 16;
            _grid.Columns["Signos"].FillWeight = 20;
            _grid.Columns["Receta"].FillWeight = 10;
            _grid.Columns["Medico"].FillWeight = 10;
            _grid.TabStop = false;
            _grid.Height = 380;

            // Para que el contenido tenga el alto de la pestaña usamos un panel que rellena.
            Panel p = new Panel();
            p.Height = 400;
            p.Padding = new Padding(Estilo.MargenInterior);
            _grid.Dock = DockStyle.Fill;
            p.Controls.Add(_grid);
            p.Dock = DockStyle.Fill;
            return p;
        }

        private Control ConstruirNuevaAtencion()
        {
            _txtMotivo = Estilo.CrearTextBox(false, 200);
            _txtDiagnostico = Estilo.CrearTextoMultilinea(2, 1000);
            _cuadroCie10 = new CuadroCie10(new Cie10Servicio());
            _signos = new PanelSignosVitales();
            _cboMedicamento = Estilo.CrearCombo();
            _txtCantidad = Estilo.CrearTextBox(false, 7);
            _txtCantidad.KeyPress += delegate (object s, KeyPressEventArgs e)
            {
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true; // solo números
            };
            _btnGuardar = Estilo.CrearBotonPrimario("Guardar atención");

            // Receta opcional: Medicamento 70 % | Cantidad 30 %
            TableLayoutPanel receta = new TableLayoutPanel();
            receta.ColumnCount = 2;
            receta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70f));
            receta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30f));
            receta.RowCount = 1;
            receta.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            receta.Dock = DockStyle.Fill;
            receta.AutoSize = true;
            receta.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            receta.Margin = Padding.Empty;
            TableLayoutPanel cMed = Estilo.CrearCampoConError("Medicamento (opcional)", _cboMedicamento, out _errMedicamento);
            TableLayoutPanel cCant = Estilo.CrearCampoConError("Cantidad", _txtCantidad, out _errCantidad);
            cMed.Margin = new Padding(0, 0, Estilo.EspacioCampos, Estilo.EspacioCampos);
            cCant.Margin = new Padding(0, 0, 0, Estilo.EspacioCampos);
            receta.Controls.Add(cMed, 0, 0);
            receta.Controls.Add(cCant, 1, 0);

            TableLayoutPanel columna = Estilo.CrearColumna();
            Estilo.AgregarFila(columna, Estilo.CrearCampoConError("Motivo de consulta", _txtMotivo, out _errMotivo));
            Estilo.AgregarFila(columna, Estilo.CrearCampoConError("Diagnóstico CIE-10 (escriba el código o parte del nombre; se guarda cifrado)", _cuadroCie10, out _errCie10));
            Estilo.AgregarFila(columna, Estilo.CrearCampoConError("Detalle del diagnóstico (texto libre; se guarda cifrado)", _txtDiagnostico, out _errDiagnostico));
            Estilo.AgregarFila(columna, _signos);
            Estilo.AgregarFila(columna, receta);
            Estilo.AgregarFila(columna, Estilo.CrearFilaBotones(_btnGuardar));
            columna.Margin = new Padding(Estilo.MargenInterior);
            columna.AutoSize = true;
            return columna;
        }

        private Control ConstruirAlergias()
        {
            _txtSustancia = Estilo.CrearTextBox(false, 100);
            _cboGravedad = Estilo.CrearCombo();
            _cboGravedad.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (string g in Alergia.Gravedades) _cboGravedad.Items.Add(g);
            _cboGravedad.SelectedItem = Alergia.Moderada;
            _txtObservacion = Estilo.CrearTextBox(false, 200);
            _btnAgregarAlergia = Estilo.CrearBotonPrimario("Agregar alergia");
            _btnDesactivarAlergia = Estilo.CrearBotonPeligro("Desactivar seleccionada");

            TableLayoutPanel fila = new TableLayoutPanel();
            fila.ColumnCount = 3;
            fila.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35f));
            fila.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
            fila.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));
            fila.RowCount = 1;
            fila.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            fila.Dock = DockStyle.Fill;
            fila.AutoSize = true;
            fila.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            fila.Margin = Padding.Empty;
            TableLayoutPanel c1 = Estilo.CrearCampoConError("Sustancia (ej. Penicilina)", _txtSustancia, out _errSustancia);
            TableLayoutPanel c2 = Estilo.CrearCampoConError("Gravedad", _cboGravedad, out _errGravedad);
            TableLayoutPanel c3 = Estilo.CrearCampoConError("Observación (opcional)", _txtObservacion, out _errObservacion);
            c1.Margin = new Padding(0, 0, Estilo.EspacioCampos, Estilo.EspacioCampos);
            c2.Margin = new Padding(0, 0, Estilo.EspacioCampos, Estilo.EspacioCampos);
            c3.Margin = new Padding(0, 0, 0, Estilo.EspacioCampos);
            fila.Controls.Add(c1, 0, 0);
            fila.Controls.Add(c2, 1, 0);
            fila.Controls.Add(c3, 2, 0);

            _gridAlergias = new DataGridView();
            Estilo.EstilizarGrid(_gridAlergias);
            _gridAlergias.Columns.Add("Sustancia", "Sustancia");
            _gridAlergias.Columns.Add("Gravedad", "Gravedad");
            _gridAlergias.Columns.Add("Observacion", "Observación");
            _gridAlergias.Columns.Add("Estado", "Estado");
            _gridAlergias.Columns.Add("Fecha", "Registrada");
            _gridAlergias.Columns["Sustancia"].FillWeight = 26;
            _gridAlergias.Columns["Gravedad"].FillWeight = 12;
            _gridAlergias.Columns["Observacion"].FillWeight = 34;
            _gridAlergias.Columns["Estado"].FillWeight = 12;
            _gridAlergias.Columns["Fecha"].FillWeight = 16;
            _gridAlergias.Height = 230;
            _gridAlergias.Dock = DockStyle.None;
            _gridAlergias.Width = 880;
            _gridAlergias.Margin = new Padding(0, 0, 0, Estilo.EspacioCampos);

            Label nota = Estilo.CrearTexto(
                "Las alergias no se borran: al desactivar una queda en el historial pero deja de generar avisos. " +
                "Al recetar, el sistema compara el nombre del medicamento con las alergias activas.", 880);
            nota.Font = Estilo.FuentePequena;
            nota.ForeColor = Estilo.Gris;

            TableLayoutPanel columna = Estilo.CrearColumna();
            Estilo.AgregarFila(columna, fila);
            Estilo.AgregarFila(columna, Estilo.CrearFilaBotones(_btnAgregarAlergia));
            Estilo.AgregarFila(columna, _gridAlergias);
            Estilo.AgregarFila(columna, Estilo.CrearFilaBotones(_btnDesactivarAlergia));
            Estilo.AgregarFila(columna, nota);
            columna.Margin = new Padding(Estilo.MargenInterior);
            columna.AutoSize = true;
            return columna;
        }

        private Control ConstruirAntecedentes()
        {
            _txtAntecedentes = Estilo.CrearTextoMultilinea(10, 1000);
            _btnGuardarAntecedentes = Estilo.CrearBotonPrimario("Guardar antecedentes");
            _lblAntecedentesAviso = Estilo.CrearTexto(string.Empty, 880);
            _lblAntecedentesAviso.ForeColor = Estilo.Error;
            _lblAntecedentesAviso.Visible = false;

            TableLayoutPanel columna = Estilo.CrearColumna();
            Estilo.AgregarFila(columna, _lblAntecedentesAviso);
            Estilo.AgregarFila(columna, Estilo.CrearCampoConError(
                "Antecedentes personales y familiares (se guardan cifrados con AES, máx. 1000 caracteres)",
                _txtAntecedentes, out _errAntecedentes));
            Estilo.AgregarFila(columna, Estilo.CrearFilaBotones(_btnGuardarAntecedentes));
            columna.Margin = new Padding(Estilo.MargenInterior);
            columna.AutoSize = true;
            return columna;
        }

        // ================== Carga ==================

        private void FrmHistoriaClinica_Load(object sender, EventArgs e)
        {
            try
            {
                _historia = _servicio.ObtenerHistoria(_paciente.IdPaciente);
                if (_historia == null)
                {
                    Estilo.MostrarError(this, "Este paciente no tiene historia clínica registrada.");
                    Close();
                    return;
                }
                PonerDato("N.º de historia", _historia.NumeroHistoria);
                // El DNI se descifra solo aquí, porque esta pantalla lo muestra.
                PonerDato("DNI", _servicio.DescifrarDni(_paciente.IdPaciente));
                RecargarAtenciones();
                CargarMedicamentos();
                RecargarAlergias();
                CargarAntecedentes();
            }
            catch (AccesoDenegadoException ex)
            {
                Estilo.MostrarError(this, ex.Message);
                Close();
            }
            catch (Exception)
            {
                Estilo.MostrarError(this, "No se pudo cargar la historia clínica. Verifique la conexión con la base de datos.");
            }
        }

        private void PonerDato(string etiqueta, string valor)
        {
            Control[] c = Controls.Find("dato_" + etiqueta, true);
            if (c.Length > 0) c[0].Text = valor;
        }

        private void RecargarAtenciones()
        {
            Stopwatch reloj = Stopwatch.StartNew();
            List<Atencion> lista = _servicio.ListarAtenciones(_historia.IdHistoria);
            reloj.Stop();
            Estilo.MostrarConsulta(_lblConsulta, reloj.ElapsedMilliseconds);   // incluye descifrar los diagnósticos
            _grid.Rows.Clear();
            foreach (Atencion a in lista)
            {
                string signos = (a.Signos == null || !a.Signos.TieneDatos) ? "—" : a.Signos.Resumen();
                _grid.Rows.Add(a.Fecha.ToString("dd/MM/yyyy HH:mm"), a.Motivo, a.Cie10Texto, a.Diagnostico, signos, a.RecetaTexto, a.NombreMedico);
            }
            _grid.ClearSelection();
        }

        /// <summary>Llena la lista de receta con los medicamentos recetables, más "(Sin receta)".</summary>
        private void CargarMedicamentos()
        {
            Medicamento previo = MedicamentoElegido(); // para conservar la selección si sigue disponible
            List<Medicamento> disponibles = _servicio.ListarMedicamentosRecetables(_paciente.IdPosta);
            List<Medicamento> opciones = new List<Medicamento>();
            opciones.Add(new Medicamento { IdMedicamento = 0, Nombre = "(Sin receta)" });
            opciones.AddRange(disponibles);

            _cboMedicamento.DataSource = null;
            _cboMedicamento.DisplayMember = "DescripcionStock";
            _cboMedicamento.ValueMember = "IdMedicamento";
            _cboMedicamento.DataSource = opciones;
            int posicion = previo == null ? 0 : opciones.FindIndex(x => x.IdMedicamento == previo.IdMedicamento);
            _cboMedicamento.SelectedIndex = posicion < 0 ? 0 : posicion;
        }

        private Medicamento MedicamentoElegido()
        {
            Medicamento m = _cboMedicamento.SelectedItem as Medicamento;
            return (m == null || m.IdMedicamento == 0) ? null : m;
        }

        // ================== Alergias ==================

        /// <summary>Vuelve a leer las alergias, llena la tabla y actualiza el aviso rojo de alergias graves.</summary>
        private void RecargarAlergias()
        {
            _alergias = _servicio.ListarAlergias(_paciente.IdPaciente);
            _gridAlergias.Rows.Clear();
            foreach (Alergia a in _alergias)
            {
                int i = _gridAlergias.Rows.Add(a.Sustancia, a.Gravedad,
                    string.IsNullOrEmpty(a.Observacion) ? "—" : a.Observacion,
                    a.Activo ? "Activa" : "Desactivada", a.Fecha.ToString("dd/MM/yyyy"));
                DataGridViewRow fila = _gridAlergias.Rows[i];
                fila.Tag = a;
                if (!a.Activo)
                {
                    fila.DefaultCellStyle.ForeColor = Estilo.Gris;
                    fila.DefaultCellStyle.Font = Estilo.FuentePequena;
                }
                else if (a.EsGrave)
                {
                    fila.DefaultCellStyle.BackColor = Color.FromArgb(253, 224, 224);
                    fila.DefaultCellStyle.ForeColor = Estilo.Error;
                }
                else if (a.Gravedad == Alergia.Moderada)
                {
                    fila.DefaultCellStyle.BackColor = Color.FromArgb(255, 243, 205);
                }
            }
            _gridAlergias.ClearSelection();

            string graves = PacienteServicio.TextoAlergiasGraves(_alergias);
            _bannerAlergia.Visible = graves.Length > 0;
            _lblBanner.Text = graves.Length > 0 ? "⚠ ALERGIA GRAVE: " + graves : string.Empty;
        }

        private void BtnAgregarAlergia_Click(object sender, EventArgs e)
        {
            Estilo.MostrarErrorCampo(_errSustancia, null);
            Estilo.MostrarErrorCampo(_errGravedad, null);
            Estilo.MostrarErrorCampo(_errObservacion, null);

            string gravedad = _cboGravedad.SelectedItem as string;
            Dictionary<string, string> errores = _servicio.ValidarAlergia(_txtSustancia.Text, gravedad, _txtObservacion.Text);
            if (errores.Count > 0)
            {
                string m;
                if (errores.TryGetValue("Sustancia", out m)) Estilo.MostrarErrorCampo(_errSustancia, m);
                if (errores.TryGetValue("Gravedad", out m)) Estilo.MostrarErrorCampo(_errGravedad, m);
                if (errores.TryGetValue("Observacion", out m)) Estilo.MostrarErrorCampo(_errObservacion, m);
                return;
            }

            try
            {
                _servicio.AgregarAlergia(_paciente.IdPaciente, _txtSustancia.Text, gravedad, _txtObservacion.Text);
                _txtSustancia.Clear();
                _txtObservacion.Clear();
                _cboGravedad.SelectedItem = Alergia.Moderada;
                RecargarAlergias();
            }
            catch (ValidacionException ex)
            {
                if (ex.Campo == "Sustancia") Estilo.MostrarErrorCampo(_errSustancia, ex.Message);
                else Estilo.MostrarError(this, ex.Message);
            }
            catch (AccesoDenegadoException ex) { Estilo.MostrarError(this, ex.Message); }
            catch (ReglaNegocioException ex) { Estilo.MostrarError(this, ex.Message); }
            catch (Exception)
            {
                Estilo.MostrarError(this, "No se pudo guardar la alergia. Verifique la conexión con la base de datos.");
            }
        }

        private void BtnDesactivarAlergia_Click(object sender, EventArgs e)
        {
            Alergia a = _gridAlergias.CurrentRow == null ? null : _gridAlergias.CurrentRow.Tag as Alergia;
            if (a == null || !_gridAlergias.CurrentRow.Selected)
            {
                Estilo.MostrarInfo(this, "Seleccione en la tabla la alergia que desea desactivar.");
                return;
            }
            if (!a.Activo)
            {
                Estilo.MostrarInfo(this, "Esa alergia ya está desactivada.");
                return;
            }
            DialogResult r = MessageBox.Show(this,
                "¿Desactivar la alergia a \"" + a.Sustancia + "\"?\n\nNo se borra: queda en el historial, pero dejará de generar avisos.",
                "Desactivar alergia", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (r != DialogResult.Yes) return;

            try
            {
                _servicio.DesactivarAlergia(a.IdAlergia);
                RecargarAlergias();
            }
            catch (AccesoDenegadoException ex) { Estilo.MostrarError(this, ex.Message); }
            catch (ReglaNegocioException ex) { Estilo.MostrarError(this, ex.Message); }
            catch (Exception)
            {
                Estilo.MostrarError(this, "No se pudo desactivar la alergia. Verifique la conexión con la base de datos.");
            }
        }

        // ================== Antecedentes ==================

        private void CargarAntecedentes()
        {
            string texto = _servicio.ObtenerAntecedentes(_historia);
            _antecedentesIlegibles = texto == MarcaIlegible;
            if (_antecedentesIlegibles)
            {
                _txtAntecedentes.Text = string.Empty;
                _lblAntecedentesAviso.Text = "Los antecedentes guardados no se pudieron leer (llave distinta o datos dañados). " +
                                             "Si guarda un texto nuevo, reemplazará a los anteriores.";
                _lblAntecedentesAviso.Visible = true;
            }
            else
            {
                _txtAntecedentes.Text = texto;
                _lblAntecedentesAviso.Visible = false;
            }
        }

        private void BtnGuardarAntecedentes_Click(object sender, EventArgs e)
        {
            Estilo.MostrarErrorCampo(_errAntecedentes, null);
            try
            {
                _servicio.GuardarAntecedentes(_historia.IdHistoria, _txtAntecedentes.Text);
                _antecedentesIlegibles = false;
                _lblAntecedentesAviso.Visible = false;
                Estilo.MostrarExito(this, "Antecedentes guardados (cifrados).");
            }
            catch (ValidacionException ex) { Estilo.MostrarErrorCampo(_errAntecedentes, ex.Message); }
            catch (AccesoDenegadoException ex) { Estilo.MostrarError(this, ex.Message); }
            catch (ReglaNegocioException ex) { Estilo.MostrarError(this, ex.Message); }
            catch (Exception)
            {
                Estilo.MostrarError(this, "No se pudieron guardar los antecedentes. Verifique la conexión con la base de datos.");
            }
        }

        // ================== Nueva atención ==================

        private void MostrarErroresReceta(Dictionary<string, string> errores)
        {
            string m;
            if (errores.TryGetValue("Medicamento", out m)) Estilo.MostrarErrorCampo(_errMedicamento, m);
            if (errores.TryGetValue("Cantidad", out m)) Estilo.MostrarErrorCampo(_errCantidad, m);
        }

        private void BtnGuardar_Click(object sender, EventArgs e)
        {
            GuardarAtencion(false);
        }

        /// <summary>
        /// Guarda la atención. Si el medicamento coincide con una alergia activa, el servicio lo rechaza con
        /// Campo = "Alergia": aquí se pide confirmación explícita y, solo si el profesional acepta, se reintenta confirmando.
        /// </summary>
        private void GuardarAtencion(bool confirmaAlergia)
        {
            Estilo.MostrarErrorCampo(_errMotivo, null);
            Estilo.MostrarErrorCampo(_errDiagnostico, null);
            Estilo.MostrarErrorCampo(_errCie10, null);
            Estilo.MostrarErrorCampo(_errMedicamento, null);
            Estilo.MostrarErrorCampo(_errCantidad, null);
            _signos.LimpiarErrores();

            // Receta opcional: medicamento y cantidad van juntos o ninguno.
            Medicamento elegido = MedicamentoElegido();
            int? idMedicamento = elegido == null ? (int?)null : elegido.IdMedicamento;
            int? cantidad = null;
            Dictionary<string, string> errores = new Dictionary<string, string>();
            if (_txtCantidad.Text.Trim().Length > 0)
            {
                int c;
                if (int.TryParse(_txtCantidad.Text.Trim(), out c)) cantidad = c;
                else errores["Cantidad"] = "Ingrese un número entero.";
            }

            Dictionary<string, string> erroresSignos = new Dictionary<string, string>();
            SignosVitales signos = _signos.Leer(erroresSignos);
            if (erroresSignos.Count == 0)
                foreach (KeyValuePair<string, string> par in _servicio.ValidarSignos(signos)) erroresSignos[par.Key] = par.Value;

            foreach (KeyValuePair<string, string> par in _servicio.ValidarAtencion(_txtMotivo.Text, _txtDiagnostico.Text))
                errores[par.Key] = par.Value;
            string errorCie = _servicio.ValidarCie10(_cuadroCie10.CodigoElegido);
            if (errorCie != null)
                errores["Cie10"] = _cuadroCie10.HayTextoSinElegir
                    ? "Elija un diagnóstico de la lista de sugerencias." : errorCie;
            if (!errores.ContainsKey("Cantidad"))
            {
                foreach (KeyValuePair<string, string> par in _servicio.ValidarReceta(idMedicamento, cantidad))
                    errores[par.Key] = par.Value;
            }

            if (errores.Count > 0 || erroresSignos.Count > 0)
            {
                string m;
                if (errores.TryGetValue("Motivo", out m)) Estilo.MostrarErrorCampo(_errMotivo, m);
                if (errores.TryGetValue("Diagnostico", out m)) Estilo.MostrarErrorCampo(_errDiagnostico, m);
                if (errores.TryGetValue("Cie10", out m)) Estilo.MostrarErrorCampo(_errCie10, m);
                MostrarErroresReceta(errores);
                _signos.MostrarErrores(erroresSignos);
                return;
            }

            try
            {
                Atencion guardada = _servicio.RegistrarAtencion(
                    _historia.IdHistoria, _txtMotivo.Text, _txtDiagnostico.Text, idMedicamento, cantidad, signos, confirmaAlergia, _cuadroCie10.CodigoElegido);
                _txtMotivo.Clear();
                _txtDiagnostico.Clear();
                _cuadroCie10.Limpiar();
                _txtCantidad.Clear();
                _signos.Limpiar();
                RecargarAtenciones();
                if (guardada.TieneReceta) CargarMedicamentos(); // el stock cambió: se actualiza la lista

                string aviso = "Atención registrada.";
                if (guardada.TieneReceta) aviso += "\nReceta: " + guardada.RecetaTexto + " (stock actualizado).";
                Estilo.MostrarExito(this, aviso);
            }
            catch (AccesoDenegadoException ex)
            {
                Estilo.MostrarError(this, ex.Message);
            }
            catch (ValidacionException ex)
            {
                ManejarValidacion(ex, confirmaAlergia);
            }
            catch (ReglaNegocioException ex)
            {
                Estilo.MostrarError(this, ex.Message);
            }
            catch (Exception)
            {
                Estilo.MostrarError(this, "No se pudo guardar la atención. Verifique la conexión con la base de datos.");
            }
        }

        private void ManejarValidacion(ValidacionException ex, bool yaConfirmo)
        {
            string campo = ex.Campo ?? string.Empty;

            if (campo == "Alergia" && !yaConfirmo)
            {
                // Advertencia explícita: por defecto el botón es "No" para que Enter no confirme por accidente.
                DialogResult r = MessageBox.Show(this,
                    ex.Message + "\n\n¿Desea recetarlo de todas formas bajo su responsabilidad?",
                    "Posible alergia al medicamento",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                if (r == DialogResult.Yes) GuardarAtencion(true);
                return;
            }

            if (campo == "Motivo") Estilo.MostrarErrorCampo(_errMotivo, ex.Message);
            else if (campo == "Diagnostico") Estilo.MostrarErrorCampo(_errDiagnostico, ex.Message);
            else if (campo == "Cie10") Estilo.MostrarErrorCampo(_errCie10, ex.Message);
            else if (campo == "Medicamento" || campo == "Cantidad")
            {
                Dictionary<string, string> uno = new Dictionary<string, string>();
                uno[campo] = ex.Message;
                MostrarErroresReceta(uno);
                try { CargarMedicamentos(); } catch (Exception) { /* la lista se refrescará al reabrir */ }
            }
            else if (_signos.TieneClave(campo))
            {
                Dictionary<string, string> uno = new Dictionary<string, string>();
                uno[campo] = ex.Message;
                _signos.MostrarErrores(uno);
            }
            else Estilo.MostrarError(this, ex.Message);
        }
    }
}
