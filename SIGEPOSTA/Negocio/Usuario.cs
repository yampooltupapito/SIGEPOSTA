using System;

namespace SIGEPOSTA.Negocio
{
    /// <summary>
    /// Usuario del sistema con su rol.
    /// Decisión de diseño: solo se guardan el HASH, el SALT y las iteraciones (nunca la contraseña),
    /// con el mismo formato de la tabla Usuario (VARBINARY(32), VARBINARY(16), INT).
    /// El hash lo genera/verifica la capa Seguridad; esta clase solo lo transporta.
    /// </summary>
    public class Usuario
    {
        private string _nombreUsuario;
        private string _nombreCompleto;
        private byte[] _passwordHash;
        private byte[] _salt;
        private int _iteraciones;
        private int _intentosFallidos;

        public int IdUsuario { get; set; }
        public Rol Rol { get; set; }
        /// <summary>Nombre del rol tal como viene de la tabla Rol (solo informativo).</summary>
        public string RolNombre { get; set; }
        public bool Activo { get; set; }
        public DateTime? BloqueadoHasta { get; set; }

        public string NombreUsuario
        {
            get { return _nombreUsuario; }
            set { _nombreUsuario = Validador.TextoObligatorio(value, "NombreUsuario", 50); }
        }

        public string NombreCompleto
        {
            get { return _nombreCompleto; }
            set { _nombreCompleto = Validador.TextoObligatorio(value, "NombreCompleto", 120); }
        }

        public byte[] PasswordHash
        {
            get { return _passwordHash; }
            set
            {
                if (value == null || value.Length != 32)
                    throw new ValidacionException("El hash de la contraseña debe tener 32 bytes.", "PasswordHash");
                _passwordHash = value;
            }
        }

        public byte[] Salt
        {
            get { return _salt; }
            set
            {
                if (value == null || value.Length != 16)
                    throw new ValidacionException("El salt debe tener 16 bytes.", "Salt");
                _salt = value;
            }
        }

        public int Iteraciones
        {
            get { return _iteraciones; }
            set { _iteraciones = Validador.Positivo(value, "Iteraciones"); }
        }

        public int IntentosFallidos
        {
            get { return _intentosFallidos; }
            set { _intentosFallidos = Validador.NoNegativo(value, "IntentosFallidos"); }
        }

        /// <summary>True si la cuenta está bloqueada en este momento.</summary>
        public bool EstaBloqueado
        {
            get { return BloqueadoHasta.HasValue && BloqueadoHasta.Value > DateTime.Now; }
        }

        /// <summary>Constructor vacío para la capa de Datos.</summary>
        public Usuario()
        {
            Activo = true;
        }

        /// <summary>Constructor para crear un usuario nuevo.</summary>
        public Usuario(string nombreUsuario, string nombreCompleto, byte[] passwordHash, byte[] salt, int iteraciones, Rol rol)
        {
            NombreUsuario = nombreUsuario;
            NombreCompleto = nombreCompleto;
            PasswordHash = passwordHash;
            Salt = salt;
            Iteraciones = iteraciones;
            Rol = rol;
            Activo = true;
        }

        /// <summary>Constructor completo para reconstruir un usuario guardado.</summary>
        public Usuario(int idUsuario, string nombreUsuario, string nombreCompleto, byte[] passwordHash, byte[] salt,
                       int iteraciones, Rol rol, bool activo, int intentosFallidos, DateTime? bloqueadoHasta)
            : this(nombreUsuario, nombreCompleto, passwordHash, salt, iteraciones, rol)
        {
            IdUsuario = idUsuario;
            Activo = activo;
            IntentosFallidos = intentosFallidos;
            BloqueadoHasta = bloqueadoHasta;
        }
    }
}
