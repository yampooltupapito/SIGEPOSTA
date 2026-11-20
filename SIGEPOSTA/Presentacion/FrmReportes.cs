using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using SIGEPOSTA.Negocio;

namespace SIGEPOSTA.Presentacion
{
    /// <summary>
    /// Reportes con exportación a CSV. La lista de reportes solo incluye los que el rol puede ver
    /// (Administrador: todos; Médico: pacientes; Farmacia: inventario), y además cada método de
    /// ReporteServicio vuelve a validar el permiso.
    /// </summary>
    public class FrmReportes : Form
    {
        private enum TipoReporte
        {
            PacientesPorDia,
            DiagnosticosFrecuentes,
            MedicamentosCriticos,
            MovimientosInventario
        }

        /// <summary>Elemento de la lista desplegable (texto visible + tipo de reporte).</summary>
        private class OpcionReporte
        {
            public TipoReporte Tipo;
            public string Texto;
            public bool UsaFechas;
            public override string ToString() { return Texto; }
        }

        private readonly ReporteServicio _servicio = new ReporteServicio();
        private ReporteTabla _actual;

        private ComboBox _cboReporte;
        private DateTimePicker _dtpDesde, _dtpHasta;
        private Label _errReporte, _errDesde, _errHasta, _lblResumen;
        private Button _btnGenerar, _btnExportar;
        private ToolStripStatusLabel _lblConsulta;
        private DataGridView _grid;

        public FrmReportes()
        {
            Permisos.ExigirAccesoReportes();
            Construir();
            Load += FrmReportes_Load;
        }

        // ================== Construcción de la pantalla ==================

        private void Construir()
        {
            Text = "SIGEPOSTA - Reportes";
            Font = Estilo.Fuente;
            BackColor = Estilo.Fondo;
            ForeColor = Estilo.Texto;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1100, 720);
            MinimumSize = new Size(900, 560);

            TableLayoutPanel raiz = new TableLayoutPanel();
            raiz.Dock = DockStyle.Fill;
            raiz.ColumnCount = 1;
            raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            raiz.RowCount = 3;
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // barra de título
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // filtros
            raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // resultado

            raiz.Controls.Add(Estilo.CrearBarraTitulo("Reportes"), 0, 0);
            GroupBox filtros = ConstruirFiltros();
            raiz.Controls.Add(filtros, 0, 1);
            raiz.Controls.Add(ConstruirResultado(), 0, 2);
            Controls.Add(raiz);

            // Barra de estado: "Consulta en X ms" (se agrega después del contenido para acoplarse abajo).
            Controls.Add(Estilo.CrearBarraEstado(out _lblConsulta));

            _cboReporte.SelectedIndexChanged += delegate { AlCambiarReporte(); };
            _btnGenerar.Click += BtnGenerar_Click;
            _btnExportar.Click += BtnExportar_Click;

            Estilo.VincularEnter(this, filtros, _btnGenerar);
            AcceptButton = _btnGenerar;
            Estilo.EscCierra(this);
        }

        private GroupBox ConstruirFiltros()
        {
            _cboReporte = Estilo.CrearCombo();
            _dtpDesde = Estilo.CrearSelectorFecha();
            _dtpHasta = Estilo.CrearSelectorFecha();
            _dtpDesde.Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            _btnGenerar = Estilo.CrearBotonPrimario("Generar");
            _btnExportar = Estilo.CrearBotonSecundario("Exportar a CSV");
            _btnExportar.Enabled = false;
            foreach (Button b in new[] { _btnGenerar, _btnExportar })
            {
                b.Dock = DockStyle.None;
                b.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                b.Width = 160;
                b.Margin = new Padding(Estilo.EspacioCampos, 0, 0, Estilo.EspacioCampos);
            }

            // Reporte 40 % | Desde 15 % | Hasta 15 % | Generar | Exportar
            TableLayoutPanel fila = new TableLayoutPanel();
            fila.ColumnCount = 5;
            fila.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            fila.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            fila.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            fila.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            fila.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            fila.RowCount = 1;
            fila.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            fila.Dock = DockStyle.Fill;
            fila.AutoSize = true;
            fila.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            fila.Margin = Padding.Empty;

            TableLayoutPanel c1 = Estilo.CrearCampoConError("Reporte", _cboReporte, out _errReporte);
            TableLayoutPanel c2 = Estilo.CrearCampoConError("Desde", _dtpDesde, out _errDesde);
            TableLayoutPanel c3 = Estilo.CrearCampoConError("Hasta", _dtpHasta, out _errHasta);
            c1.Margin = new Padding(0, 0, Estilo.EspacioCampos, Estilo.EspacioCampos);
            c2.Margin = new Padding(0, 0, Estilo.EspacioCampos, Estilo.EspacioCampos);
            c3.Margin = new Padding(0, 0, 0, Estilo.EspacioCampos);
            fila.Controls.Add(c1, 0, 0);
            fila.Controls.Add(c2, 1, 0);
            fila.Controls.Add(c3, 2, 0);
            fila.Controls.Add(_btnGenerar, 3, 0);
            fila.Controls.Add(_btnExportar, 4, 0);

            GroupBox g = Estilo.CrearRecuadro("Reporte");
            g.Controls.Add(fila);
            return g;
        }

        private GroupBox ConstruirResultado()
        {
            _grid = new DataGridView();
            Estilo.EstilizarGrid(_grid);

            _lblResumen = Estilo.CrearTexto("Elija un reporte y pulse Generar.", 900);
            _lblResumen.Margin = new Padding(0, Estilo.EspacioCampos, 0, 0);

            TableLayoutPanel contenido = Estilo.CrearColumna();
            contenido.AutoSize = false;
            contenido.RowCount = 2;
            contenido.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            contenido.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            contenido.Controls.Add(_grid, 0, 0);
            contenido.Controls.Add(_lblResumen, 0, 1);

            GroupBox g = Estilo.CrearRecuadro("Resultado");
            g.AutoSize = false; // el resultado ocupa todo el espacio que sobra
            g.Margin = new Padding(Estilo.MargenInterior, 0, Estilo.MargenInterior, Estilo.MargenInterior);
            g.Controls.Add(contenido);
            return g;
        }

        // ================== Lógica de pantalla ==================

        private void FrmReportes_Load(object sender, EventArgs e)
        {
            // Solo se ofrecen los reportes que el rol puede ver.
            if (Permisos.PuedeAccederPacientes)
                _cboReporte.Items.Add(new OpcionReporte
                {
                    Tipo = TipoReporte.PacientesPorDia,
                    Texto = "Pacientes atendidos por día",
                    UsaFechas = true
                });
            if (Permisos.PuedeAccederPacientes)   // Administrador y Médico
                _cboReporte.Items.Add(new OpcionReporte
                {
                    Tipo = TipoReporte.DiagnosticosFrecuentes,
                    Texto = "Diagnósticos más frecuentes (CIE-10)",
                    UsaFechas = true
                });
            if (Permisos.PuedeAccederInventario)
            {
                _cboReporte.Items.Add(new OpcionReporte
                {
                    Tipo = TipoReporte.MedicamentosCriticos,
                    Texto = "Medicamentos con stock crítico o por vencer",
                    UsaFechas = false
                });
                _cboReporte.Items.Add(new OpcionReporte
                {
                    Tipo = TipoReporte.MovimientosInventario,
                    Texto = "Movimientos de inventario por fecha",
                    UsaFechas = true
                });
            }
            if (_cboReporte.Items.Count > 0) _cboReporte.SelectedIndex = 0;
        }

        private OpcionReporte Elegido()
        {
            return _cboReporte.SelectedItem as OpcionReporte;
        }

        private void AlCambiarReporte()
        {
            OpcionReporte o = Elegido();
            bool fechas = o != null && o.UsaFechas;
            _dtpDesde.Enabled = fechas;
            _dtpHasta.Enabled = fechas;
            LimpiarErrores();
        }

        private void LimpiarErrores()
        {
            Estilo.MostrarErrorCampo(_errReporte, null);
            Estilo.MostrarErrorCampo(_errDesde, null);
            Estilo.MostrarErrorCampo(_errHasta, null);
        }

        private void BtnGenerar_Click(object sender, EventArgs e)
        {
            LimpiarErrores();
            OpcionReporte o = Elegido();
            if (o == null)
            {
                Estilo.MostrarErrorCampo(_errReporte, "Seleccione un reporte.");
                return;
            }
            if (o.UsaFechas && _dtpDesde.Value.Date > _dtpHasta.Value.Date)
            {
                Estilo.MostrarErrorCampo(_errDesde, "'Desde' no puede ser posterior a 'Hasta'.");
                return;
            }

            Cursor = Cursors.WaitCursor;
            try
            {
                Stopwatch reloj = Stopwatch.StartNew();
                ReporteTabla t;
                switch (o.Tipo)
                {
                    case TipoReporte.PacientesPorDia:
                        t = _servicio.PacientesAtendidosPorDia(_dtpDesde.Value, _dtpHasta.Value);
                        break;
                    case TipoReporte.DiagnosticosFrecuentes:
                        t = _servicio.DiagnosticosMasFrecuentes(_dtpDesde.Value, _dtpHasta.Value);
                        break;
                    case TipoReporte.MedicamentosCriticos:
                        t = _servicio.MedicamentosCriticos();
                        break;
                    default:
                        t = _servicio.MovimientosPorFecha(_dtpDesde.Value, _dtpHasta.Value);
                        break;
                }
                reloj.Stop();
                Mostrar(t);
                Estilo.MostrarConsulta(_lblConsulta, reloj.ElapsedMilliseconds);
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
                Estilo.MostrarError(this, "No se pudo generar el reporte. Verifique la conexión con la base de datos.");
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void Mostrar(ReporteTabla t)
        {
            _actual = t;
            _grid.Columns.Clear();
            for (int i = 0; i < t.Columnas.Count; i++)
                _grid.Columns.Add("c" + i, t.Columnas[i]);
            foreach (string[] fila in t.Filas)
                _grid.Rows.Add((object[])fila);
            _grid.ClearSelection();

            _lblResumen.Text = t.Resumen;
            _btnExportar.Enabled = true;
        }

        private void BtnExportar_Click(object sender, EventArgs e)
        {
            if (_actual == null) return;

            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Title = "Exportar a CSV";
                dlg.Filter = "Archivo CSV (*.csv)|*.csv";
                dlg.DefaultExt = "csv";
                dlg.AddExtension = true;
                dlg.OverwritePrompt = true;
                dlg.FileName = "Reporte_" + DateTime.Now.ToString("yyyyMMdd_HHmm");
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    _actual.GuardarCsv(dlg.FileName);
                    Estilo.MostrarExito(this, "Reporte exportado correctamente.");
                }
                catch (ReglaNegocioException ex)
                {
                    Estilo.MostrarError(this, ex.Message);
                }
                catch (Exception)
                {
                    // Errores de disco, permisos o archivo en uso: se explica sin texto técnico ni rutas.
                    Estilo.MostrarError(this, "No se pudo guardar el archivo. Verifique que tenga permiso sobre esa carpeta y que el archivo no esté abierto en otro programa.");
                }
            }
        }
    }
}
