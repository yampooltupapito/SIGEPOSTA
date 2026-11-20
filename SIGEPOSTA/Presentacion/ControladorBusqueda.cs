using System;
using System.ComponentModel;
using System.Windows.Forms;
using SIGEPOSTA.Negocio;

namespace SIGEPOSTA.Presentacion
{
    /// <summary>
    /// Búsqueda "mientras se escribe" con paginación, reutilizada por Pacientes e Inventario.
    /// Decisiones de diseño:
    ///  - ESPERA de 300 ms (Timer): cada tecla reinicia la cuenta; solo se consulta cuando el usuario hace una
    ///    pausa. Escribir "quispe" no lanza 6 consultas sino una.
    ///  - La consulta corre en un BackgroundWorker: la ventana no se congela.
    ///  - Si el usuario sigue escribiendo mientras una consulta está en curso, la anterior se CANCELA (se avisa al
    ///    servicio, que la abandona entre el COUNT y la página) y su resultado se descarta; apenas termina, se lanza
    ///    la búsqueda con el texto más reciente. Así nunca se muestra un resultado "viejo".
    ///  - El worker NO toca controles: el texto, la página y el filtro se leen en el hilo de la pantalla y se le
    ///    entregan como una copia; los resultados se muestran en RunWorkerCompleted (hilo de la pantalla).
    /// </summary>
    public sealed class ControladorBusqueda<T> : IDisposable where T : class
    {
        public const int EsperaMilisegundos = 300;

        private class Peticion
        {
            public string Texto;
            public int Pagina;
            public object Filtro;
        }

        private readonly Func<string, int, object, Func<bool>, ResultadoPagina<T>> _consulta;
        private readonly Action<ResultadoPagina<T>> _alMostrar;
        private readonly Action<Exception> _alFallar;
        private readonly Action<bool> _alCambiarEstado;

        private readonly Timer _espera = new Timer();
        private readonly BackgroundWorker _worker = new BackgroundWorker();

        private string _texto = string.Empty;
        private int _pagina = 1;
        private bool _pendiente;
        private bool _liberado;

        /// <summary>Filtro adicional que se le pasa a la consulta (ej. "solo críticos"). Se copia al lanzar la búsqueda.</summary>
        public object Filtro { get; set; }

        /// <param name="consulta">Se ejecuta en el hilo del worker: (texto, página, filtro, cancelado) → resultado (null si se canceló).</param>
        /// <param name="alMostrar">Muestra el resultado (hilo de la pantalla).</param>
        /// <param name="alFallar">Informa un error (hilo de la pantalla).</param>
        /// <param name="alCambiarEstado">true cuando empieza a buscar, false cuando termina (hilo de la pantalla).</param>
        public ControladorBusqueda(Func<string, int, object, Func<bool>, ResultadoPagina<T>> consulta,
                                   Action<ResultadoPagina<T>> alMostrar, Action<Exception> alFallar, Action<bool> alCambiarEstado)
        {
            if (consulta == null) throw new ArgumentNullException("consulta");
            _consulta = consulta;
            _alMostrar = alMostrar;
            _alFallar = alFallar;
            _alCambiarEstado = alCambiarEstado;

            _espera.Interval = EsperaMilisegundos;
            _espera.Tick += delegate { _espera.Stop(); Lanzar(); };

            _worker.WorkerSupportsCancellation = true;
            _worker.DoWork += Worker_DoWork;
            _worker.RunWorkerCompleted += Worker_Completed;
        }

        /// <summary>El usuario cambió el texto: vuelve a la página 1 y espera 300 ms de pausa antes de consultar.</summary>
        public void TextoCambio(string texto)
        {
            _texto = texto ?? string.Empty;
            _pagina = 1;
            _espera.Stop();
            _espera.Start();
        }

        /// <summary>Busca de inmediato (sin esperar) con el texto actual, desde la página 1 (ej. al cambiar un filtro).</summary>
        public void BuscarYa()
        {
            _pagina = 1;
            Lanzar();
        }

        /// <summary>Va a una página (botones Anterior / Siguiente), de inmediato.</summary>
        public void IrAPagina(int pagina)
        {
            _pagina = Math.Max(1, pagina);
            Lanzar();
        }

        /// <summary>Vuelve a consultar la página actual (ej. tras guardar o dar de baja).</summary>
        public void Recargar()
        {
            Lanzar();
        }

        private void Lanzar()
        {
            _espera.Stop();
            if (_liberado) return;

            if (_worker.IsBusy)
            {
                // Hay una consulta en curso: se cancela y, al terminar, se lanza la más reciente.
                _pendiente = true;
                _worker.CancelAsync();
                return;
            }
            Iniciar();
        }

        private void Iniciar()
        {
            _pendiente = false;
            if (_alCambiarEstado != null) _alCambiarEstado(true);
            _worker.RunWorkerAsync(new Peticion { Texto = _texto, Pagina = _pagina, Filtro = Filtro });
        }

        /// <summary>Corre en OTRO hilo: aquí no se toca ningún control.</summary>
        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            Peticion p = (Peticion)e.Argument;
            ResultadoPagina<T> r = _consulta(p.Texto, p.Pagina, p.Filtro, delegate { return _worker.CancellationPending; });
            if (r == null || _worker.CancellationPending) { e.Cancel = true; return; }
            e.Result = r;
        }

        private void Worker_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            if (_liberado) return;

            if (_pendiente)
            {
                // El resultado quedó viejo (el usuario siguió escribiendo): se descarta y se busca lo más reciente.
                Iniciar();
                return;
            }

            if (_alCambiarEstado != null) _alCambiarEstado(false);
            if (e.Error != null)
            {
                if (_alFallar != null) _alFallar(e.Error);
                return;
            }
            if (e.Cancelled) return;
            if (_alMostrar != null) _alMostrar((ResultadoPagina<T>)e.Result);
        }

        public void Dispose()
        {
            _liberado = true;
            _espera.Stop();
            _espera.Dispose();
            _worker.Dispose();
        }
    }
}
