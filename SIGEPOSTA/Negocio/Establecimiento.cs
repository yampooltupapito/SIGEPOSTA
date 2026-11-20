using System;
using SIGEPOSTA.Datos;

namespace SIGEPOSTA.Negocio
{
    /// <summary>
    /// Configuración del establecimiento. SIGEPOSTA se diseñó para UN solo establecimiento: la Posta Jesús Nazareno.
    /// Decisiones de diseño:
    ///  - El nombre es una constante (se muestra en el login, la ventana principal y el dashboard).
    ///  - El IdPosta se consulta UNA sola vez a la tabla Posta y se guarda en memoria; todo lo nuevo
    ///    (pacientes y medicamentos) se registra automáticamente con ese IdPosta, así el usuario ya no elige posta.
    ///  - La tabla Posta y la columna IdPosta se conservan en la base de datos: la estructura queda lista para
    ///    crecer a una red de postas (bastaría con volver a ofrecer la elección de posta en las pantallas).
    /// </summary>
    public static class Establecimiento
    {
        public const string Nombre = "Posta Jesús Nazareno";

        private static readonly object Candado = new object();
        private static int _idPosta; // 0 = todavía no se ha leído de la base de datos

        /// <summary>IdPosta del establecimiento. Se consulta la primera vez que se necesita y luego se reutiliza.</summary>
        public static int IdPosta
        {
            get
            {
                Inicializar();
                return _idPosta;
            }
        }

        public static bool EstaInicializado
        {
            get { return _idPosta > 0; }
        }

        /// <summary>
        /// Lee el IdPosta de la tabla Posta (una sola vez). Se llama al iniciar la aplicación; si falla la
        /// conexión no se guarda nada y se reintentará en el siguiente uso.
        /// </summary>
        public static void Inicializar()
        {
            Inicializar(new EstablecimientoDatos());
        }

        public static void Inicializar(EstablecimientoDatos datos)
        {
            if (_idPosta > 0) return;
            lock (Candado)
            {
                if (_idPosta > 0) return;
                int id = datos.ObtenerIdPosta();
                if (id <= 0)
                    throw new ReglaNegocioException(
                        "No se encontró el establecimiento en la base de datos (la tabla Posta está vacía). Avise al administrador.");
                _idPosta = id;
            }
        }
    }
}
