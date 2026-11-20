using System;
using SIGEPOSTA.Negocio;

namespace SIGEPOSTA.Seguridad
{
    /// <summary>
    /// Sesión actual de la aplicación: quién inició sesión y con qué rol.
    /// Decisión de diseño: es estática porque la aplicación de escritorio es monousuario por proceso.
    /// Los formularios consultan TieneRol(...) para habilitar u ocultar opciones.
    /// </summary>
    public static class Sesion
    {
        public static Usuario UsuarioActual { get; private set; }

        public static bool EstaAutenticado
        {
            get { return UsuarioActual != null; }
        }

        public static Rol RolActual
        {
            get
            {
                if (UsuarioActual == null)
                    throw new ReglaNegocioException("No hay una sesión iniciada.");
                return UsuarioActual.Rol;
            }
        }

        public static void Iniciar(Usuario usuario)
        {
            if (usuario == null) throw new ArgumentNullException("usuario");
            if (!usuario.Activo) throw new ReglaNegocioException("El usuario está inactivo.");
            UsuarioActual = usuario;
        }

        public static void Cerrar()
        {
            UsuarioActual = null;
        }

        /// <summary>True si hay sesión y el rol del usuario es alguno de los indicados.</summary>
        public static bool TieneRol(params Rol[] roles)
        {
            if (UsuarioActual == null || roles == null) return false;
            foreach (Rol r in roles)
            {
                if (UsuarioActual.Rol == r) return true;
            }
            return false;
        }
    }
}
