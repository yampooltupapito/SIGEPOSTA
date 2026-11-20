namespace SIGEPOSTA.Negocio
{
    /// <summary>Roles del sistema. Un enum evita "strings mágicos" y errores de tipeo al validar permisos.</summary>
    public enum Rol
    {
        Administrador = 1,
        Medico = 2,
        Farmacia = 3
    }

    /// <summary>Tipo de movimiento de inventario (coincide con los textos Ingreso/Salida de la BD).</summary>
    public enum TipoMovimiento
    {
        Ingreso = 1,
        Salida = 2
    }
}
