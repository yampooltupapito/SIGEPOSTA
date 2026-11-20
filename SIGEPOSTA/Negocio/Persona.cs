using System;

namespace SIGEPOSTA.Negocio
{
    /// <summary>
    /// Clase abstracta base: representa a cualquier persona del sistema.
    /// Decisión de diseño: es abstracta porque nunca se registra una "persona" genérica;
    /// siempre es un tipo concreto (Paciente hoy, y a futuro personal de salud).
    /// Concentra los datos de identidad y su validación para que las subclases no la dupliquen.
    /// </summary>
    public abstract class Persona
    {
        private string _dni;
        private string _nombres;
        private string _apellidos;
        private DateTime _fechaNacimiento;
        private string _telefono = string.Empty;

        /// <summary>Constructor vacío: lo usa la capa de Datos al reconstruir el objeto desde la BD.</summary>
        protected Persona()
        {
            _fechaNacimiento = DateTime.Today;
        }

        /// <summary>Constructor completo: valida todo a través de las propiedades (nunca se asigna a los campos directamente).</summary>
        protected Persona(string dni, string nombres, string apellidos, DateTime fechaNacimiento, string telefono)
        {
            Dni = dni;
            Nombres = nombres;
            Apellidos = apellidos;
            FechaNacimiento = fechaNacimiento;
            Telefono = telefono;
        }

        public string Dni
        {
            get { return _dni; }
            set { _dni = Validador.Dni(value); }
        }

        public string Nombres
        {
            get { return _nombres; }
            set { _nombres = Validador.TextoObligatorio(value, "Nombres", 80); }
        }

        public string Apellidos
        {
            get { return _apellidos; }
            set { _apellidos = Validador.TextoObligatorio(value, "Apellidos", 80); }
        }

        public DateTime FechaNacimiento
        {
            get { return _fechaNacimiento; }
            set { _fechaNacimiento = Validador.FechaNoFutura(value, "FechaNacimiento"); }
        }

        public string Telefono
        {
            get { return _telefono; }
            set { _telefono = Validador.Telefono(value); }
        }

        /// <summary>Nombre completo, útil para listas y reportes.</summary>
        public string NombreCompleto
        {
            get { return Apellidos + ", " + Nombres; }
        }

        /// <summary>Calcula la edad en años cumplidos a la fecha de hoy.</summary>
        public int ObtenerEdad()
        {
            DateTime hoy = DateTime.Today;
            int edad = hoy.Year - _fechaNacimiento.Year;
            // Si aún no cumple años este año, se resta uno.
            if (_fechaNacimiento.Date > hoy.AddYears(-edad)) edad--;
            return edad;
        }

        /// <summary>
        /// Virtual: las subclases pueden ampliar lo que muestran (polimorfismo).
        /// Por seguridad el DNI se muestra enmascarado en la salida de texto.
        /// </summary>
        public virtual string MostrarDatos()
        {
            string dniMasc = string.IsNullOrEmpty(_dni) ? "" : "****" + _dni.Substring(4);
            return "DNI: " + dniMasc + " | " + NombreCompleto + " | Edad: " + ObtenerEdad() + " años";
        }
    }
}
