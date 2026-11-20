using System;

namespace SIGEPOSTA.Negocio
{
    /// <summary>
    /// Registro histórico de una entrada o salida de medicamento (kardex).
    /// Decisión de diseño: cada cambio de stock deja un movimiento con fecha y usuario,
    /// lo que da trazabilidad y auditoría.
    /// </summary>
    public class MovimientoInventario
    {
        private int _cantidad = 1;
        private string _motivo = string.Empty;

        public int IdMovimiento { get; set; }
        public int IdMedicamento { get; set; }
        public int IdUsuario { get; set; }
        /// <summary>Nombre del usuario que hizo el movimiento, solo para mostrar.</summary>
        public string NombreUsuario { get; set; }
        public TipoMovimiento Tipo { get; set; }
        public DateTime Fecha { get; set; }

        /// <summary>Proveedor del ingreso (null en las salidas y en movimientos antiguos).</summary>
        public int? IdProveedor { get; set; }
        /// <summary>Nombre del proveedor, solo para mostrar.</summary>
        public string NombreProveedor { get; set; }

        /// <summary>N.º de guía o factura del ingreso (vacío en las salidas).</summary>
        public string NumeroDocumento
        {
            get { return _numeroDocumento; }
            set { _numeroDocumento = Validador.TextoOpcional(value, "NumeroDocumento", MaxNumeroDocumento); }
        }

        public const int MaxNumeroDocumento = 30;
        private string _numeroDocumento = string.Empty;

        public int Cantidad
        {
            get { return _cantidad; }
            set { _cantidad = Validador.Positivo(value, "Cantidad"); }
        }

        public string Motivo
        {
            get { return _motivo; }
            set { _motivo = Validador.TextoOpcional(value, "Motivo", 200); }
        }

        public MovimientoInventario()
        {
            Fecha = DateTime.Now;
        }

        public MovimientoInventario(int idMedicamento, int idUsuario, TipoMovimiento tipo, int cantidad, string motivo)
        {
            if (idMedicamento <= 0) throw new ValidacionException("Medicamento inválido.", "idMedicamento");
            if (idUsuario <= 0) throw new ValidacionException("Usuario inválido.", "idUsuario");
            IdMedicamento = idMedicamento;
            IdUsuario = idUsuario;
            Tipo = tipo;
            Cantidad = cantidad;
            Motivo = motivo;
            Fecha = DateTime.Now;
        }
    }
}
