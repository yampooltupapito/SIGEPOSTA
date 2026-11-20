using System;

namespace SIGEPOSTA.Negocio
{
    /// <summary>
    /// Un código del catálogo de diagnósticos CIE-10 (ej. "J06.9 - Infección aguda de las vías respiratorias superiores").
    /// El catálogo es información pública (no es dato del paciente): va en claro. Lo que SÍ es sensible es qué
    /// código se le asignó a una atención, y eso se guarda cifrado (ver Atencion.Cie10Cifrado).
    /// La baja es lógica (Activo = false): el código deja de ofrecerse, pero las atenciones antiguas lo siguen mostrando.
    /// </summary>
    public class Cie10
    {
        public const int MaxDescripcion = 250;

        private string _codigo = string.Empty;
        private string _descripcion = string.Empty;

        /// <summary>Código en mayúsculas, ej. "J06.9" (clave primaria; no se edita).</summary>
        public string Codigo
        {
            get { return _codigo; }
            set { _codigo = Validador.Cie10Codigo(value, "Codigo"); }
        }

        public string Descripcion
        {
            get { return _descripcion; }
            set { _descripcion = Validador.TextoObligatorio(value, "Descripcion", MaxDescripcion); }
        }

        public bool Activo { get; set; }

        public Cie10()
        {
            Activo = true;
        }

        public Cie10(string codigo, string descripcion) : this()
        {
            Codigo = codigo;
            Descripcion = descripcion;
        }

        /// <summary>Texto para mostrar: "J06.9 - Descripción".</summary>
        public string Texto
        {
            get { return Combinar(_codigo, _descripcion); }
        }

        public override string ToString()
        {
            return Texto;
        }

        public static string Combinar(string codigo, string descripcion)
        {
            return string.IsNullOrEmpty(descripcion) ? codigo : codigo + " - " + descripcion;
        }

        /// <summary>Carga lo guardado SIN validar (un dato cargado desde SSMS con otro formato no debe romper la lista).</summary>
        public static Cie10 Cargar(string codigo, string descripcion, bool activo)
        {
            Cie10 c = new Cie10();
            c._codigo = (codigo ?? string.Empty).Trim();
            c._descripcion = descripcion ?? string.Empty;
            c.Activo = activo;
            return c;
        }
    }
}
