using System;
using System.Collections.Generic;

namespace SIGEPOSTA.Negocio
{
    /// <summary>
    /// Historia clínica de un paciente. Es una relación de composición: contiene sus atenciones.
    /// Se enlaza al paciente por IdPaciente (no guarda el objeto Paciente completo)
    /// para evitar cargar datos innecesarios y referencias circulares.
    /// </summary>
    public class HistoriaClinica
    {
        private string _numeroHistoria;
        private string _antecedentes = string.Empty;
        private readonly List<Atencion> _atenciones = new List<Atencion>();

        public int IdHistoria { get; set; }
        public int IdPaciente { get; set; }

        /// <summary>Antecedentes cifrados con AES (bytes tal como se guardan en la BD; null si no hay).</summary>
        public byte[] AntecedentesCifrado { get; set; }
        public DateTime FechaApertura { get; set; }

        public string NumeroHistoria
        {
            get { return _numeroHistoria; }
            set { _numeroHistoria = Validador.TextoObligatorio(value, "NumeroHistoria", 20); }
        }

        public string Antecedentes
        {
            get { return _antecedentes; }
            set { _antecedentes = Validador.TextoOpcional(value, "Antecedentes", 1000); }
        }

        /// <summary>Solo lectura: las atenciones se agregan con AgregarAtencion para mantener la consistencia.</summary>
        public IReadOnlyList<Atencion> Atenciones
        {
            get { return _atenciones; }
        }

        public HistoriaClinica()
        {
            FechaApertura = DateTime.Now;
        }

        public HistoriaClinica(int idPaciente, string numeroHistoria, string antecedentes)
        {
            if (idPaciente <= 0)
                throw new ValidacionException("La historia debe pertenecer a un paciente válido.", "idPaciente");
            IdPaciente = idPaciente;
            NumeroHistoria = numeroHistoria;
            Antecedentes = antecedentes;
            FechaApertura = DateTime.Now;
        }

        public void AgregarAtencion(Atencion atencion)
        {
            if (atencion == null) throw new ArgumentNullException("atencion");
            atencion.IdHistoria = IdHistoria;
            _atenciones.Add(atencion);
        }
    }
}
