using System;
using System.Linq;

namespace SIGEPOSTA.Negocio
{
    /// <summary>
    /// Medicamento del inventario.
    /// Decisión de diseño: el stock se modifica con Ingresar/Retirar (reglas de negocio),
    /// pero el setter también valida que nunca sea negativo para proteger la carga desde la BD.
    /// </summary>
    public class Medicamento
    {
        private string _nombre;
        private string _presentacion;
        private int _stock;
        private int _stockMinimo;

        /// <summary>Días de anticipación para avisar que un medicamento está por vencer.</summary>
        public const int DiasAlertaVencimiento = 30;

        public int IdMedicamento { get; set; }
        public DateTime? FechaVencimiento { get; set; }

        /// <summary>Posta a la que pertenece el stock (0 = sin asignar).</summary>
        public int IdPosta { get; set; }

        /// <summary>Nombre de la posta, solo para mostrar en listas.</summary>
        public string PostaNombre { get; set; }

        public bool Activo { get; set; }

        public string Nombre
        {
            get { return _nombre; }
            set { _nombre = Validador.TextoObligatorio(value, "Nombre", 100); }
        }

        /// <summary>Opcional: la columna Presentacion de la BD acepta NULL.</summary>
        public string Presentacion
        {
            get { return _presentacion; }
            set { _presentacion = Validador.TextoOpcional(value, "Presentacion", 60); }
        }

        public int Stock
        {
            get { return _stock; }
            set { _stock = Validador.NoNegativo(value, "Stock"); }
        }

        public int StockMinimo
        {
            get { return _stockMinimo; }
            set { _stockMinimo = Validador.NoNegativo(value, "StockMinimo"); }
        }

        /// <summary>Alerta para Farmacia: el stock llegó al mínimo o por debajo.</summary>
        public bool EstaBajoStock
        {
            get { return _stock <= _stockMinimo; }
        }

        // ---------- Concentración, forma farmacéutica y unidad (datos estructurados) ----------

        public const string SinDefinir = "Sin definir";
        public const int MaxConcentracion = 40;

        private string _concentracion = string.Empty;

        /// <summary>Concentración como texto, por ejemplo "500 mg" (opcional, máx. 40 caracteres).</summary>
        public string Concentracion
        {
            get { return _concentracion; }
            set { _concentracion = Validador.TextoOpcional(value, "Concentracion", MaxConcentracion); }
        }

        /// <summary>Carga la concentración tal como está guardada, SIN validar (para leer filas de la base).</summary>
        public static void CargarConcentracion(Medicamento m, string valor)
        {
            m._concentracion = valor ?? string.Empty;
        }

        /// <summary>Forma farmacéutica (null = antiguo sin definir).</summary>
        public int? IdForma { get; set; }
        /// <summary>Unidad de medida (null = antiguo sin definir).</summary>
        public int? IdUnidad { get; set; }
        /// <summary>Nombre de la forma, solo para mostrar (ej. "Tableta").</summary>
        public string FormaNombre { get; set; }
        /// <summary>Nombre de la unidad, solo para mostrar (ej. "Tableta").</summary>
        public string UnidadNombre { get; set; }

        public static bool EsSinDefinir(string nombre)
        {
            return string.IsNullOrWhiteSpace(nombre) || string.Equals(nombre.Trim(), SinDefinir, StringComparison.CurrentCultureIgnoreCase);
        }

        /// <summary>
        /// Nombre completo para listas: "Paracetamol 500 mg - Tableta". Si la forma está "Sin definir" (medicamento antiguo)
        /// se usa el texto de la columna Presentacion, que se conserva por compatibilidad.
        /// </summary>
        public string NombreCompleto
        {
            get { return ComponerNombre(_nombre, _concentracion, FormaNombre, _presentacion); }
        }

        public static string ComponerNombre(string nombre, string concentracion, string forma, string presentacionAntigua)
        {
            string r = nombre ?? string.Empty;
            bool formaDefinida = !EsSinDefinir(forma);
            if (!formaDefinida && string.IsNullOrWhiteSpace(concentracion))
                return string.IsNullOrWhiteSpace(presentacionAntigua) ? r : r + " " + presentacionAntigua.Trim();
            if (!string.IsNullOrWhiteSpace(concentracion)) r += " " + concentracion.Trim();
            if (formaDefinida) r += " - " + forma.Trim();
            return r;
        }

        /// <summary>Stock con su unidad: "10 tabletas" o "1 tableta" (sin unidad definida, solo el número).</summary>
        public string StockTexto
        {
            get { return FormatoCantidad(_stock, UnidadNombre); }
        }

        /// <summary>
        /// "10 tabletas" / "1 tableta". Plural sencillo: termina en vocal → +s (tableta → tabletas); en consonante → +es
        /// (inhalador → inhaladores, unidad → unidades); en "z" → "ces"; en "ión" → "iones"; ya terminada en "s" → igual.
        /// Las abreviaturas (hasta 3 letras o con mayúsculas dentro, como "mL" o "g") no se cambian.
        /// </summary>
        public static string FormatoCantidad(int cantidad, string unidad)
        {
            if (EsSinDefinir(unidad)) return cantidad.ToString();
            string u = unidad.Trim();
            bool abreviatura = u.Length <= 3 || u.Substring(1).Any(char.IsUpper);
            if (abreviatura) return cantidad + " " + u;
            u = char.ToLower(u[0]) + u.Substring(1);
            return cantidad + " " + (cantidad == 1 ? u : Plural(u));
        }

        private static string Plural(string u)
        {
            string baja = u.ToLowerInvariant();
            if (baja.EndsWith("ión")) return u.Substring(0, u.Length - 3) + "iones";
            if (baja.EndsWith("s")) return u;
            if (baja.EndsWith("z")) return u.Substring(0, u.Length - 1) + "ces";
            if ("aeiouáéíóú".IndexOf(baja[baja.Length - 1]) >= 0) return u + "s";
            return u + "es";
        }

        /// <summary>Texto para listas desplegables, por ejemplo "Paracetamol 500 mg - Tableta — stock 10 tabletas".</summary>
        public string DescripcionStock
        {
            get
            {
                if (IdMedicamento == 0) return _nombre; // elemento de relleno "(Sin receta)"
                return NombreCompleto + " — stock " + StockTexto;
            }
        }

        public bool EstaVencido
        {
            get { return FechaVencimiento.HasValue && FechaVencimiento.Value.Date < DateTime.Today; }
        }

        /// <summary>Todavía no venció, pero vence en 30 días o menos (incluye hoy).</summary>
        public bool EstaPorVencer
        {
            get
            {
                if (!FechaVencimiento.HasValue || EstaVencido) return false;
                return (FechaVencimiento.Value.Date - DateTime.Today).Days <= DiasAlertaVencimiento;
            }
        }

        /// <summary>Días que faltan para vencer (negativo si ya venció); null si no tiene fecha.</summary>
        public int? DiasParaVencer
        {
            get
            {
                if (!FechaVencimiento.HasValue) return null;
                return (FechaVencimiento.Value.Date - DateTime.Today).Days;
            }
        }

        public Medicamento()
        {
            Activo = true;
        }

        public Medicamento(string nombre, string presentacion, int stock, int stockMinimo, DateTime? fechaVencimiento)
        {
            Nombre = nombre;
            Presentacion = presentacion;
            Stock = stock;
            StockMinimo = stockMinimo;
            FechaVencimiento = fechaVencimiento;
        }

        public Medicamento(int idMedicamento, string nombre, string presentacion, int stock, int stockMinimo, DateTime? fechaVencimiento)
            : this(nombre, presentacion, stock, stockMinimo, fechaVencimiento)
        {
            IdMedicamento = idMedicamento;
        }

        public void Ingresar(int cantidad)
        {
            Stock = _stock + Validador.Positivo(cantidad, "Cantidad");
        }

        public void Retirar(int cantidad)
        {
            Validador.Positivo(cantidad, "Cantidad");
            if (cantidad > _stock)
                throw new ReglaNegocioException("Stock insuficiente de '" + _nombre + "'. Disponible: " + _stock + ".");
            Stock = _stock - cantidad;
        }
    }
}
