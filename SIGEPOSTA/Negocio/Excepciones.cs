using System;

namespace SIGEPOSTA.Negocio
{
    /// <summary>
    /// Error de regla de negocio cuyo mensaje SÍ está pensado para el usuario (ej. "Stock insuficiente").
    /// Decisión de diseño: las pantallas solo muestran el texto de las excepciones propias (esta,
    /// ValidacionException y AccesoDenegadoException). Cualquier otra excepción (SQL, archivos, .NET)
    /// puede traer texto técnico o datos internos, así que se reemplaza por un mensaje amable.
    /// Hereda de InvalidOperationException para no romper el código que ya la captura.
    /// </summary>
    public class ReglaNegocioException : InvalidOperationException
    {
        public ReglaNegocioException(string mensaje) : base(mensaje) { }
    }

    /// <summary>
    /// El usuario no tiene permiso (rol) o no ha iniciado sesión. Se distingue de
    /// UnauthorizedAccessException del sistema (que ocurre, por ejemplo, al escribir en una carpeta protegida
    /// y cuyo texto incluye rutas del equipo).
    /// </summary>
    public class AccesoDenegadoException : UnauthorizedAccessException
    {
        public AccesoDenegadoException(string mensaje) : base(mensaje) { }
    }
}
