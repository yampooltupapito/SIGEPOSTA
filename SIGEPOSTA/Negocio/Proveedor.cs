using System;

namespace SIGEPOSTA.Negocio
{
    /// <summary>
    /// Proveedor de medicamentos. Las reglas de cada campo se validan en los setters (mismo estilo que Medicamento):
    /// nombre obligatorio, RUC de 11 dígitos con prefijo y dígito verificador válidos, teléfono y correo opcionales.
    /// Nunca se borra: la baja es lógica (Activo = false) para conservar el historial de ingresos.
    /// </summary>
    public class Proveedor
    {
        public const int MaxNombre = 150;
        public const int MaxCorreo = 100;
        public const int MaxDireccion = 150;

        private string _nombre = string.Empty;
        private string _ruc = string.Empty;
        private string _telefono = string.Empty;
        private string _correo = string.Empty;
        private string _direccion = string.Empty;

        public int IdProveedor { get; set; }
        public bool Activo { get; set; }

        public string Nombre
        {
            get { return _nombre; }
            set { _nombre = Validador.TextoObligatorio(value, "Nombre", MaxNombre); }
        }

        public string Ruc
        {
            get { return _ruc; }
            set { _ruc = Validador.Ruc(value); }
        }

        public string Telefono
        {
            get { return _telefono; }
            set { _telefono = Validador.Telefono(value); }
        }

        public string Correo
        {
            get { return _correo; }
            set { _correo = Validador.Correo(value, MaxCorreo); }
        }

        public string Direccion
        {
            get { return _direccion; }
            set { _direccion = Validador.TextoOpcional(value, "Direccion", MaxDireccion); }
        }

        public Proveedor()
        {
            Activo = true;
        }

        public Proveedor(string nombre, string ruc, string telefono, string correo, string direccion) : this()
        {
            Nombre = nombre;
            Ruc = ruc;
            Telefono = telefono;
            Correo = correo;
            Direccion = direccion;
        }

        /// <summary>
        /// Carga los valores tal como están guardados, SIN validar: un dato viejo o cargado desde SSMS con otro formato
        /// no debe impedir que la lista se muestre (las reglas se aplican al guardar).
        /// </summary>
        public static Proveedor Cargar(int id, string nombre, string ruc, string telefono, string correo, string direccion, bool activo)
        {
            Proveedor p = new Proveedor();
            p.IdProveedor = id;
            p._nombre = nombre ?? string.Empty;
            p._ruc = ruc ?? string.Empty;
            p._telefono = telefono ?? string.Empty;
            p._correo = correo ?? string.Empty;
            p._direccion = direccion ?? string.Empty;
            p.Activo = activo;
            return p;
        }

        /// <summary>Texto para listas: "Nombre (RUC)".</summary>
        public override string ToString()
        {
            return Nombre + " (" + Ruc + ")";
        }
    }
}
