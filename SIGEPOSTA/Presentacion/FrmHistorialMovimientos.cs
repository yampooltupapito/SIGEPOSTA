using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using SIGEPOSTA.Negocio;

namespace SIGEPOSTA.Presentacion
{
    /// <summary>
    /// Historial (kardex) de movimientos de un medicamento: fecha, tipo, cantidad, motivo y quién lo hizo.
    /// Los ingresos se muestran en verde y las salidas en rojo.
    /// </summary>
    public class FrmHistorialMovimientos : Form
    {
        private readonly InventarioServicio _servicio;
        private readonly Medicamento _medicamento;
        private DataGridView _grid;
        private Button _btnCerrar;
        private ToolStripStatusLabel _lblConsulta;

        public FrmHistorialMovimientos(InventarioServicio servicio, Medicamento medicamento)
        {
            if (servicio == null) throw new ArgumentNullException("servicio");
            if (medicamento == null) throw new ArgumentNullException("medicamento");
            Permisos.ExigirAccesoInventario();

            _servicio = servicio;
            _medicamento = medicamento;
            Construir();
            Load += FrmHistorialMovimientos_Load;
        }

        private void Construir()
        {
            Text = "SIGEPOSTA - Movimientos";
            Font = Estilo.Fuente;
            BackColor = Estilo.Fondo;
            ForeColor = Estilo.Texto;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1100, 560);
            MinimumSize = new Size(900, 420);

            _grid = new DataGridView();
            Estilo.EstilizarGrid(_grid);
            _grid.Columns.Add("Fecha", "Fecha");
            _grid.Columns.Add("Tipo", "Tipo");
            _grid.Columns.Add("Cantidad", "Cantidad");
            _grid.Columns.Add("Proveedor", "Proveedor");
            _grid.Columns.Add("Documento", "Guía / factura");
            _grid.Columns.Add("Motivo", "Motivo");
            _grid.Columns.Add("Usuario", "Usuario");
            _grid.Columns["Fecha"].FillWeight = 15;
            _grid.Columns["Tipo"].FillWeight = 9;
            _grid.Columns["Cantidad"].FillWeight = 9;
            _grid.Columns["Proveedor"].FillWeight = 22;
            _grid.Columns["Documento"].FillWeight = 13;
            _grid.Columns["Motivo"].FillWeight = 18;
            _grid.Columns["Usuario"].FillWeight = 14;
            _grid.Columns["Cantidad"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

            string resumen = _medicamento.NombreCompleto + " — stock actual: " + _medicamento.StockTexto;
            GroupBox recuadro = Estilo.CrearRecuadro("Movimientos de " + resumen);
            recuadro.AutoSize = false;
            recuadro.Margin = new Padding(Estilo.MargenInterior, Estilo.MargenInterior, Estilo.MargenInterior, Estilo.EspacioCampos);
            recuadro.Controls.Add(_grid);

            _btnCerrar = Estilo.CrearBotonSecundario("Cerrar");
            _btnCerrar.Dock = DockStyle.None;
            _btnCerrar.Anchor = AnchorStyles.Right;
            _btnCerrar.Width = 150;
            _btnCerrar.Margin = new Padding(Estilo.MargenInterior, 0, Estilo.MargenInterior, Estilo.MargenInterior);
            _btnCerrar.Click += delegate { Close(); };
            CancelButton = _btnCerrar;   // Esc cierra
            AcceptButton = _btnCerrar;   // Enter también cierra (ventana solo de consulta)

            TableLayoutPanel raiz = new TableLayoutPanel();
            raiz.Dock = DockStyle.Fill;
            raiz.ColumnCount = 1;
            raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            raiz.RowCount = 3;
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            raiz.Controls.Add(Estilo.CrearBarraTitulo("Historial de movimientos"), 0, 0);
            raiz.Controls.Add(recuadro, 0, 1);
            raiz.Controls.Add(_btnCerrar, 0, 2);
            Controls.Add(raiz);

            // Barra de estado: "Consulta en X ms" (se agrega después del contenido para acoplarse abajo).
            Controls.Add(Estilo.CrearBarraEstado(out _lblConsulta));
        }

        private void FrmHistorialMovimientos_Load(object sender, EventArgs e)
        {
            try
            {
                Stopwatch reloj = Stopwatch.StartNew();
                List<MovimientoInventario> lista = _servicio.ListarMovimientos(_medicamento.IdMedicamento);
                reloj.Stop();
                Estilo.MostrarConsulta(_lblConsulta, reloj.ElapsedMilliseconds);
                _grid.Rows.Clear();
                foreach (MovimientoInventario mv in lista)
                {
                    int i = _grid.Rows.Add(mv.Fecha.ToString("dd/MM/yyyy HH:mm"), mv.Tipo.ToString(), Medicamento.FormatoCantidad(mv.Cantidad, _medicamento.UnidadNombre),
                        mv.NombreProveedor, mv.NumeroDocumento, mv.Motivo, mv.NombreUsuario);
                    _grid.Rows[i].Cells["Tipo"].Style.ForeColor = mv.Tipo == TipoMovimiento.Ingreso ? Estilo.Exito : Estilo.Error;
                    _grid.Rows[i].Cells["Tipo"].Style.Font = Estilo.FuenteNegrita;
                }
                _grid.ClearSelection();
            }
            catch (AccesoDenegadoException ex)
            {
                Estilo.MostrarError(this, ex.Message);
                Close();
            }
            catch (Exception)
            {
                Estilo.MostrarError(this, "No se pudo cargar el historial. Verifique la conexión con la base de datos.");
            }
        }

		private void InitializeComponent()
		{
			this.SuspendLayout();
			// 
			// FrmHistorialMovimientos
			// 
			this.ClientSize = new System.Drawing.Size(282, 253);
			this.Name = "FrmHistorialMovimientos";
			this.Load += new System.EventHandler(this.FrmHistorialMovimientos_Load_1);
			this.ResumeLayout(false);

		}

		private void FrmHistorialMovimientos_Load_1(object sender, EventArgs e)
		{

		}
	}
}
