using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace SIGEPOSTA.Presentacion
{
    /// <summary>
    /// Detecta inactividad de toda la aplicación (teclado y mouse, en CUALQUIER ventana abierta, incluidas
    /// las de diálogo) y avisa cuando pasa el tiempo límite (10 minutos por defecto).
    /// Decisiones de diseño:
    ///  - Es un IMessageFilter: ve los mensajes de entrada de todas las ventanas de la aplicación sin
    ///    que cada formulario tenga que avisar.
    ///  - Mide con Stopwatch (reloj monótono), no con la hora del sistema: cambiar la hora del equipo no
    ///    puede alargar ni acortar la sesión.
    ///  - Se puede PAUSAR (ej. mientras corre un respaldo largo), para no cerrar la sesión en pleno trabajo.
    /// </summary>
    public sealed class MonitorInactividad : IMessageFilter, IDisposable
    {
        public static readonly TimeSpan LimitePorDefecto = TimeSpan.FromMinutes(10);

        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_MOUSEWHEEL = 0x020A;

        private static int _pausas;

        private readonly TimeSpan _limite;
        private readonly Timer _reloj;
        private long _ultimaActividad;
        private bool _activo;

        /// <summary>Se produce una sola vez cuando se cumple el tiempo sin actividad.</summary>
        public event EventHandler InactividadAgotada;

        public MonitorInactividad() : this(LimitePorDefecto, 10000) { }

        /// <param name="limite">Tiempo sin actividad que cierra la sesión.</param>
        /// <param name="revisarCadaMs">Cada cuánto se comprueba (milisegundos).</param>
        public MonitorInactividad(TimeSpan limite, int revisarCadaMs)
        {
            _limite = limite;
            _reloj = new Timer();
            _reloj.Interval = revisarCadaMs;
            _reloj.Tick += Reloj_Tick;
        }

        public void Iniciar()
        {
            if (_activo) return;
            _activo = true;
            Registrar();
            Application.AddMessageFilter(this);
            _reloj.Start();
        }

        /// <summary>Pausa el conteo (se puede anidar). Úsese con try/finally junto a Reanudar().</summary>
        public static void Pausar()
        {
            _pausas++;
        }

        public static void Reanudar()
        {
            if (_pausas > 0) _pausas--;
        }

        public bool PreFilterMessage(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_KEYDOWN:
                case WM_SYSKEYDOWN:
                case WM_MOUSEMOVE:
                case WM_LBUTTONDOWN:
                case WM_RBUTTONDOWN:
                case WM_MBUTTONDOWN:
                case WM_MOUSEWHEEL:
                    Registrar();
                    break;
            }
            return false; // nunca "consume" el mensaje: solo lo observa
        }

        private void Registrar()
        {
            _ultimaActividad = Stopwatch.GetTimestamp();
        }

        private void Reloj_Tick(object sender, EventArgs e)
        {
            if (_pausas > 0)
            {
                Registrar(); // mientras está en pausa no se acumula inactividad
                return;
            }

            double segundos = (Stopwatch.GetTimestamp() - _ultimaActividad) / (double)Stopwatch.Frequency;
            if (segundos < _limite.TotalSeconds) return;

            _reloj.Stop();
            EventHandler h = InactividadAgotada;
            if (h != null) h(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (_activo) Application.RemoveMessageFilter(this);
            _activo = false;
            _reloj.Stop();
            _reloj.Dispose();
        }
    }
}
