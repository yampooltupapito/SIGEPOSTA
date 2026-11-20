using System;
using System.Linq;
using SIGEPOSTA.Datos;
using SIGEPOSTA.Seguridad;

namespace SIGEPOSTA.Negocio
{
    /// <summary>Resultado de un intento de login. El mensaje de fallo es siempre el mismo texto genérico.</summary>
    public class ResultadoLogin
    {
        public bool Exitoso { get; set; }
        public string Mensaje { get; set; }
        public Usuario Usuario { get; set; }
    }

    /// <summary>
    /// Reglas de autenticación.
    /// Decisiones de diseño:
    ///  - Mensaje de error genérico en TODOS los fallos (usuario inexistente, clave mala, cuenta bloqueada
    ///    o inactiva): así un atacante no puede averiguar qué usuarios existen.
    ///  - Si el usuario no existe igual se calcula un hash de mentira, para que el tiempo de respuesta
    ///    no delate si el usuario existe o no.
    ///  - Tras 5 fallos se bloquea 5 minutos; durante el bloqueo ni siquiera se verifica la contraseña.
    ///  - El administrador inicial lo crea la persona que instala el sistema: no hay clave en el código.
    /// </summary>
    public class AutenticacionServicio
    {
        public const string MensajeGenerico = "Usuario o contraseña incorrectos";
        public const int MaxIntentosFallidos = 5;
        public static readonly TimeSpan DuracionBloqueo = TimeSpan.FromMinutes(5);

        private static readonly byte[] HashFalso = new byte[32];
        private static readonly byte[] SaltFalso = new byte[16];

        private readonly UsuarioDatos _datos;

        public AutenticacionServicio() : this(new UsuarioDatos()) { }

        public AutenticacionServicio(UsuarioDatos datos)
        {
            if (datos == null) throw new ArgumentNullException("datos");
            _datos = datos;
        }

        /// <summary>True si ya hay al menos un usuario; si no, la app debe pedir crear el administrador.</summary>
        public bool ExistenUsuarios()
        {
            return _datos.ContarUsuarios() > 0;
        }

        public ResultadoLogin Login(string nombreUsuario, string contrasena)
        {
            if (string.IsNullOrWhiteSpace(nombreUsuario) || string.IsNullOrEmpty(contrasena))
                return Fallo();

            Usuario u = _datos.ObtenerPorNombre(nombreUsuario.Trim());

            if (u == null)
            {
                // Trabajo equivalente al de un usuario real (anti enumeración por tiempo).
                HashContrasena.Verificar(contrasena, HashFalso, SaltFalso, HashContrasena.Iteraciones);
                // No se guarda lo que se escribió como usuario (podría ser una contraseña escrita en el campo equivocado).
                AuditoriaServicio.RegistrarComo(null, null, AccionAuditoria.InicioSesionFallido, "Usuario", null,
                    "Usuario inexistente", AuditoriaServicio.Fallo);
                return Fallo();
            }

            if (u.EstaBloqueado)
            {
                AuditoriaServicio.RegistrarComo(u.IdUsuario, u.NombreUsuario, AccionAuditoria.InicioSesionFallido, "Usuario",
                    u.IdUsuario, "Cuenta bloqueada temporalmente", AuditoriaServicio.Fallo);
                return Fallo();
            }

            bool correcta = HashContrasena.Verificar(contrasena, u.PasswordHash, u.Salt, u.Iteraciones);

            if (!correcta)
            {
                int intentos = _datos.RegistrarIntentoFallido(u.IdUsuario);
                AuditoriaServicio.RegistrarComo(u.IdUsuario, u.NombreUsuario, AccionAuditoria.InicioSesionFallido, "Usuario",
                    u.IdUsuario, "Contraseña incorrecta (intento " + intentos + " de " + MaxIntentosFallidos + ")",
                    AuditoriaServicio.Fallo);
                if (intentos >= MaxIntentosFallidos)
                {
                    _datos.BloquearHasta(u.IdUsuario, DateTime.Now.Add(DuracionBloqueo));
                    AuditoriaServicio.RegistrarComo(u.IdUsuario, u.NombreUsuario, AccionAuditoria.BloqueoUsuario, "Usuario",
                        u.IdUsuario, "Bloqueado " + (int)DuracionBloqueo.TotalMinutes + " minutos por intentos fallidos",
                        AuditoriaServicio.Ok);
                }
                return Fallo();
            }

            if (!u.Activo)
            {
                AuditoriaServicio.RegistrarComo(u.IdUsuario, u.NombreUsuario, AccionAuditoria.InicioSesionFallido, "Usuario",
                    u.IdUsuario, "Usuario inactivo", AuditoriaServicio.Fallo);
                return Fallo();
            }

            _datos.ReiniciarIntentos(u.IdUsuario);
            u.IntentosFallidos = 0;
            u.BloqueadoHasta = null;
            Sesion.Iniciar(u);
            AuditoriaServicio.Registrar(AccionAuditoria.InicioSesion, "Usuario", u.IdUsuario, null, AuditoriaServicio.Ok);

            return new ResultadoLogin { Exitoso = true, Usuario = u, Mensaje = string.Empty };
        }

        /// <summary>
        /// Política de contraseña. Devuelve el mensaje del primer incumplimiento, o null si es válida:
        /// mínimo 8 caracteres, con mayúscula, minúscula y número.
        /// </summary>
        public static string ValidarPolitica(string contrasena)
        {
            if (string.IsNullOrEmpty(contrasena) || contrasena.Length < 8)
                return "La contraseña debe tener al menos 8 caracteres.";
            if (!contrasena.Any(char.IsUpper))
                return "La contraseña debe incluir al menos una letra mayúscula.";
            if (!contrasena.Any(char.IsLower))
                return "La contraseña debe incluir al menos una letra minúscula.";
            if (!contrasena.Any(char.IsDigit))
                return "La contraseña debe incluir al menos un número.";
            return null;
        }

        /// <summary>
        /// Crea el primer administrador. Solo funciona si NO existe ningún usuario
        /// (se vuelve a comprobar aquí, no solo en la ventana).
        /// </summary>
        public int CrearAdministradorInicial(string nombreUsuario, string nombreCompleto, string contrasena, string confirmacion)
        {
            if (_datos.ContarUsuarios() > 0)
                throw new ReglaNegocioException("Ya existen usuarios registrados; no se puede crear el administrador inicial.");

            return CrearInterno(nombreUsuario, nombreCompleto, contrasena, confirmacion, Rol.Administrador);
        }

        /// <summary>Crea un usuario con cualquier rol. Solo un Administrador con sesión iniciada puede hacerlo.</summary>
        public int CrearUsuario(string nombreUsuario, string nombreCompleto, string contrasena, string confirmacion, Rol rol)
        {
            Permisos.ExigirAdministrador();
            return CrearInterno(nombreUsuario, nombreCompleto, contrasena, confirmacion, rol);
        }

        private int CrearInterno(string nombreUsuario, string nombreCompleto, string contrasena, string confirmacion, Rol rol)
        {
            if (contrasena != confirmacion)
                throw new ValidacionException("La contraseña y su confirmación no coinciden.", "Confirmacion");

            string error = ValidarPolitica(contrasena);
            if (error != null)
                throw new ValidacionException(error, "Contrasena");

            HashContrasena.DatosHash h = HashContrasena.GenerarPartes(contrasena);
            // Los setters de Usuario validan nombre de usuario y nombre completo.
            Usuario nuevo = new Usuario(nombreUsuario, nombreCompleto, h.Hash, h.Salt, h.Iteraciones, rol);
            int id = _datos.Crear(nuevo);
            AuditoriaServicio.Registrar(AccionAuditoria.CrearUsuario, "Usuario", id, "Rol: " + rol, AuditoriaServicio.Ok);
            return id;
        }

        private static ResultadoLogin Fallo()
        {
            return new ResultadoLogin { Exitoso = false, Mensaje = MensajeGenerico };
        }
    }
}
