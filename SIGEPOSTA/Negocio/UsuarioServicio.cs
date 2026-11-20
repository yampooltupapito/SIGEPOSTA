using System;
using System.Collections.Generic;
using System.Linq;
using SIGEPOSTA.Datos;
using SIGEPOSTA.Seguridad;

namespace SIGEPOSTA.Negocio
{
    /// <summary>
    /// Gestión de usuarios (solo Administrador). Reglas:
    ///  - La contraseña cumple la política (mínimo 8, mayúscula, minúscula y número) y SIEMPRE se guarda
    ///    con PBKDF2 y un salt nuevo (también al restablecerla).
    ///  - Nadie puede desactivar su propia cuenta ni cambiarse el rol a sí mismo (la sesión abierta
    ///    seguiría con el rol anterior y quedaría inconsistente).
    ///  - Nunca puede quedar el sistema sin un administrador activo: se revisa aquí (mensaje claro) y
    ///    otra vez dentro del UPDATE de la base de datos (protege ante cambios simultáneos).
    /// </summary>
    public class UsuarioServicio
    {
        private const string MensajeUltimoAdmin = "Debe existir al menos un administrador activo.";

        private readonly UsuarioDatos _datos;
        private readonly AutenticacionServicio _autenticacion;

        public UsuarioServicio() : this(new UsuarioDatos())
        {
        }

        public UsuarioServicio(UsuarioDatos datos)
        {
            if (datos == null) throw new ArgumentNullException("datos");
            _datos = datos;
            _autenticacion = new AutenticacionServicio(datos);
        }

        public List<Usuario> Listar()
        {
            Permisos.ExigirAdministrador();
            return _datos.Listar()
                         .OrderBy(u => u.NombreUsuario, StringComparer.CurrentCultureIgnoreCase)
                         .ToList();
        }

        /// <summary>Crea un usuario con la política de contraseña aplicada. Devuelve su Id.</summary>
        public int Crear(string nombreUsuario, string nombreCompleto, string contrasena, string confirmacion, Rol rol)
        {
            Permisos.ExigirAdministrador();
            if (!Enum.IsDefined(typeof(Rol), rol)) throw new ValidacionException("Seleccione un rol.", "Rol");
            return _autenticacion.CrearUsuario(nombreUsuario, nombreCompleto, contrasena, confirmacion, rol);
        }

        public void CambiarRol(int idUsuario, Rol nuevoRol)
        {
            Permisos.ExigirAdministrador();
            if (!Enum.IsDefined(typeof(Rol), nuevoRol)) throw new ValidacionException("Seleccione un rol.", "Rol");
            if (EsUsuarioActual(idUsuario))
                throw new ReglaNegocioException("No puede cambiar su propio rol. Pídaselo a otro administrador.");

            Usuario u = ObtenerOFallar(idUsuario);
            if (u.Rol == nuevoRol) return;

            if (u.Rol == Rol.Administrador && u.Activo && _datos.ContarAdministradoresActivos() <= 1)
                throw new ReglaNegocioException(MensajeUltimoAdmin);

            if (_datos.CambiarRol(idUsuario, nuevoRol) == 0)
                throw new ReglaNegocioException(MensajeUltimoAdmin);
            AuditoriaServicio.Registrar(AccionAuditoria.CambioRol, "Usuario", idUsuario,
                "Rol: " + u.Rol + " -> " + nuevoRol, AuditoriaServicio.Ok);
        }

        public void CambiarActivo(int idUsuario, bool activo)
        {
            Permisos.ExigirAdministrador();
            if (!activo && EsUsuarioActual(idUsuario))
                throw new ReglaNegocioException("No puede desactivar su propia cuenta.");

            Usuario u = ObtenerOFallar(idUsuario);
            if (u.Activo == activo) return;

            if (!activo && u.Rol == Rol.Administrador && _datos.ContarAdministradoresActivos() <= 1)
                throw new ReglaNegocioException(MensajeUltimoAdmin);

            if (_datos.CambiarActivo(idUsuario, activo) == 0)
                throw new ReglaNegocioException(MensajeUltimoAdmin);
            AuditoriaServicio.Registrar(AccionAuditoria.CambioEstadoUsuario, "Usuario", idUsuario,
                activo ? "Activado" : "Desactivado", AuditoriaServicio.Ok);
        }

        /// <summary>Restablece la contraseña (con salt nuevo) y levanta cualquier bloqueo por intentos fallidos.</summary>
        public void RestablecerContrasena(int idUsuario, string contrasena, string confirmacion)
        {
            Permisos.ExigirAdministrador();
            if (contrasena != confirmacion)
                throw new ValidacionException("La contraseña y su confirmación no coinciden.", "Confirmacion");
            string error = AutenticacionServicio.ValidarPolitica(contrasena);
            if (error != null) throw new ValidacionException(error, "Contrasena");

            ObtenerOFallar(idUsuario);
            HashContrasena.DatosHash h = HashContrasena.GenerarPartes(contrasena); // salt aleatorio nuevo
            _datos.RestablecerContrasena(idUsuario, h.Hash, h.Salt, h.Iteraciones);
            // Nunca se guarda la contraseña (ni su hash): solo el hecho de que se restableció.
            AuditoriaServicio.Registrar(AccionAuditoria.RestablecerContrasena, "Usuario", idUsuario, null, AuditoriaServicio.Ok);
        }

        // ---------- Auxiliares ----------

        private static bool EsUsuarioActual(int idUsuario)
        {
            return Sesion.UsuarioActual != null && Sesion.UsuarioActual.IdUsuario == idUsuario;
        }

        private Usuario ObtenerOFallar(int idUsuario)
        {
            Usuario u = _datos.ObtenerPorId(idUsuario);
            if (u == null) throw new ReglaNegocioException("El usuario ya no existe. Actualice la lista.");
            return u;
        }
    }
}
