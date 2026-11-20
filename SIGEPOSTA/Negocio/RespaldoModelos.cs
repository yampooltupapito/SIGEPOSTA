using System;
using System.Collections.Generic;

namespace SIGEPOSTA.Negocio
{
    // Modelos que se serializan a XML con XmlSerializer. Requisitos de XmlSerializer: clases públicas,
    // constructor sin parámetros y propiedades públicas de lectura y escritura.
    // Decisión de diseño: son clases aparte de las entidades (Paciente, Atencion...) para que el formato
    // del archivo .sigebak no cambie si cambian las entidades, y para incluir los datos YA descifrados
    // (el archivo completo se cifra después, por eso el DNI y el diagnóstico van en claro SOLO ahí dentro).

    /// <summary>Contenido completo de un archivo de respaldo (.sigebak).</summary>
    public class RespaldoArchivo
    {
        public string Version { get; set; }
        public DateTime FechaRespaldo { get; set; }
        /// <summary>"Paciente" o "Establecimiento" (los respaldos antiguos pueden decir "Posta").</summary>
        public string Alcance { get; set; }
        public string Posta { get; set; }
        public string GeneradoPor { get; set; }
        public List<PacienteRespaldo> Pacientes { get; set; }

        public RespaldoArchivo()
        {
            Pacientes = new List<PacienteRespaldo>();
        }
    }

    public class PacienteRespaldo
    {
        public string NumeroHistoria { get; set; }
        public string Dni { get; set; }
        public string Nombres { get; set; }
        public string Apellidos { get; set; }
        public DateTime FechaNacimiento { get; set; }
        public string Telefono { get; set; }
        public string Direccion { get; set; }
        public string Posta { get; set; }
        /// <summary>Antecedentes de la historia (ya descifrados; el archivo completo va cifrado). Los respaldos antiguos no lo traen.</summary>
        public string Antecedentes { get; set; }
        public List<AlergiaRespaldo> Alergias { get; set; }
        public List<AtencionRespaldo> Atenciones { get; set; }

        public PacienteRespaldo()
        {
            Alergias = new List<AlergiaRespaldo>();
            Atenciones = new List<AtencionRespaldo>();
        }
    }

    public class AtencionRespaldo
    {
        public DateTime Fecha { get; set; }
        public string Motivo { get; set; }
        public string Diagnostico { get; set; }
        /// <summary>Diagnóstico CIE-10 como "J06.9 - Descripción" (vacío en atenciones antiguas; "[Código CIE-10 ilegible]" si no se pudo descifrar). Los respaldos antiguos no lo traen.</summary>
        public string Cie10 { get; set; }
        public string Medico { get; set; }
        /// <summary>Receta de la atención, ej. "Paracetamol x 10" (vacío si no hubo). Los respaldos antiguos no la traen.</summary>
        public string Receta { get; set; }
        /// <summary>Signos vitales de la atención (omitido en el XML si no se registró ninguno).</summary>
        public SignosRespaldo Signos { get; set; }
    }

    /// <summary>
    /// Signos vitales dentro del XML del respaldo: los 7 valores numéricos (los que no se midieron se omiten del XML),
    /// el IMC calculado y el resumen en texto.
    /// </summary>
    public class SignosRespaldo
    {
        public decimal? PesoKg { get; set; }
        public decimal? TallaCm { get; set; }
        public int? PresionSistolica { get; set; }
        public int? PresionDiastolica { get; set; }
        public decimal? Temperatura { get; set; }
        public int? FrecuenciaCardiaca { get; set; }
        public int? SaturacionO2 { get; set; }
        public decimal? Imc { get; set; }
        public string Resumen { get; set; }

        // XmlSerializer: un valor nulo no se escribe (en vez de <PesoKg xsi:nil="true" />).
        public bool ShouldSerializePesoKg() { return PesoKg.HasValue; }
        public bool ShouldSerializeTallaCm() { return TallaCm.HasValue; }
        public bool ShouldSerializePresionSistolica() { return PresionSistolica.HasValue; }
        public bool ShouldSerializePresionDiastolica() { return PresionDiastolica.HasValue; }
        public bool ShouldSerializeTemperatura() { return Temperatura.HasValue; }
        public bool ShouldSerializeFrecuenciaCardiaca() { return FrecuenciaCardiaca.HasValue; }
        public bool ShouldSerializeSaturacionO2() { return SaturacionO2.HasValue; }
        public bool ShouldSerializeImc() { return Imc.HasValue; }
    }

    public class AlergiaRespaldo
    {
        public string Sustancia { get; set; }
        public string Gravedad { get; set; }
        public string Observacion { get; set; }
        public bool Activo { get; set; }
        public DateTime Fecha { get; set; }
    }
}
