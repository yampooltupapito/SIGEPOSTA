using System;

namespace SIGEPOSTA.Negocio
{
    /// <summary>
    /// Error de validación asociado a un campo concreto (ParamName / Campo).
    /// Decisión de diseño: ArgumentException agrega por defecto "Nombre del parámetro: x" al Message;
    /// aquí se sobrescribe Message para devolver SOLO el texto pensado para el usuario, y la
    /// pantalla usa Campo para mostrar el error justo debajo del cuadro correspondiente.
    /// </summary>
    public class ValidacionException : ArgumentException
    {
        private readonly string _mensaje;

        public ValidacionException(string mensaje, string campo) : base(mensaje, campo)
        {
            _mensaje = mensaje;
        }

        public string Campo
        {
            get { return ParamName; }
        }

        public override string Message
        {
            get { return _mensaje; }
        }
    }
}
