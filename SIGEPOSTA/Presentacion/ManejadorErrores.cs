using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace SIGEPOSTA.Presentacion
{
    /// <summary>
    /// Manejador global de errores inesperados (Application.ThreadException y AppDomain.UnhandledException).
    /// Decisiones de diseño:
    ///  - El usuario NUNCA ve el texto técnico de la excepción: solo un mensaje amable y un código corto.
    ///  - El detalle técnico va a un archivo de registro local con ese mismo código, para que quien da
    ///    soporte pueda encontrarlo. Se registra el TIPO y la traza de la pila, pero NO el Message de la
    ///    excepción, porque puede incluir datos (valores de SQL, rutas, nombres).
    ///  - Todo está protegido con try/catch: el manejador de errores no puede generar otro error.
    /// </summary>
    public static class ManejadorErrores
    {
        private static bool _mostrando;

        public static void Instalar()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate (object s, System.Threading.ThreadExceptionEventArgs e)
            {
                Atender(e.Exception, false);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate (object s, UnhandledExceptionEventArgs e)
            {
                Atender(e.ExceptionObject as Exception, e.IsTerminating);
            };
        }

        /// <summary>Guarda el detalle técnico en el registro y devuelve un código corto para el usuario.</summary>
        public static string Registrar(Exception ex)
        {
            string codigo = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
            try
            {
                string carpeta = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SIGEPOSTA");
                Directory.CreateDirectory(carpeta);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] Código " + codigo);
                int nivel = 0;
                for (Exception actual = ex; actual != null && nivel < 5; actual = actual.InnerException, nivel++)
                {
                    sb.AppendLine("  Tipo: " + actual.GetType().FullName);   // sin Message: puede traer datos
                    sb.AppendLine(actual.StackTrace ?? "  (sin traza)");
                }
                sb.AppendLine();
                File.AppendAllText(Path.Combine(carpeta, "errores.log"), sb.ToString(), Encoding.UTF8);
            }
            catch (Exception)
            {
                // Si ni siquiera se puede escribir el registro, se sigue: el usuario igual recibe el código.
            }
            return codigo;
        }

        private static void Atender(Exception ex, bool terminando)
        {
            if (_mostrando) return; // evita ventanas en cascada si el propio aviso falla
            _mostrando = true;
            try
            {
                string codigo = Registrar(ex ?? new Exception("Error desconocido"));
                string mensaje =
                    "Ocurrió un problema inesperado y la operación no se completó.\n" +
                    "Sus datos ya guardados no se han perdido.\n\n" +
                    (terminando ? "La aplicación debe cerrarse. Vuelva a abrirla.\n\n" : string.Empty) +
                    "Si el problema continúa, avise al administrador e indique este código: " + codigo;
                MessageBox.Show(mensaje, "SIGEPOSTA", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception)
            {
                // Nada más que hacer: el manejador de errores nunca debe lanzar otro error.
            }
            finally
            {
                _mostrando = false;
            }
        }
    }
}
