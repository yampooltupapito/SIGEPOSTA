using System;
using System.Globalization;
using System.Text;

namespace SIGEPOSTA.Negocio
{
    /// <summary>
    /// Alergia de un paciente (tabla AlergiaPaciente). Decisión de diseño: una alergia NUNCA se borra:
    /// se desactiva (Activo = 0) para conservar el historial; solo las activas cuentan para los avisos.
    /// </summary>
    public class Alergia
    {
        public const string Leve = "Leve";
        public const string Moderada = "Moderada";
        public const string Grave = "Grave";
        public static readonly string[] Gravedades = { Leve, Moderada, Grave };

        private string _sustancia;
        private string _gravedad = Leve;
        private string _observacion = string.Empty;

        public int IdAlergia { get; set; }
        public int IdPaciente { get; set; }
        public bool Activo { get; set; }
        public DateTime Fecha { get; set; }

        public string Sustancia
        {
            get { return _sustancia; }
            set { _sustancia = Validador.TextoObligatorio(value, "Sustancia", 100); }
        }

        public string Gravedad
        {
            get { return _gravedad; }
            set
            {
                string v = (value ?? string.Empty).Trim();
                foreach (string g in Gravedades)
                {
                    if (string.Equals(g, v, StringComparison.OrdinalIgnoreCase)) { _gravedad = g; return; }
                }
                throw new ValidacionException("Seleccione la gravedad (Leve, Moderada o Grave).", "Gravedad");
            }
        }

        public string Observacion
        {
            get { return _observacion; }
            set { _observacion = Validador.TextoOpcional(value, "Observacion", 200); }
        }

        public bool EsGrave
        {
            get { return _gravedad == Grave; }
        }

        public Alergia()
        {
            Activo = true;
            Fecha = DateTime.Now;
        }

        public Alergia(int idPaciente, string sustancia, string gravedad, string observacion) : this()
        {
            IdPaciente = idPaciente;
            Sustancia = sustancia;
            Gravedad = gravedad;
            Observacion = observacion;
        }

        // ================== Coincidencia con un medicamento ==================

        /// <summary>
        /// True si el nombre del medicamento coincide con la sustancia de la alergia: se comparan sin tildes ni
        /// mayúsculas, y vale que uno contenga al otro (alergia "penicilina" ↔ medicamento "Penicilina G benzatínica").
        /// Limitación: solo detecta coincidencias de NOMBRE; no sabe de familias de fármacos (una alergia a la
        /// penicilina no avisa para la amoxicilina). Es una ayuda, no sustituye el criterio del médico.
        /// </summary>
        public static bool Coincide(string sustancia, string nombreMedicamento)
        {
            string s = Normalizar(sustancia);
            string m = Normalizar(nombreMedicamento);
            if (s.Length < 3 || m.Length < 3) return false;
            return m.Contains(s) || s.Contains(m);
        }

        /// <summary>Minúsculas, sin tildes y con los espacios sobrantes quitados.</summary>
        public static string Normalizar(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
            string descompuesto = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder(descompuesto.Length);
            foreach (char c in descompuesto)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
