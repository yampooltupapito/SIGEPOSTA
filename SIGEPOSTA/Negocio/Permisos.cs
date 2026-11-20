using System;
using SIGEPOSTA.Seguridad;

namespace SIGEPOSTA.Negocio
{
    /// <summary>
    /// Reglas de autorización por rol, en la capa de Negocio.
    /// Decisión de diseño: ocultar un botón NO es seguridad (se puede saltar). Por eso cada operación
    /// sensible llama a un método Exigir..., que lanza UnauthorizedAccessException si el rol no corresponde.
    /// Los formularios usan los métodos Puede... solo para mostrar u ocultar opciones.
    /// </summary>
    public static class Permisos
    {
        public static bool PuedeAccederPacientes
        {
            get { return Sesion.TieneRol(Rol.Administrador, Rol.Medico); }
        }

        public static bool PuedeAccederInventario
        {
            get { return Sesion.TieneRol(Rol.Administrador, Rol.Farmacia); }
        }

        /// <summary>Proveedores de medicamentos: Administrador y Farmacia (los mismos que el Inventario).</summary>
        public static bool PuedeAccederProveedores
        {
            get { return Sesion.TieneRol(Rol.Administrador, Rol.Farmacia); }
        }

        public static void ExigirAccesoProveedores()
        {
            Exigir(PuedeAccederProveedores, "Proveedores");
        }

        /// <summary>Formas farmacéuticas y unidades de medida de los medicamentos: Administrador y Farmacia.</summary>
        public static bool PuedeAccederCatalogosMedicamento
        {
            get { return Sesion.TieneRol(Rol.Administrador, Rol.Farmacia); }
        }

        public static void ExigirAccesoCatalogosMedicamento()
        {
            Exigir(PuedeAccederCatalogosMedicamento, "Formas y unidades");
        }

        /// <summary>Recetar dentro de una atención: Administrador o Médico (no requiere acceso a la pantalla de Inventario).</summary>
        public static bool PuedeRecetar
        {
            get { return Sesion.TieneRol(Rol.Administrador, Rol.Medico); }
        }

        public static void ExigirRecetar()
        {
            Exigir(PuedeRecetar, "Recetas");
        }

        public static bool EsAdministrador
        {
            get { return Sesion.TieneRol(Rol.Administrador); }
        }

        /// <summary>Algún reporte disponible: pacientes (Administrador, Médico) o inventario (Administrador, Farmacia).</summary>
        public static bool PuedeVerReportes
        {
            get { return PuedeAccederPacientes || PuedeAccederInventario; }
        }

        /// <summary>
        /// Panel de inicio: cualquier usuario con sesión iniciada. Qué BLOQUES ve cada rol lo decide
        /// DashboardServicio con los permisos de Pacientes e Inventario.
        /// </summary>
        public static void ExigirAccesoDashboard()
        {
            Exigir(true, "Panel de inicio");
        }

        public static void ExigirAccesoReportes()
        {
            Exigir(PuedeVerReportes, "Reportes");
        }

        public static void ExigirAccesoPacientes()
        {
            Exigir(PuedeAccederPacientes, "Pacientes");
        }

        public static void ExigirAccesoInventario()
        {
            Exigir(PuedeAccederInventario, "Inventario");
        }

        public static void ExigirAdministrador()
        {
            Exigir(EsAdministrador, "Administración");
        }

        private static void Exigir(bool permitido, string modulo)
        {
            // Todo acceso denegado queda en la auditoría (con el módulo y el rol, sin datos personales).
            if (!Sesion.EstaAutenticado)
            {
                AuditoriaServicio.Registrar(AccionAuditoria.AccesoDenegado, null, null,
                    "Módulo: " + modulo + " (sin sesión iniciada)", AuditoriaServicio.Denegado);
                throw new AccesoDenegadoException("Debe iniciar sesión para acceder a " + modulo + ".");
            }
            if (!permitido)
            {
                AuditoriaServicio.Registrar(AccionAuditoria.AccesoDenegado, null, null,
                    "Módulo: " + modulo + " · Rol: " + Sesion.RolActual, AuditoriaServicio.Denegado);
                throw new AccesoDenegadoException("Su rol no tiene permiso para acceder a " + modulo + ".");
            }
        }
    }
}
