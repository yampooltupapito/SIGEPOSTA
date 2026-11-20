using System;

namespace SIGEPOSTA.Negocio
{
    /// <summary>
    /// Una consulta/atención dentro de una historia clínica.
    /// Decisión de diseño: el diagnóstico es dato sensible; en memoria vive en texto plano (Diagnostico)
    /// y en la BD solo existen los bytes cifrados (DiagnosticoCifrado). El servicio cifra antes de guardar
    /// y descifra solo cuando la pantalla lo necesita.
    /// </summary>
    public class Atencion
    {
        private string _motivo;
        private string _diagnostico;
        private string _tratamiento = string.Empty;
        private DateTime _fecha;

        public int IdAtencion { get; set; }
        public int IdHistoria { get; set; }

        /// <summary>Usuario (médico) que realizó la atención (columna IdUsuario).</summary>
        public int IdMedico { get; set; }

        /// <summary>Nombre del usuario que atendió, solo para mostrar.</summary>
        public string NombreMedico { get; set; }

        /// <summary>Diagnóstico cifrado con AES (bytes tal como se guardan en la BD).</summary>
        public byte[] DiagnosticoCifrado { get; set; }

        /// <summary>
        /// Receta opcional: medicamento y cantidad. Van JUNTOS o ninguno (la tabla lo exige con un CHECK):
        /// ambos null = atención sin receta; ambos con valor = hay receta y la cantidad es mayor que cero.
        /// </summary>
        public int? IdMedicamento { get; set; }
        public int? CantidadRecetada { get; set; }

        /// <summary>
        /// Código CIE-10 cifrado con AES (IV aleatorio), tal como se guarda en la columna Cie10Cifrado.
        /// null = atención antigua sin código (en las nuevas es obligatorio).
        /// </summary>
        public byte[] Cie10Cifrado { get; set; }

        /// <summary>Código CIE-10 descifrado, SOLO en memoria para mostrarlo (nunca se guarda en claro ni se audita).</summary>
        public string Cie10Codigo { get; set; }

        /// <summary>Descripción del catálogo para ese código (vacía si el código ya no está en el catálogo).</summary>
        public string Cie10Descripcion { get; set; }

        /// <summary>true si había un código guardado pero no se pudo descifrar o no tiene formato CIE-10.</summary>
        public bool Cie10Ilegible { get; set; }

        /// <summary>"J06.9 - Descripción"; "Sin código CIE-10" (atención antigua) o "[Código CIE-10 ilegible]".</summary>
        public string Cie10Texto
        {
            get
            {
                if (Cie10Ilegible) return "[Código CIE-10 ilegible]";
                if (string.IsNullOrEmpty(Cie10Codigo)) return "Sin código CIE-10";
                return string.IsNullOrEmpty(Cie10Descripcion)
                    ? Cie10Codigo + " - (no figura en el catálogo)"
                    : Cie10Codigo + " - " + Cie10Descripcion;
            }
        }

        /// <summary>Signos vitales de la atención (null si no se registró ninguno).</summary>
        public SignosVitales Signos { get; set; }

        /// <summary>Texto para mostrar en la lista, por ejemplo "Paracetamol x 10" (vacío si no hubo receta).</summary>
        public string RecetaTexto { get; set; }

        public bool TieneReceta
        {
            get { return IdMedicamento.HasValue && CantidadRecetada.HasValue; }
        }

        public DateTime Fecha
        {
            get { return _fecha; }
            set { _fecha = Validador.FechaNoFutura(value, "Fecha"); }
        }

        public string Motivo
        {
            get { return _motivo; }
            set { _motivo = Validador.TextoObligatorio(value, "Motivo", 200); }
        }

        public string Diagnostico
        {
            get { return _diagnostico; }
            set { _diagnostico = Validador.TextoObligatorio(value, "Diagnostico", 1000); }
        }

        public string Tratamiento
        {
            get { return _tratamiento; }
            set { _tratamiento = Validador.TextoOpcional(value, "Tratamiento", 1000); }
        }

        public Atencion()
        {
            _fecha = DateTime.Now;
        }

        public Atencion(int idHistoria, int idMedico, string motivo, string diagnostico, string tratamiento)
        {
            IdHistoria = idHistoria;
            IdMedico = idMedico;
            Fecha = DateTime.Now;
            Motivo = motivo;
            Diagnostico = diagnostico;
            Tratamiento = tratamiento;
        }
    }
}
