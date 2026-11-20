using System;
using System.Drawing;
using System.Windows.Forms;
using SIGEPOSTA.Negocio;

namespace SIGEPOSTA.Presentacion
{
    /// <summary>
    /// Consulta de la auditoría (solo Administrador): filtros por fecha, usuario y acción, tabla paginada de 50 en 50
    /// y exportación a CSV. Es solo de lectura: la auditoría no se puede editar ni borrar (ni aquí ni en la base).
    /// </summary>
    public class FrmAuditoria : Form
    {
        private const string TodasLasAcciones = "(Todas)";

        private readonly AuditoriaServicio _servicio = new AuditoriaServicio();
        private int _pagina = 1;

        private DateTimePicker _dtpDesde, _dtpHasta;
        private TextBox _txtUsuario;
        private ComboBox _cboAccion;
        private Label _errDesde, _errHasta, _errUsuario, _errAccion;
        private Button _btnBuscar, _btnLimpiar, _btnExportar, _btnAnterior, _btnSiguiente;
        private Label _lblPagina;
        private DataGridView _grid;
        private ToolStripStatusLabel _lblConsulta;

        public FrmAuditoria()
        {
            // Si no es Administrador, ni siquiera se construye la pantalla (y el intento queda auditado).
            Permisos.ExigirAdministrador();
            Construir();
            Load += delegate { Buscar(1); };
        }

        private void Construir()
        {
            Text = "SIGEPOSTA - Auditoría";
            Font = Estilo.Fuente;
            BackColor = Estilo.Fondo;
            ForeColor = Estilo.Texto;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1180, 720);
            MinimumSize = new Size(960, 560);

            TableLayoutPanel raiz = new TableLayoutPanel();
            raiz.Dock = DockStyle.Fill;
            raiz.ColumnCount = 1;
            raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            raiz.RowCount = 3;
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // barra de título
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // filtros
            raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // tabla

            raiz.Controls.Add(Estilo.CrearBarraTitulo("Auditoría"), 0, 0);
            GroupBox filtros = ConstruirFiltros();
            raiz.Controls.Add(filtros, 0, 1);
            raiz.Controls.Add(ConstruirResultado(), 0, 2);
            Controls.Add(raiz);

            Controls.Add(Estilo.CrearBarraEstado(out _lblConsulta));

            _btnBuscar.Click += delegate { Buscar(1); };
            _btnLimpiar.Click += delegate
            {
                _dtpDesde.Checked = false;
                _dtpHasta.Checked = false;
                _txtUsuario.Clear();
                _cboAccion.SelectedIndex = 0;
                Buscar(1);
            };
            _btnExportar.Click += BtnExportar_Click;
            _btnAnterior.Click += delegate { Buscar(_pagina - 1); };
            _btnSiguiente.Click += delegate { Buscar(_pagina + 1); };

            Estilo.VincularEnter(this, filtros, _btnBuscar);   // Enter en los filtros = Buscar
            AcceptButton = _btnBuscar;
            Estilo.EscCierra(this);
        }

        private GroupBox ConstruirFiltros()
        {
            // Selectores con casilla: sin marcar = sin filtro por esa fecha.
            _dtpDesde = Estilo.CrearSelectorFechaVencimiento();
            _dtpHasta = Estilo.CrearSelectorFechaVencimiento();
            _txtUsuario = Estilo.CrearTextBox(false, 50);
            _cboAccion = Estilo.CrearCombo();
            _cboAccion.Items.Add(TodasLasAcciones);
            foreach (string a in AccionAuditoria.Todas) _cboAccion.Items.Add(a);
            _cboAccion.SelectedIndex = 0;

            _btnBuscar = Estilo.CrearBotonPrimario("Buscar");
            _btnLimpiar = Estilo.CrearBotonSecundario("Limpiar filtros");
            _btnExportar = Estilo.CrearBotonSecundario("Exportar a CSV");
            foreach (Button b in new[] { _btnBuscar, _btnLimpiar, _btnExportar })
            {
                b.Dock = DockStyle.None;
                b.Width = 150;
                b.Margin = new Padding(Estilo.EspacioCampos, 0, 0, 0);
            }

            // Fila 1: los cuatro filtros en columnas del mismo ancho. Fila 2: los botones juntos, alineados a la derecha.
            TableLayoutPanel cuadricula = new TableLayoutPanel();
            cuadricula.ColumnCount = 4;
            for (int i = 0; i < 4; i++) cuadricula.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            cuadricula.RowCount = 2;
            cuadricula.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            cuadricula.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            cuadricula.Dock = DockStyle.Fill;
            cuadricula.AutoSize = true;
            cuadricula.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            cuadricula.Margin = Padding.Empty;

            TableLayoutPanel c1 = Estilo.CrearCampoConError("Desde", _dtpDesde, out _errDesde);
            TableLayoutPanel c2 = Estilo.CrearCampoConError("Hasta", _dtpHasta, out _errHasta);
            TableLayoutPanel c3 = Estilo.CrearCampoConError("Usuario", _txtUsuario, out _errUsuario);
            TableLayoutPanel c4 = Estilo.CrearCampoConError("Acción", _cboAccion, out _errAccion);
            c1.Margin = new Padding(0, 0, Estilo.EspacioCampos, Estilo.EspacioCampos);
            c2.Margin = new Padding(0, 0, Estilo.EspacioCampos, Estilo.EspacioCampos);
            c3.Margin = new Padding(0, 0, Estilo.EspacioCampos, Estilo.EspacioCampos);
            c4.Margin = new Padding(0, 0, 0, Estilo.EspacioCampos);
            cuadricula.Controls.Add(c1, 0, 0);
            cuadricula.Controls.Add(c2, 1, 0);
            cuadricula.Controls.Add(c3, 2, 0);
            cuadricula.Controls.Add(c4, 3, 0);

            Label ayuda = Estilo.CrearTexto(
                "Marque la casilla de una fecha para usarla. El usuario se busca por el inicio de su nombre.", 520);
            ayuda.Font = Estilo.FuentePequena;
            ayuda.ForeColor = Estilo.Gris;
            ayuda.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            ayuda.Margin = new Padding(0, 0, 0, 6);

            // Los botones se agregan al revés porque el flujo va de derecha a izquierda: se ve Buscar · Limpiar · Exportar.
            FlowLayoutPanel botones = new FlowLayoutPanel();
            botones.FlowDirection = FlowDirection.RightToLeft;
            botones.WrapContents = false;
            botones.AutoSize = true;
            botones.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            botones.Dock = DockStyle.Fill;
            botones.Margin = Padding.Empty;
            botones.Controls.Add(_btnExportar);
            botones.Controls.Add(_btnLimpiar);
            botones.Controls.Add(_btnBuscar);

            TableLayoutPanel pie = new TableLayoutPanel();
            pie.ColumnCount = 2;
            pie.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            pie.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pie.RowCount = 1;
            pie.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            pie.Dock = DockStyle.Fill;
            pie.AutoSize = true;
            pie.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            pie.Margin = Padding.Empty;
            pie.Controls.Add(ayuda, 0, 0);
            pie.Controls.Add(botones, 1, 0);
            cuadricula.Controls.Add(pie, 0, 1);
            cuadricula.SetColumnSpan(pie, 4);

            GroupBox g = Estilo.CrearRecuadro("Filtros");
            TableLayoutPanel fila = cuadricula;
            g.Controls.Add(fila);
            return g;
        }

        private GroupBox ConstruirResultado()
        {
            _grid = new DataGridView();
            Estilo.EstilizarGrid(_grid);
            _grid.Columns.Add("Fecha", "Fecha");
            _grid.Columns.Add("Usuario", "Usuario");
            _grid.Columns.Add("Accion", "Acción");
            _grid.Columns.Add("Entidad", "Entidad");
            _grid.Columns.Add("IdEntidad", "Id");
            _grid.Columns.Add("Detalle", "Detalle");
            _grid.Columns.Add("Resultado", "Resultado");
            _grid.Columns["Fecha"].FillWeight = 15;
            _grid.Columns["Usuario"].FillWeight = 11;
            _grid.Columns["Accion"].FillWeight = 19;
            _grid.Columns["Entidad"].FillWeight = 10;
            _grid.Columns["IdEntidad"].FillWeight = 6;
            _grid.Columns["Detalle"].FillWeight = 29;
            _grid.Columns["Resultado"].FillWeight = 10;

            Button anterior, siguiente;
            TableLayoutPanel paginador = Estilo.CrearPaginador(out anterior, out siguiente, out _lblPagina);
            _btnAnterior = anterior;
            _btnSiguiente = siguiente;

            TableLayoutPanel contenido = new TableLayoutPanel();
            contenido.ColumnCount = 1;
            contenido.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            contenido.RowCount = 2;
            contenido.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            contenido.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            contenido.Dock = DockStyle.Fill;
            contenido.Margin = Padding.Empty;
            _grid.Dock = DockStyle.Fill;
            contenido.Controls.Add(_grid, 0, 0);
            contenido.Controls.Add(paginador, 0, 1);

            GroupBox g = Estilo.CrearRecuadro("Registros (solo lectura)");
            g.AutoSize = false;
            g.Margin = new Padding(Estilo.MargenInterior, 0, Estilo.MargenInterior, Estilo.MargenInterior);
            g.Controls.Add(contenido);
            return g;
        }

        // ================== Acciones ==================

        private FiltroAuditoria LeerFiltro()
        {
            FiltroAuditoria f = new FiltroAuditoria();
            if (_dtpDesde.Checked) f.Desde = _dtpDesde.Value.Date;
            if (_dtpHasta.Checked) f.Hasta = _dtpHasta.Value.Date;
            f.Usuario = _txtUsuario.Text.Trim();
            string accion = _cboAccion.SelectedItem as string;
            f.Accion = (accion == null || accion == TodasLasAcciones) ? null : accion;
            return f;
        }

        private void Buscar(int pagina)
        {
            Estilo.MostrarErrorCampo(_errDesde, null);
            try
            {
                ResultadoPagina<RegistroAuditoria> r = _servicio.Buscar(LeerFiltro(), pagina);
                _pagina = r.Pagina;
                Estilo.MostrarConsulta(_lblConsulta, r.Milisegundos);

                _grid.Rows.Clear();
                foreach (RegistroAuditoria a in r.Items)
                {
                    int i = _grid.Rows.Add(
                        a.Fecha.ToString("dd/MM/yyyy HH:mm:ss"),
                        a.NombreUsuario ?? "—",
                        a.Accion,
                        a.Entidad ?? "—",
                        a.IdEntidad.HasValue ? a.IdEntidad.Value.ToString() : "—",
                        a.Detalle ?? string.Empty,
                        a.Resultado);
                    if (a.Resultado != AuditoriaServicio.Ok)
                        _grid.Rows[i].DefaultCellStyle.ForeColor = Estilo.Error;   // fallos, denegados y cancelados en rojo
                }
                _grid.ClearSelection();

                _lblPagina.Text = r.TextoPagina;
                _btnAnterior.Enabled = r.HayAnterior;
                _btnSiguiente.Enabled = r.HaySiguiente;
            }
            catch (ValidacionException ex)
            {
                Estilo.MostrarErrorCampo(_errDesde, ex.Message);
            }
            catch (AccesoDenegadoException ex)
            {
                Estilo.MostrarError(this, ex.Message);
                Close();
            }
            catch (Exception)
            {
                Estilo.MostrarError(this, "No se pudo consultar la auditoría. Verifique la conexión con la base de datos.");
            }
        }

        private void BtnExportar_Click(object sender, EventArgs e)
        {
            try
            {
                using (SaveFileDialog dlg = new SaveFileDialog())
                {
                    dlg.Title = "Exportar auditoría a CSV";
                    dlg.Filter = "Archivo CSV (*.csv)|*.csv";
                    dlg.DefaultExt = "csv";
                    dlg.AddExtension = true;
                    dlg.OverwritePrompt = true;
                    dlg.FileName = "Auditoria_" + DateTime.Now.ToString("yyyyMMdd_HHmm");
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;

                    Cursor = Cursors.WaitCursor;
                    ReporteTabla tabla = _servicio.Exportar(LeerFiltro());
                    tabla.GuardarCsv(dlg.FileName);
                    Estilo.MostrarExito(this, "Auditoría exportada: " + tabla.Resumen + ".");
                }
            }
            catch (ValidacionException ex)
            {
                Estilo.MostrarErrorCampo(_errDesde, ex.Message);
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
                Estilo.MostrarError(this, "No se pudo exportar la auditoría. Verifique la conexión y que tenga permiso sobre esa carpeta.");
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }
    }
}
