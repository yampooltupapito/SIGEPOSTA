using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SIGEPOSTA.Negocio;
using SIGEPOSTA.Seguridad;

namespace SIGEPOSTA.Presentacion
{
    /// <summary>
    /// Ventana principal con el menú. Las opciones se ocultan según el rol, pero además cada clic
    /// vuelve a validar el permiso en la capa de Negocio (Permisos.Exigir...), porque ocultar no basta.
    /// Cierra la sesión sola tras 10 minutos sin actividad y tiene el botón "Cerrar sesión" siempre
    /// visible en la barra roja.
    /// </summary>
    public class FrmPrincipal : Form
    {
        private MenuStrip _menu;
        private ToolStripMenuItem _mnuPacientes, _mnuInventario, _mnuFormas, _mnuProveedores, _mnuCie10, _mnuReportes, _mnuRespaldo, _mnuUsuarios, _mnuAuditoria;
        private MonitorInactividad _monitor;
        private PanelDashboard _dashboard;

        /// <summary>True si la sesión terminó (por botón o por inactividad) y hay que volver al login.</summary>
        public bool CerrarSesionSolicitado { get; private set; }

        /// <summary>Texto para mostrar en el login cuando la sesión se cerró sola.</summary>
        public string MotivoCierre { get; private set; }

        public FrmPrincipal()
        {
            Construir();
            Load += delegate { IniciarMonitor(); };
            FormClosed += delegate
            {
                DetenerMonitor();
                // Cerrar la ventana con "Salir" también termina la sesión (si no se registró ya el cierre).
                if (!CerrarSesionSolicitado && Sesion.EstaAutenticado)
                    AuditoriaServicio.Registrar(AccionAuditoria.CierreSesion, "Usuario", Sesion.UsuarioActual.IdUsuario,
                        "Salida del programa", AuditoriaServicio.Ok);
            };
        }

        private void Construir()
        {
            Text = "SIGEPOSTA — " + Establecimiento.Nombre;
            Font = Estilo.Fuente;
            BackColor = Estilo.Fondo;
            ForeColor = Estilo.Texto;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1100, Math.Min(840, Screen.PrimaryScreen.WorkingArea.Height - 40));
            MinimumSize = new Size(760, 520);

            Usuario u = Sesion.UsuarioActual;
            string quien = u == null ? "Sin sesión" : u.NombreCompleto + "  ·  " + u.Rol;

            // --- Menú ---
            _menu = new MenuStrip();
            _menu.Dock = DockStyle.Top;
            _menu.BackColor = Estilo.Recuadro;
            _menu.ForeColor = Estilo.Texto;
            _menu.Font = Estilo.Fuente;

            _mnuPacientes = new ToolStripMenuItem("Pacientes");
            _mnuInventario = new ToolStripMenuItem("Inventario");
            _mnuFormas = new ToolStripMenuItem("Formas y unidades");
            _mnuProveedores = new ToolStripMenuItem("Proveedores");
            _mnuCie10 = new ToolStripMenuItem("CIE-10");
            _mnuReportes = new ToolStripMenuItem("Reportes");
            _mnuRespaldo = new ToolStripMenuItem("Respaldo");
            _mnuUsuarios = new ToolStripMenuItem("Usuarios");
            _mnuAuditoria = new ToolStripMenuItem("Auditoría");
            ToolStripMenuItem mnuSesion = new ToolStripMenuItem("Sesión");
            ToolStripMenuItem mnuCerrar = new ToolStripMenuItem("Cerrar sesión");
            ToolStripMenuItem mnuSalir = new ToolStripMenuItem("Salir");
            mnuSesion.DropDownItems.Add(mnuCerrar);
            mnuSesion.DropDownItems.Add(mnuSalir);

            _menu.Items.Add(_mnuPacientes);
            _menu.Items.Add(_mnuInventario);
            _menu.Items.Add(_mnuFormas);
            _menu.Items.Add(_mnuProveedores);
            _menu.Items.Add(_mnuCie10);
            _menu.Items.Add(_mnuReportes);
            _menu.Items.Add(_mnuRespaldo);
            _menu.Items.Add(_mnuUsuarios);
            _menu.Items.Add(_mnuAuditoria);
            _menu.Items.Add(mnuSesion);
            MainMenuStrip = _menu;

            // Visibilidad por rol (la validación real está en Permisos, en cada clic).
            _mnuPacientes.Visible = Permisos.PuedeAccederPacientes;
            _mnuInventario.Visible = Permisos.PuedeAccederInventario;
            _mnuFormas.Visible = Permisos.PuedeAccederCatalogosMedicamento;
            _mnuProveedores.Visible = Permisos.PuedeAccederProveedores;
            _mnuCie10.Visible = Permisos.EsAdministrador;
            _mnuReportes.Visible = Permisos.PuedeVerReportes;
            _mnuRespaldo.Visible = Permisos.PuedeAccederPacientes;   // Administrador y Médico
            _mnuUsuarios.Visible = Permisos.EsAdministrador;
            _mnuAuditoria.Visible = Permisos.EsAdministrador;

            _mnuPacientes.Click += delegate { Abrir(Permisos.ExigirAccesoPacientes, () => new FrmPacientes()); };
            _mnuInventario.Click += delegate { Abrir(Permisos.ExigirAccesoInventario, () => new FrmInventario()); };
            _mnuFormas.Click += delegate { Abrir(Permisos.ExigirAccesoCatalogosMedicamento, () => new FrmFormasUnidades()); };
            _mnuProveedores.Click += delegate { Abrir(Permisos.ExigirAccesoProveedores, () => new FrmProveedores()); };
            _mnuCie10.Click += delegate { Abrir(Permisos.ExigirAdministrador, () => new FrmCie10()); };
            _mnuReportes.Click += delegate { Abrir(Permisos.ExigirAccesoReportes, () => new FrmReportes()); };
            _mnuRespaldo.Click += delegate { Abrir(Permisos.ExigirAccesoPacientes, () => new FrmRespaldo()); };
            _mnuUsuarios.Click += delegate { Abrir(Permisos.ExigirAdministrador, () => new FrmUsuarios()); };
            _mnuAuditoria.Click += delegate { Abrir(Permisos.ExigirAdministrador, () => new FrmAuditoria()); };
            mnuCerrar.Click += delegate { CerrarSesion(null); };
            mnuSalir.Click += delegate { Close(); };

            // --- Barra de título roja con el usuario y el botón "Cerrar sesión" ---
            Panel barra = Estilo.CrearBarraTitulo(Establecimiento.Nombre + "   |   " + quien);
            barra.Dock = DockStyle.Top;
            Estilo.AgregarBotonABarra(barra, "Cerrar sesión", delegate { CerrarSesion(null); });

            // --- Cuerpo ---
            // El panel de inicio (dashboard) es lo primero que se ve al entrar; el menú sigue disponible arriba.
            Panel cuerpo = new Panel();
            cuerpo.Dock = DockStyle.Fill;

            _dashboard = new PanelDashboard();
            _dashboard.SolicitaInventario += delegate { Abrir(Permisos.ExigirAccesoInventario, () => new FrmInventario()); };
            cuerpo.Controls.Add(_dashboard);

            // Orden de agregado: lo último queda más arriba (menú, luego barra, luego cuerpo).
            Controls.Add(cuerpo);
            Controls.Add(barra);
            Controls.Add(_menu);
        }

        /// <summary>
        /// Abre una ventana como diálogo. Primero se valida el permiso en Negocio; si el rol no
        /// corresponde, se avisa y no se construye la ventana.
        /// </summary>
        private void Abrir(Action exigirPermiso, Func<Form> crear)
        {
            try
            {
                exigirPermiso();
                using (Form f = crear())
                {
                    f.ShowDialog(this);
                }
                // Lo que se hizo en la ventana (pacientes, atenciones, recetas, stock) se refleja en el panel de inicio.
                if (_dashboard != null && !_dashboard.IsDisposed) _dashboard.Actualizar();
            }
            catch (AccesoDenegadoException ex)
            {
                Estilo.MostrarError(this, ex.Message);
            }
        }

        // ================== Sesión ==================

        private void CerrarSesion(string motivo)
        {
            MotivoCierre = motivo;
            CerrarSesionSolicitado = true;
            // Se audita ANTES de cerrar la sesión (después ya no se sabría quién era).
            if (Sesion.EstaAutenticado)
                AuditoriaServicio.Registrar(AccionAuditoria.CierreSesion, "Usuario", Sesion.UsuarioActual.IdUsuario,
                    motivo == null ? "Cierre voluntario" : "Cierre por inactividad", AuditoriaServicio.Ok);
            Sesion.Cerrar();
            Close();
        }

        private void IniciarMonitor()
        {
            _monitor = new MonitorInactividad();
            _monitor.InactividadAgotada += delegate { CerrarPorInactividad(); };
            _monitor.Iniciar();
        }

        private void DetenerMonitor()
        {
            if (_monitor != null)
            {
                _monitor.Dispose();
                _monitor = null;
            }
        }

        /// <summary>Cierra la sesión, cierra cualquier ventana abierta encima y vuelve al login con un aviso.</summary>
        private void CerrarPorInactividad()
        {
            // Se audita antes de invalidar la sesión (después ya no se sabría quién era) y luego se invalida YA mismo:
            // ninguna ventana abierta puede seguir operando.
            Usuario quien = Sesion.UsuarioActual;
            if (quien != null)
                AuditoriaServicio.Registrar(AccionAuditoria.CierreSesion, "Usuario", quien.IdUsuario,
                    "Cierre por inactividad", AuditoriaServicio.Ok);
            Sesion.Cerrar();
            foreach (Form f in Application.OpenForms.Cast<Form>().Where(x => x != this).ToList())
            {
                f.Close();
            }
            CerrarSesion("Su sesión se cerró por " + (int)MonitorInactividad.LimitePorDefecto.TotalMinutes +
                         " minutos sin actividad. Inicie sesión de nuevo.");
        }
    }
}
