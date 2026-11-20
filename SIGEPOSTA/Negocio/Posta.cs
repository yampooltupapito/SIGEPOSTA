namespace SIGEPOSTA.Negocio
{
    /// <summary>
    /// Puesto de salud (tabla Posta). SIGEPOSTA hoy trabaja con UN solo establecimiento (ver Establecimiento) y
    /// ninguna pantalla pide elegir posta; esta entidad se conserva, junto con la tabla y la columna IdPosta,
    /// para que la estructura quede lista para crecer a una red de postas.
    /// </summary>
    public class Posta
    {
        public int IdPosta { get; set; }
        public string Nombre { get; set; }
        public string Distrito { get; set; }
        public string Categoria { get; set; }
        public bool Activo { get; set; }

        public Posta() { }

        public Posta(int idPosta, string nombre, string distrito, string categoria, bool activo)
        {
            IdPosta = idPosta;
            Nombre = nombre;
            Distrito = distrito;
            Categoria = categoria;
            Activo = activo;
        }

        /// <summary>Texto que se muestra en la lista desplegable.</summary>
        public override string ToString()
        {
            return Nombre;
        }
    }
}
