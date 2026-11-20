using System;
using System.Windows.Forms;
using SIGEPOSTA.Negocio;
using SIGEPOSTA.Presentacion;
using SIGEPOSTA.Seguridad;

namespace SIGEPOSTA
{
	internal static class Program
	{
		/// <summary>
		/// Punto de entrada. Flujo: (si no hay usuarios) crear administrador -> login -> ventana principal.
		/// Si el usuario cierra sesión (o la sesión caduca por inactividad), se vuelve al login;
		/// si cierra la ventana principal, la aplicación termina.
		/// </summary>
		[STAThread]
		static void Main()
		{
			// El manejador global se instala ANTES de crear cualquier ventana.
			ManejadorErrores.Instalar();
			// Si la auditoría falla, la operación sigue y el detalle técnico va al registro de errores.
			AuditoriaServicio.AlFallar = delegate (Exception ex) { ManejadorErrores.Registrar(ex); };

			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);

			AutenticacionServicio servicio = new AutenticacionServicio();
			string aviso = null; // mensaje para el login (ej. "sesión cerrada por inactividad")

			try
			{
				// El IdPosta del establecimiento se lee UNA sola vez de la tabla Posta, al iniciar.
				// Si la base no responde, el mensaje de conexión de abajo se muestra aquí mismo.
				Establecimiento.Inicializar();

				while (true)
				{
					if (!servicio.ExistenUsuarios())
					{
						using (FrmCrearAdmin crear = new FrmCrearAdmin(servicio))
						{
							if (crear.ShowDialog() != DialogResult.OK) return;
						}
					}

					using (FrmLogin login = new FrmLogin(servicio, aviso))
					{
						aviso = null;
						if (login.ShowDialog() != DialogResult.OK) return;
					}

					using (FrmPrincipal principal = new FrmPrincipal())
					{
						principal.ShowDialog();
						aviso = principal.MotivoCierre;
						Sesion.Cerrar();
						if (!principal.CerrarSesionSolicitado) break;
					}
				}
			}
			catch (ReglaNegocioException ex)
			{
				// Mensaje propio (p. ej. la tabla Posta está vacía): es seguro mostrarlo.
				Estilo.MostrarError(null, ex.Message);
			}
			catch (Exception ex)
			{
				// Normalmente es un problema de conexión con SQL Server. Se muestra un texto fijo (nunca el
				// del sistema) y el detalle técnico queda solo en el registro de errores.
				string codigo = ManejadorErrores.Registrar(ex);
				Estilo.MostrarError(null,
					"No se pudo conectar con la base de datos SIGEPOSTA.\n" +
					"Verifique que SQL Server esté en ejecución y que las tablas existan.\n\n" +
					"Código de referencia: " + codigo);
			}
		}
	}
}
