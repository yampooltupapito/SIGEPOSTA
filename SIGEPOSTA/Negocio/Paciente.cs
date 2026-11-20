using System;

namespace SIGEPOSTA.Negocio
{
    /// <summary>
    /// Paciente del puesto de salud. Hereda la identidad de Persona y añade datos propios.
    /// Su historia clínica se maneja como objeto aparte (HistoriaClinica) enlazado por IdPaciente.
    /// Decisión de diseño sobre el DNI: en la base se guarda cifrado (DniCifrado) y como HMAC (DniHash).
    /// Al listar pacientes NO se descifra: la propiedad Dni queda vacía hasta que el servicio la
    /// llene a pedido, para que el DNI en claro exista en memoria el menor tiempo posible.
    /// </summary>
    public class Paciente : Persona
    {
        private string _direccion = string.Empty;

        /// <summary>Id en la base de datos (0 = aún no guardado).</summary>
        public int IdPaciente { get; set; }

        public DateTime FechaRegistro { get; set; }

        /// <summary>Posta donde se atiende (0 = sin asignar).</summary>
        public int IdPosta { get; set; }

        /// <summary>Nombre de la posta, solo para mostrar en listas.</summary>
        public string PostaNombre { get; set; }

        public bool Activo { get; set; }

        /// <summary>DNI cifrado con AES (bytes tal como se guardan en la BD).</summary>
        public byte[] DniCifrado { get; set; }

        /// <summary>HMAC-SHA256 del DNI (64 caracteres hex): sirve para buscar y evitar duplicados.</summary>
        public string DniHash { get; set; }

        /// <summary>Datos de su historia clínica, solo para mostrar en listas.</summary>
        public int IdHistoria { get; set; }
        public string NumeroHistoria { get; set; }

        public string Direccion
        {
            get { return _direccion; }
            set { _direccion = Validador.TextoOpcional(value, "Direccion", 150); }
        }

        /// <summary>Constructor vacío para la capa de Datos.</summary>
        public Paciente() : base()
        {
            FechaRegistro = DateTime.Now;
            Activo = true;
        }

        /// <summary>Constructor para registrar un paciente nuevo desde un formulario.</summary>
        public Paciente(string dni, string nombres, string apellidos, DateTime fechaNacimiento, string telefono, string direccion)
            : base(dni, nombres, apellidos, fechaNacimiento, telefono)
        {
            Direccion = direccion;
            FechaRegistro = DateTime.Now;
            Activo = true;
        }

        /// <summary>Constructor completo para reconstruir un paciente ya guardado.</summary>
        public Paciente(int idPaciente, string dni, string nombres, string apellidos, DateTime fechaNacimiento,
                        string telefono, string direccion, DateTime fechaRegistro)
            : this(dni, nombres, apellidos, fechaNacimiento, telefono, direccion)
        {
            IdPaciente = idPaciente;
            FechaRegistro = fechaRegistro;
        }

        public override string MostrarDatos()
        {
            string baseTxt = base.MostrarDatos();
            return baseTxt + (string.IsNullOrEmpty(Direccion) ? "" : " | Dir.: " + Direccion);
        }
    }
}
