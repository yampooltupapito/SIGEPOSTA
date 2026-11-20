using System;
using System.Collections.Generic;
using System.Linq;
using SIGEPOSTA.Seguridad;

namespace SIGEPOSTA.Negocio
{
    /// <summary>
    /// Respaldo y verificación de historias clínicas (permiso: Administrador y Médico).
    /// Decisiones de diseño:
    ///  - Los métodos de exportación reciben un callback de progreso y (el de posta) uno de cancelación,
    ///    para que la pantalla los ejecute en un BackgroundWorker sin congelarse. Este servicio no sabe
    ///    nada de ventanas ni de hilos.
    ///  - El DNI y los diagnósticos se descifran SOLO para armar el modelo en memoria; el archivo que
    ///    se escribe a disco sale siempre comprimido y cifrado (ArchivoRespaldoSeguro).
    ///  - "Restaurar" aquí significa verificar y leer el contenido; NO escribe nada en la base de datos.
    /// </summary>
    public class RespaldoServicio
    {
        public const string Version = "1.0";
        private readonly PacienteServicio _pacientes;

        public RespaldoServicio() : this(new PacienteServicio()) { }

        public RespaldoServicio(PacienteServicio pacientes)
        {
            if (pacientes == null) throw new ArgumentNullException("pacientes");
            _pacientes = pacientes;
        }

        /// <summary>Respalda la historia clínica de UN paciente.</summary>
        public void ExportarPaciente(Paciente paciente, string ruta, Action<int, string> progreso)
        {
            Permisos.ExigirAccesoPacientes();
            if (paciente == null) throw new ReglaNegocioException("Seleccione un paciente.");
            ruta = NormalizarRuta(ruta);

            try
            {
                Avisar(progreso, 10, "Leyendo la historia clínica…");
                RespaldoArchivo archivo = NuevoArchivo("Paciente");
                archivo.Pacientes.Add(ArmarPaciente(paciente));

                Avisar(progreso, 60, "Comprimiendo y cifrando…");
                ArchivoRespaldoSeguro.Guardar(ruta, archivo);
                Avisar(progreso, 100, "Respaldo guardado.");
            }
            catch (Exception)
            {
                AuditoriaServicio.Registrar(AccionAuditoria.Respaldo, "Paciente", paciente.IdPaciente,
                    "Respaldo de la historia del paciente Id " + paciente.IdPaciente, AuditoriaServicio.Fallo);
                throw;
            }
            // Solo el identificador: ni la ruta del archivo ni datos del paciente.
            AuditoriaServicio.Registrar(AccionAuditoria.Respaldo, "Paciente", paciente.IdPaciente,
                "Respaldo de la historia del paciente Id " + paciente.IdPaciente, AuditoriaServicio.Ok);
        }

        /// <summary>
        /// Respalda las historias de TODOS los pacientes activos del establecimiento.
        /// Devuelve la cantidad de historias respaldadas, o -1 si se canceló (en ese caso no se escribe archivo).
        /// </summary>
        public int ExportarEstablecimiento(string ruta, Action<int, string> progreso, Func<bool> cancelado)
        {
            Permisos.ExigirAccesoPacientes();
            ruta = NormalizarRuta(ruta);

            List<Paciente> lista = _pacientes.Listar();
            if (lista.Count == 0)
                throw new ReglaNegocioException("El establecimiento no tiene pacientes para respaldar.");

            try
            {
                RespaldoArchivo archivo = NuevoArchivo("Establecimiento");
                for (int i = 0; i < lista.Count; i++)
                {
                    if (cancelado != null && cancelado()) { AuditarCancelado(); return -1; }
                    Avisar(progreso, 5 + (i * 80) / lista.Count,
                        "Leyendo historia " + (i + 1) + " de " + lista.Count + "…");
                    archivo.Pacientes.Add(ArmarPaciente(lista[i]));
                }

                if (cancelado != null && cancelado()) { AuditarCancelado(); return -1; }
                Avisar(progreso, 90, "Comprimiendo y cifrando…");
                ArchivoRespaldoSeguro.Guardar(ruta, archivo);
                Avisar(progreso, 100, "Respaldo guardado.");
            }
            catch (Exception)
            {
                AuditoriaServicio.Registrar(AccionAuditoria.Respaldo, "Establecimiento", null,
                    "Respaldo de todas las historias del establecimiento", AuditoriaServicio.Fallo);
                throw;
            }
            AuditoriaServicio.Registrar(AccionAuditoria.Respaldo, "Establecimiento", null,
                "Respaldo de " + lista.Count + " historia(s) del establecimiento", AuditoriaServicio.Ok);
            return lista.Count;
        }

        private static void AuditarCancelado()
        {
            AuditoriaServicio.Registrar(AccionAuditoria.Respaldo, "Establecimiento", null,
                "Respaldo del establecimiento cancelado por el usuario", AuditoriaServicio.Cancelado);
        }

        /// <summary>Verifica (integridad y llave) y lee un respaldo. Lanza RespaldoInvalidoException si no es válido.</summary>
        public RespaldoArchivo Verificar(string ruta)
        {
            Permisos.ExigirAccesoPacientes();
            if (string.IsNullOrWhiteSpace(ruta)) throw new ReglaNegocioException("Seleccione un archivo de respaldo.");
            RespaldoArchivo archivo;
            try
            {
                archivo = ArchivoRespaldoSeguro.Leer<RespaldoArchivo>(ruta);
            }
            catch (Exception)
            {
                AuditoriaServicio.Registrar(AccionAuditoria.Restauracion, "Respaldo", null,
                    "Verificación de un archivo de respaldo", AuditoriaServicio.Fallo);   // sin ruta ni nombre de archivo
                throw;
            }
            AuditoriaServicio.Registrar(AccionAuditoria.Restauracion, "Respaldo", null,
                "Verificación de respaldo (" + (archivo.Pacientes == null ? 0 : archivo.Pacientes.Count) + " historia(s))",
                AuditoriaServicio.Ok);
            return archivo;
        }

        // ---------- Auxiliares ----------

        private PacienteRespaldo ArmarPaciente(Paciente p)
        {
            PacienteRespaldo r = new PacienteRespaldo();
            r.NumeroHistoria = p.NumeroHistoria;
            r.Dni = _pacientes.DescifrarDni(p.IdPaciente);   // en claro SOLO dentro del archivo que luego se cifra
            r.Nombres = p.Nombres;
            r.Apellidos = p.Apellidos;
            r.FechaNacimiento = p.FechaNacimiento;
            r.Telefono = p.Telefono;
            r.Direccion = p.Direccion;
            r.Posta = Establecimiento.Nombre;

            // Antecedentes y alergias: se descifran/leen solo para armar el modelo del respaldo.
            r.Alergias = _pacientes.ListarAlergias(p.IdPaciente)
                .Select(x => new AlergiaRespaldo
                {
                    Sustancia = x.Sustancia,
                    Gravedad = x.Gravedad,
                    Observacion = x.Observacion,
                    Activo = x.Activo,
                    Fecha = x.Fecha
                })
                .ToList();
            r.Antecedentes = _pacientes.ObtenerAntecedentes(_pacientes.ObtenerHistoria(p.IdPaciente));

            if (p.IdHistoria > 0)
            {
                r.Atenciones = _pacientes.ListarAtenciones(p.IdHistoria)
                    .OrderBy(a => a.Fecha)
                    .Select(a => new AtencionRespaldo
                    {
                        Fecha = a.Fecha,
                        Motivo = a.Motivo,
                        Diagnostico = a.Diagnostico,   // ya descifrado por el servicio
                        Cie10 = (a.Cie10Codigo == null && !a.Cie10Ilegible) ? string.Empty : a.Cie10Texto,   // "Código - descripción"
                        Medico = a.NombreMedico,
                        Receta = a.RecetaTexto,
                        Signos = ConvertirSignos(a.Signos)
                    })
                    .ToList();
            }
            return r;
        }

        private static SignosRespaldo ConvertirSignos(SignosVitales s)
        {
            if (s == null || !s.TieneDatos) return null;
            return new SignosRespaldo
            {
                PesoKg = s.PesoKg,
                TallaCm = s.TallaCm,
                PresionSistolica = s.PresionSistolica,
                PresionDiastolica = s.PresionDiastolica,
                Temperatura = s.Temperatura,
                FrecuenciaCardiaca = s.FrecuenciaCardiaca,
                SaturacionO2 = s.SaturacionO2,
                Imc = s.Imc,
                Resumen = s.Resumen()
            };
        }

        private static RespaldoArchivo NuevoArchivo(string alcance)
        {
            RespaldoArchivo a = new RespaldoArchivo();
            a.Version = Version;
            a.FechaRespaldo = DateTime.Now;
            a.Alcance = alcance;
            a.Posta = Establecimiento.Nombre;   // nombre del establecimiento (la propiedad conserva su nombre por compatibilidad con respaldos antiguos)
            a.GeneradoPor = Sesion.UsuarioActual == null ? string.Empty : Sesion.UsuarioActual.NombreCompleto;
            return a;
        }

        private static string NormalizarRuta(string ruta)
        {
            if (string.IsNullOrWhiteSpace(ruta)) throw new ReglaNegocioException("Indique dónde guardar el respaldo.");
            ruta = ruta.Trim();
            if (!ruta.EndsWith(ArchivoRespaldoSeguro.Extension, StringComparison.OrdinalIgnoreCase))
                ruta += ArchivoRespaldoSeguro.Extension;
            return ruta;
        }

        private static void Avisar(Action<int, string> progreso, int porcentaje, string texto)
        {
            if (progreso != null) progreso(porcentaje, texto);
        }
    }
}
