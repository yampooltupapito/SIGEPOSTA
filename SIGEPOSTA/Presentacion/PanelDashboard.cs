using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using SIGEPOSTA.Negocio;
using SIGEPOSTA.Seguridad;
// Alias para no mezclar System.Windows.Forms.Label con System.Windows.Forms.DataVisualization.Charting.Label.
using Graficos = System.Windows.Forms.DataVisualization.Charting;

namespace SIGEPOSTA.Presentacion
{
    /// <summary>
    /// Panel de inicio (dashboard): saludo, tarjetas de indicadores, gráficos y alertas de inventario.
    /// Decisiones de diseño:
    ///  - Es un UserControl que vive DENTRO de FrmPrincipal: el menú sigue disponible y el panel se muestra
    ///    apenas se inicia sesión.
    ///  - La pantalla solo DIBUJA lo que DashboardServicio le entrega. Qué bloques se ven por rol lo decide el
    ///    servicio (validando permisos en Negocio); aquí solo se arman los bloques que ese rol puede ver, para
    ///    no dejar huecos vacíos.
    ///  - La carga corre en un BackgroundWorker (la ventana no se congela) y se repite sola cada 60 segundos.
    ///  - Si falla una consulta, se muestra un mensaje amable en el propio panel (sin ventanas emergentes cada
    ///    minuto) y el detalle técnico va solo al registro de errores.
    /// </summary>
    public class PanelDashboard : UserControl
    {
        private const int SegundosActualizacion = 60;

        private readonly DashboardServicio _servicio = new DashboardServicio();
        private readonly BackgroundWorker _worker = new BackgroundWorker();
        private readonly Timer _reloj = new Timer();

        private readonly bool _verPacientes;   // solo para armar la pantalla; el permiso real lo valida DashboardServicio
        private readonly bool _verInventario;

        private TarjetaKpi _kpiPacientes, _kpiHoy, _kpiMes, _kpiStockBajo, _kpiPorVencer, _kpiVencidos;
        private Label _lblSaludo, _lblFecha, _lblEstado, _lblMensaje, _lblSinAlertas;
        private Button _btnActualizar;
        private Graficos.Chart _chartMes, _chartDias;
        private Graficos.Title _vacioMes, _vacioDias;
        private DataGridView _grid;

        private bool _teniaError;
        private string _codigoError = string.Empty;

        /// <summary>Se produce al hacer clic en una fila de "Alertas de inventario" (FrmPrincipal abre Inventario).</summary>
        public event EventHandler SolicitaInventario;

        public PanelDashboard()
        {
            _verPacientes = Permisos.PuedeAccederPacientes;
            _verInventario = Permisos.PuedeAccederInventario;

            Dock = DockStyle.Fill;
            BackColor = Estilo.Fondo;
            Font = Estilo.Fuente;
            ForeColor = Estilo.Texto;
            AutoScroll = true;
            // Si la ventana es más chica que esto, aparece desplazamiento en vez de aplastar las tarjetas.
            AutoScrollMinSize = new Size(760, 285 + (_verPacientes ? 250 : 0) + (_verInventario ? 250 : 0));

            Construir();

            _worker.DoWork += delegate (object s, DoWorkEventArgs e) { e.Result = _servicio.Cargar(); };
            _worker.RunWorkerCompleted += Worker_Completed;

            _reloj.Interval = SegundosActualizacion * 1000;
            _reloj.Tick += delegate { Actualizar(); };

            Load += delegate
            {
                ActualizarSaludo();
                Actualizar();
                _reloj.Start();
            };
        }

        // ================== Construcción de la pantalla ==================

        private void Construir()
        {
            int filas = 2 + (_verPacientes ? 1 : 0) + (_verInventario ? 1 : 0);
            TableLayoutPanel raiz = new TableLayoutPanel();
            raiz.Dock = DockStyle.Fill;
            raiz.ColumnCount = 1;
            raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            raiz.Padding = new Padding(Estilo.MargenInterior);
            raiz.RowCount = filas;
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // saludo
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // tarjetas
            float porcentaje = (_verPacientes && _verInventario) ? 55f : 100f;
            if (_verPacientes) raiz.RowStyles.Add(new RowStyle(SizeType.Percent, porcentaje));
            if (_verInventario) raiz.RowStyles.Add(new RowStyle(SizeType.Percent, (_verPacientes && _verInventario) ? 45f : 100f));

            int fila = 0;
            raiz.Controls.Add(ConstruirEncabezado(), 0, fila++);
            raiz.Controls.Add(ConstruirTarjetas(), 0, fila++);
            if (_verPacientes) raiz.Controls.Add(ConstruirGraficos(), 0, fila++);
            if (_verInventario) raiz.Controls.Add(ConstruirAlertas(), 0, fila++);
            Controls.Add(raiz);
        }

        /// <summary>Tarjeta blanca con el saludo y la fecha, y a la derecha el estado de carga y el botón Actualizar.</summary>
        private Control ConstruirEncabezado()
        {
            _lblSaludo = new Label();
            _lblSaludo.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
            _lblSaludo.ForeColor = Estilo.Morado;
            _lblSaludo.AutoSize = true;
            _lblSaludo.Margin = Padding.Empty;

            _lblFecha = new Label();
            _lblFecha.Font = Estilo.Fuente;
            _lblFecha.ForeColor = Estilo.Gris;
            _lblFecha.AutoSize = true;
            _lblFecha.Margin = new Padding(0, Estilo.EspacioEtiqueta, 0, 0);

            _lblMensaje = new Label();
            _lblMensaje.Font = Estilo.FuentePequena;
            _lblMensaje.ForeColor = Estilo.Error;
            _lblMensaje.AutoSize = true;
            _lblMensaje.MaximumSize = new Size(760, 0);
            _lblMensaje.Margin = new Padding(0, Estilo.EspacioEtiqueta, 0, 0);

            TableLayoutPanel izquierda = new TableLayoutPanel();
            izquierda.ColumnCount = 1;
            izquierda.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            izquierda.Dock = DockStyle.Fill;
            izquierda.AutoSize = true;
            izquierda.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            izquierda.Margin = Padding.Empty;
            Estilo.AgregarFila(izquierda, _lblSaludo);
            Estilo.AgregarFila(izquierda, _lblFecha);
            Estilo.AgregarFila(izquierda, _lblMensaje);

            _lblEstado = new Label();
            _lblEstado.Font = Estilo.FuentePequena;
            _lblEstado.ForeColor = Estilo.Gris;
            _lblEstado.AutoSize = true;
            _lblEstado.Anchor = AnchorStyles.Right;
            _lblEstado.Margin = new Padding(0, 0, 0, Estilo.EspacioEtiqueta);

            _btnActualizar = Estilo.CrearBotonSecundario("Actualizar");
            _btnActualizar.Dock = DockStyle.None;
            _btnActualizar.Anchor = AnchorStyles.Right;
            _btnActualizar.Width = 130;
            _btnActualizar.Click += delegate { Actualizar(); };

            TableLayoutPanel derecha = new TableLayoutPanel();
            derecha.ColumnCount = 1;
            derecha.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            derecha.Dock = DockStyle.Fill;
            derecha.AutoSize = true;
            derecha.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            derecha.Margin = new Padding(Estilo.MargenInterior, 0, 0, 0);
            Estilo.AgregarFila(derecha, _lblEstado);
            Estilo.AgregarFila(derecha, _btnActualizar);

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
            contenido.Controls.Add(izquierda, 0, 0);
            contenido.Controls.Add(derecha, 1, 0);

            Panel linea = new Panel();            // línea fina dorada de detalle (igual que la barra de título)
            linea.BackColor = Estilo.Dorado;
            linea.Dock = DockStyle.Bottom;
            linea.Height = Estilo.GrosorLinea;

            Panel tarjeta = new Panel();
            tarjeta.BackColor = Estilo.Recuadro;
            tarjeta.Dock = DockStyle.Fill;
            tarjeta.AutoSize = true;
            tarjeta.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tarjeta.Padding = new Padding(Estilo.MargenInterior, Estilo.MargenInterior, Estilo.MargenInterior,
                                          Estilo.MargenInterior + Estilo.GrosorLinea);
            tarjeta.Margin = new Padding(0, 0, 0, Estilo.EspacioCampos);
            tarjeta.Controls.Add(contenido);
            tarjeta.Controls.Add(linea);
            return tarjeta;
        }

        /// <summary>Fila de tarjetas de indicadores: solo las del rol, repartidas en columnas iguales con 12 px entre ellas.</summary>
        private Control ConstruirTarjetas()
        {
            List<TarjetaKpi> tarjetas = new List<TarjetaKpi>();
            if (_verPacientes)
            {
                _kpiPacientes = new TarjetaKpi("Pacientes registrados (activos)");
                _kpiHoy = new TarjetaKpi("Atenciones de hoy");
                _kpiMes = new TarjetaKpi("Atenciones del mes");
                tarjetas.AddRange(new[] { _kpiPacientes, _kpiHoy, _kpiMes });
            }
            if (_verInventario)
            {
                _kpiStockBajo = new TarjetaKpi("Medicamentos con stock bajo");
                _kpiPorVencer = new TarjetaKpi("Medicamentos por vencer (30 días o menos)");
                _kpiVencidos = new TarjetaKpi("Medicamentos vencidos");
                tarjetas.AddRange(new[] { _kpiStockBajo, _kpiPorVencer, _kpiVencidos });
            }

            TableLayoutPanel t = new TableLayoutPanel();
            t.ColumnCount = tarjetas.Count;
            t.RowCount = 1;
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Dock = DockStyle.Fill;
            t.AutoSize = true;
            t.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            t.Margin = new Padding(0, 0, 0, Estilo.EspacioCampos);
            for (int i = 0; i < tarjetas.Count; i++)
            {
                t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / tarjetas.Count));
                tarjetas[i].Margin = new Padding(0, 0, i < tarjetas.Count - 1 ? Estilo.EspacioCampos : 0, 0);
                t.Controls.Add(tarjetas[i], i, 0);
            }
            return t;
        }

        private Control ConstruirGraficos()
        {
            _chartMes = CrearGrafico(Graficos.SeriesChartType.Column, Estilo.Rojo, out _vacioMes);
            _chartDias = CrearGrafico(Graficos.SeriesChartType.Line, Estilo.Morado, out _vacioDias);

            GroupBox gMes = Estilo.CrearRecuadro("Atenciones por día (este mes)");
            gMes.AutoSize = false;
            gMes.Margin = new Padding(0, 0, Estilo.EspacioCampos / 2, Estilo.EspacioCampos);
            gMes.Controls.Add(_chartMes);

            GroupBox gDias = Estilo.CrearRecuadro("Atenciones de los últimos 7 días");
            gDias.AutoSize = false;
            gDias.Margin = new Padding(Estilo.EspacioCampos / 2, 0, 0, Estilo.EspacioCampos);
            gDias.Controls.Add(_chartDias);

            TableLayoutPanel t = new TableLayoutPanel();
            t.ColumnCount = 2;
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            t.RowCount = 1;
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            t.Dock = DockStyle.Fill;
            t.Margin = Padding.Empty;
            t.Controls.Add(gMes, 0, 0);
            t.Controls.Add(gDias, 1, 0);
            return t;
        }

        /// <summary>Gráfico sobrio: sin leyenda, sin cuadrícula vertical, eje Y desde cero y con números enteros.</summary>
        private static Graficos.Chart CrearGrafico(Graficos.SeriesChartType tipo, Color color, out Graficos.Title tituloVacio)
        {
            Graficos.Chart c = new Graficos.Chart();
            c.Dock = DockStyle.Fill;
            c.BackColor = Estilo.Recuadro;
            c.Font = Estilo.FuentePequena;

            Graficos.ChartArea area = new Graficos.ChartArea("area");
            area.BackColor = Estilo.Recuadro;
            area.AxisX.MajorGrid.Enabled = false;
            area.AxisX.Interval = 1;
            area.AxisX.LineColor = Estilo.Borde;
            area.AxisX.LabelStyle.Font = Estilo.FuentePequena;
            area.AxisX.LabelStyle.ForeColor = Estilo.Texto;
            area.AxisY.Minimum = 0;
            area.AxisY.MajorGrid.LineColor = Estilo.Borde;
            area.AxisY.LineColor = Estilo.Borde;
            area.AxisY.LabelStyle.Font = Estilo.FuentePequena;
            area.AxisY.LabelStyle.ForeColor = Estilo.Gris;
            area.AxisY.LabelStyle.Format = "0";
            area.AxisY.Title = "N.º de atenciones";
            area.AxisY.TitleFont = Estilo.FuentePequena;
            area.AxisY.TitleForeColor = Estilo.Gris;
            c.ChartAreas.Add(area);

            Graficos.Series s = new Graficos.Series("Atenciones");
            s.ChartType = tipo;
            s.Color = color;
            s.IsValueShownAsLabel = true;
            s.Font = Estilo.FuentePequena;
            s.LabelForeColor = Estilo.Texto;
            if (tipo == Graficos.SeriesChartType.Column)
            {
                s["PointWidth"] = "0.6";
            }
            else
            {
                s.BorderWidth = 3;
                s.MarkerStyle = Graficos.MarkerStyle.Circle;
                s.MarkerSize = 9;
                s.MarkerColor = Estilo.Dorado;
                s.MarkerBorderColor = Estilo.Morado;
            }
            c.Series.Add(s);

            // Mensaje cuando no hay datos (se muestra/oculta al actualizar).
            tituloVacio = new Graficos.Title("Sin atenciones en este período", Graficos.Docking.Top, Estilo.FuentePequena, Estilo.Gris);
            tituloVacio.Visible = false;
            c.Titles.Add(tituloVacio);
            return c;
        }

        private Control ConstruirAlertas()
        {
            _grid = new DataGridView();
            Estilo.EstilizarGrid(_grid);
            _grid.Columns.Add("Medicamento", "Medicamento");
            _grid.Columns.Add("Stock", "Stock");
            _grid.Columns.Add("Alerta", "Alerta");
            _grid.Columns["Medicamento"].FillWeight = 55;
            _grid.Columns["Stock"].FillWeight = 12;
            _grid.Columns["Alerta"].FillWeight = 33;
            _grid.Columns["Stock"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _grid.Cursor = Cursors.Hand;
            _grid.CellClick += delegate (object s, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0) return;
                EventHandler h = SolicitaInventario;
                if (h != null) h(this, EventArgs.Empty);
            };

            _lblSinAlertas = new Label();
            _lblSinAlertas.Font = Estilo.FuenteNegrita;
            _lblSinAlertas.ForeColor = Estilo.Exito;
            _lblSinAlertas.Dock = DockStyle.Bottom;
            _lblSinAlertas.Height = 30;
            _lblSinAlertas.TextAlign = ContentAlignment.MiddleLeft;
            _lblSinAlertas.Visible = false;

            GroupBox g = Estilo.CrearRecuadro("Alertas de inventario (haga clic en una fila para abrir Inventario)");
            g.AutoSize = false;
            g.Margin = Padding.Empty;
            g.Controls.Add(_grid);          // Fill primero; lo siguiente se acopla antes
            g.Controls.Add(_lblSinAlertas);
            return g;
        }

        // ================== Carga ==================

        private void ActualizarSaludo()
        {
            Usuario u = Sesion.UsuarioActual;
            if (u == null) return;

            string primerNombre = u.NombreCompleto.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? u.NombreCompleto;
            string rol = u.Rol == Rol.Medico ? "Médico" : u.Rol.ToString();
            _lblSaludo.Text = "Bienvenido, " + primerNombre + " — " + rol;

            CultureInfo cultura;
            try { cultura = new CultureInfo("es-PE"); }
            catch (CultureNotFoundException) { cultura = CultureInfo.CurrentCulture; }
            string fecha = DateTime.Today.ToString("dddd, d 'de' MMMM 'de' yyyy", cultura);
            _lblFecha.Text = Establecimiento.Nombre + "  ·  " + char.ToUpper(fecha[0], cultura) + fecha.Substring(1);
        }

        /// <summary>Vuelve a cargar los datos en segundo plano. Si ya hay una carga en curso, no hace nada.</summary>
        public void Actualizar()
        {
            if (IsDisposed || _worker.IsBusy) return;
            if (!Sesion.EstaAutenticado) return; // la sesión se cerró (ej. por inactividad)

            ActualizarSaludo();
            _lblEstado.ForeColor = Estilo.Morado;
            _lblEstado.Text = "Cargando…";
            _btnActualizar.Enabled = false;
            _worker.RunWorkerAsync();
        }

        private void Worker_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            if (IsDisposed || !IsHandleCreated) return; // el panel ya se cerró (cierre de sesión)
            _btnActualizar.Enabled = true;

            if (e.Error != null)
            {
                _lblEstado.ForeColor = Estilo.Gris;
                _lblEstado.Text = "Sin actualizar";
                if (e.Error is AccesoDenegadoException)
                {
                    _lblMensaje.Text = e.Error.Message;
                }
                else
                {
                    string codigo = ManejadorErrores.Registrar(e.Error);
                    _lblMensaje.Text = "No se pudo actualizar el panel. Verifique la conexión con la base de datos y pulse Actualizar. " +
                                       "(Código de referencia: " + codigo + ")";
                }
                return;
            }

            Aplicar((DatosDashboard)e.Result);
        }

        /// <summary>Dibuja los datos recibidos. Un bloque que falló muestra "—" y un aviso, sin afectar al otro.</summary>
        private void Aplicar(DatosDashboard d)
        {
            // ----- Pacientes y atenciones -----
            if (d.VePacientes && _kpiPacientes != null)
            {
                bool ok = !d.ErrorPacientes;
                _kpiPacientes.Mostrar(ok ? (int?)d.PacientesActivos : null, NivelKpi.Normal);
                _kpiHoy.Mostrar(ok ? (int?)d.AtencionesHoy : null, NivelKpi.Normal);
                _kpiMes.Mostrar(ok ? (int?)d.AtencionesMes : null, NivelKpi.Normal);
                LlenarGrafico(_chartMes, _vacioMes, ok ? d.AtencionesPorDiaDelMes : new List<PuntoDato>(),
                              ok ? "Sin atenciones en este período" : "No se pudo cargar este gráfico");
                LlenarGrafico(_chartDias, _vacioDias, ok ? d.AtencionesUltimos7Dias : new List<PuntoDato>(),
                              ok ? "Sin atenciones en este período" : "No se pudo cargar este gráfico");
            }

            // ----- Inventario: las tarjetas pasan a ámbar o rojo cuando el número es mayor que cero -----
            if (d.VeInventario && _kpiStockBajo != null)
            {
                bool ok = !d.ErrorInventario;
                _kpiStockBajo.Mostrar(ok ? (int?)d.StockBajo : null, ok && d.StockBajo > 0 ? NivelKpi.Ambar : NivelKpi.Normal);
                _kpiPorVencer.Mostrar(ok ? (int?)d.PorVencer : null, ok && d.PorVencer > 0 ? NivelKpi.Ambar : NivelKpi.Normal);
                _kpiVencidos.Mostrar(ok ? (int?)d.Vencidos : null, ok && d.Vencidos > 0 ? NivelKpi.Rojo : NivelKpi.Normal);
                LlenarAlertas(ok ? d.Alertas : new List<Medicamento>(), ok);
            }

            // ----- Estado y mensaje -----
            if (d.HuboErrores)
            {
                if (!_teniaError && d.ErrorTecnico != null) _codigoError = ManejadorErrores.Registrar(d.ErrorTecnico);
                string que = (d.ErrorPacientes && d.ErrorInventario) ? "pacientes e inventario"
                           : d.ErrorPacientes ? "pacientes y atenciones" : "inventario";
                _lblMensaje.Text = "No se pudieron cargar los datos de " + que + ". Verifique la conexión con la base de datos y pulse Actualizar." +
                                   (string.IsNullOrEmpty(_codigoError) ? string.Empty : " (Código de referencia: " + _codigoError + ")");
            }
            else
            {
                _lblMensaje.Text = string.Empty;
            }
            _teniaError = d.HuboErrores;

            _lblEstado.ForeColor = Estilo.Gris;
            _lblEstado.Text = "Actualizado a las " + d.ActualizadoEn.ToString("HH:mm:ss") + " · Consulta en " + d.Milisegundos.ToString("N0") + " ms";
        }

        private static void LlenarGrafico(Graficos.Chart grafico, Graficos.Title tituloVacio, List<PuntoDato> puntos, string textoVacio)
        {
            Graficos.Series serie = grafico.Series[0];
            serie.Points.Clear();
            foreach (PuntoDato p in puntos) serie.Points.AddXY(p.Etiqueta, p.Valor);
            serie.IsValueShownAsLabel = puntos.Count <= 12;   // con muchas barras (mes) los números se leen con el cursor
            serie.ToolTip = "#AXISLABEL: #VAL atención(es)";

            int maximo = puntos.Count == 0 ? 0 : puntos.Max(p => p.Valor);
            Graficos.Axis ejeY = grafico.ChartAreas[0].AxisY;
            ejeY.Interval = maximo <= 5 ? 1 : 0;           // con valores chicos, solo números enteros en el eje
            ejeY.Maximum = maximo == 0 ? 1 : double.NaN;    // sin datos, un eje corto en vez de uno vacío

            bool hayDatos = puntos.Any(p => p.Valor > 0);
            tituloVacio.Text = textoVacio;
            tituloVacio.Visible = !hayDatos;
        }

        private void LlenarAlertas(List<Medicamento> alertas, bool cargadoOk)
        {
            _grid.Rows.Clear();
            foreach (Medicamento m in alertas)
            {
                int i = _grid.Rows.Add(
                    m.NombreCompleto,
                    m.StockTexto, InventarioServicio.TextoAlerta(m));
                _grid.Rows[i].Tag = m;
                Estilo.PintarFilaAlerta(_grid.Rows[i], m);   // rojo = vencido, ámbar = stock bajo o por vencer
            }
            _grid.ClearSelection();

            _lblSinAlertas.Visible = alertas.Count == 0;
            if (alertas.Count == 0)
            {
                _lblSinAlertas.ForeColor = cargadoOk ? Estilo.Exito : Estilo.Gris;
                _lblSinAlertas.Text = cargadoOk ? "Sin alertas: el inventario está en orden." : "No se pudo cargar este bloque.";
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _reloj.Stop();
                _reloj.Dispose();
                _worker.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
