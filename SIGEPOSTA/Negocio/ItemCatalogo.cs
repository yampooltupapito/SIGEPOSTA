using System;

namespace SIGEPOSTA.Negocio
{
    /// <summary>Cuál de los dos catálogos de medicamentos: formas farmacéuticas o unidades de medida.</summary>
    public enum TipoCatalogo
    {
        Forma,
        Unidad
    }

    /// <summary>
    /// Un elemento de catálogo (forma farmacéutica o unidad de medida): Id, Nombre y Activo.
    /// "Sin definir" es el valor de los medicamentos antiguos: no se puede renombrar ni dar de baja.
    /// La baja es lógica (Activo = false): deja de ofrecerse en medicamentos nuevos pero los existentes la conservan.
    /// </summary>
    public class ItemCatalogo
    {
        public const int MaxNombre = 60;

        private string _nombre = string.Empty;

        public int Id { get; set; }
        public bool Activo { get; set; }

        /// <summary>Cuántos medicamentos ACTIVOS usan este elemento (solo para mostrar y avisar antes de una baja).</summary>
        public int MedicamentosQueLoUsan { get; set; }

        public string Nombre
        {
            get { return _nombre; }
            set { _nombre = Validador.TextoObligatorio(value, "Nombre", MaxNombre); }
        }

        public bool EsSinDefinir
        {
            get { return Medicamento.EsSinDefinir(_nombre) && !string.IsNullOrWhiteSpace(_nombre); }
        }

        public ItemCatalogo()
        {
            Activo = true;
        }

        public ItemCatalogo(string nombre) : this()
        {
            Nombre = nombre;
        }

        /// <summary>Carga lo guardado sin validar (un dato cargado desde SSMS no debe romper la lista).</summary>
        public static ItemCatalogo Cargar(int id, string nombre, bool activo, int usan)
        {
            ItemCatalogo i = new ItemCatalogo();
            i.Id = id;
            i._nombre = nombre ?? string.Empty;
            i.Activo = activo;
            i.MedicamentosQueLoUsan = usan;
            return i;
        }

        public override string ToString()
        {
            return _nombre;
        }
    }
}
