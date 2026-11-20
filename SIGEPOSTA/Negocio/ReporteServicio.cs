using System;
using System.Collections.Generic;
using System.Linq;
using SIGEPOSTA.Datos;

namespace SIGEPOSTA.Negocio
{
    /// <summary>
    /// Reportes. Cada uno valida el permiso sobre SU dato (no solo la pantalla):
    ///  - Pacientes atendidos por día → Administrador y Médico.
    ///  - Stock crítico / por vencer y movimientos de inventario → Administrador y Farmacia.
    /// Las agrupaciones, conteos y ordenamientos se hacen con LINQ sobre List&lt;T&gt;.
    /// Como el sistema es de un solo establecimiento, ningún reporte agrupa por posta: todos filtran por rango de fechas.
    /// </summary>
    public class ReporteServicio
    {
        private readonly ReporteDatos _datos;
        private readonly InventarioServicio _inventario;
        private readonly Cie10Datos _cie10;

        public ReporteServicio() : this(new ReporteDatos(), new InventarioServicio()) { }

        public ReporteServicio(ReporteDatos datos, InventarioServicio inventario) : this(datos, inventario, new Cie10Datos()) { }

        public ReporteServicio(ReporteDatos datos, InventarioServicio inventario, Cie10Datos cie10)
        {
            if (cie10 == null) throw new ArgumentNullException("cie10");
            _cie10 = cie10;
            if (datos == null) throw new ArgumentNullException("datos");
            if (inventario == null) throw new ArgumentNullException("inventario");
            _datos = datos;
            _inventario = inventario;
        }

        /// <summary>Pacientes distintos atendidos y número de atenciones POR DÍA, en el rango de fechas (inclusive).</summary>
        public ReporteTabla PacientesAtendidosPorDia(DateTime desde, DateTime hasta)
        {
            Permisos.ExigirAccesoPacientes();
            ValidarRango(desde, hasta);

            List<AtencionReporteFila> filas = _datos.ListarAtenciones(desde.Date, hasta.Date.AddDays(1));

            ReporteTabla t = new ReporteTabla("Pacientes atendidos por día",
                new[] { "Fecha", "Pacientes atendidos", "Atenciones" });

            var porDia = filas
                .GroupBy(f => f.Fecha.Date)
                .Select(g => new
                {
                    Dia = g.Key,
                    Pacientes = g.Select(x => x.IdPaciente).Distinct().Count(),
                    Atenciones = g.Count()
                })
                .OrderBy(x => x.Dia)
                .ToList();

            foreach (var x in porDia)
                t.Filas.Add(new[] { x.Dia.ToString("dd/MM/yyyy"), x.Pacientes.ToString(), x.Atenciones.ToString() });

            int pacientesTotal = filas.Select(f => f.IdPaciente).Distinct().Count();
            t.Filas.Add(new[] { "TOTAL", pacientesTotal.ToString(), filas.Count.ToString() });
            t.Resumen = "Del " + desde.ToString("dd/MM/yyyy") + " al " + hasta.ToString("dd/MM/yyyy") + ": " +
                        pacientesTotal + " paciente(s) distinto(s) y " + filas.Count + " atención(es) en " + porDia.Count + " día(s) con atenciones.";
            return t;
        }

        /// <summary>
        /// DIAGNÓSTICOS MÁS FRECUENTES (CIE-10) en el rango de fechas (inclusive). Administrador y Médico.
        /// Por qué NO se calcula en SQL: el código se guarda CIFRADO con AES y un IV aleatorio, así que dos atenciones con
        /// el mismo código producen bytes distintos y GROUP BY / COUNT no podrían agruparlas. Por eso se traen las atenciones
        /// del rango, se descifra cada código en memoria y se agrupa con LINQ (GroupBy/Count/OrderByDescending).
        /// Los códigos ilegibles y las atenciones antiguas sin código se cuentan aparte, sin romper el reporte.
        /// </summary>
        public ReporteTabla DiagnosticosMasFrecuentes(DateTime desde, DateTime hasta)
        {
            Permisos.ExigirAccesoPacientes();
            ValidarRango(desde, hasta);

            List<DiagnosticoReporteFila> filas = _datos.ListarDiagnosticos(desde.Date, hasta.Date.AddDays(1));

            int sinCodigo = 0, ilegibles = 0;
            List<string> codigos = new List<string>();
            foreach (DiagnosticoReporteFila f in filas)
            {
                bool ilegible;
                string c = Cie10Servicio.DescifrarCodigo(f.Cie10Cifrado, out ilegible);
                if (c != null) codigos.Add(c);
                else if (ilegible) ilegibles++;
                else sinCodigo++;
            }

            Dictionary<string, string> descripciones = new Dictionary<string, string>();
            if (codigos.Count > 0)
            {
                foreach (Cie10 c in _cie10.ListarTodos()) descripciones[c.Codigo] = c.Descripcion;
            }

            var grupos = codigos
                .GroupBy(c => c)
                .Select(g => new { Codigo = g.Key, Cantidad = g.Count() })
                .OrderByDescending(x => x.Cantidad)
                .ThenBy(x => x.Codigo, StringComparer.Ordinal)
                .ToList();

            ReporteTabla t = new ReporteTabla("Diagnósticos más frecuentes",
                new[] { "N.º", "Código", "Descripción", "Atenciones", "%" });
            int puesto = 0;
            foreach (var g in grupos)
            {
                puesto++;
                string desc;
                if (!descripciones.TryGetValue(g.Codigo, out desc)) desc = "(no figura en el catálogo)";
                double pct = codigos.Count == 0 ? 0 : 100.0 * g.Cantidad / codigos.Count;
                t.Filas.Add(new[] { puesto.ToString(), g.Codigo, desc, g.Cantidad.ToString(), pct.ToString("0.0") + " %" });
            }

            t.Resumen = "Del " + desde.ToString("dd/MM/yyyy") + " al " + hasta.ToString("dd/MM/yyyy") + ": " + filas.Count +
                        " atención(es); " + codigos.Count + " con código CIE-10 (" + grupos.Count + " código(s) distinto(s)), " +
                        sinCodigo + " sin código (atenciones antiguas)" +
                        (ilegibles > 0 ? " y " + ilegibles + " con código ilegible" : string.Empty) + ".";
            return t;
        }
        /// <summary>Medicamentos con stock bajo, por vencer (30 días o menos) o vencidos.</summary>
        public ReporteTabla MedicamentosCriticos()
        {
            Permisos.ExigirAccesoInventario();

            List<Medicamento> criticos = _inventario.ObtenerCriticos();
            ReporteTabla t = new ReporteTabla("Medicamentos con stock crítico o por vencer",
                new[] { "Medicamento", "Stock", "Mínimo", "Vence", "Días para vencer", "Alerta" });

            foreach (Medicamento m in criticos)
            {
                t.Filas.Add(new[]
                {
                    m.NombreCompleto,
                    m.StockTexto,
                    m.StockMinimo.ToString(),
                    m.FechaVencimiento.HasValue ? m.FechaVencimiento.Value.ToString("dd/MM/yyyy") : string.Empty,
                    m.DiasParaVencer.HasValue ? m.DiasParaVencer.Value.ToString() : string.Empty,
                    InventarioServicio.TextoAlerta(m)
                });
            }

            t.Resumen = criticos.Count + " medicamento(s) crítico(s): " +
                        criticos.Count(m => m.EstaVencido) + " vencido(s), " +
                        criticos.Count(m => m.EstaPorVencer) + " por vencer, " +
                        criticos.Count(m => m.EstaBajoStock) + " con stock bajo.";
            return t;
        }

        /// <summary>Movimientos de inventario (ingresos y salidas) en el rango de fechas (inclusive), del más reciente al más antiguo.</summary>
        public ReporteTabla MovimientosPorFecha(DateTime desde, DateTime hasta)
        {
            Permisos.ExigirAccesoInventario();
            ValidarRango(desde, hasta);

            List<MovimientoReporteFila> filas = _datos.ListarMovimientos(desde.Date, hasta.Date.AddDays(1))
                .OrderByDescending(f => f.Fecha)
                .ToList();

            ReporteTabla t = new ReporteTabla("Movimientos de inventario",
                new[] { "Fecha", "Medicamento", "Tipo", "Cantidad", "Motivo", "Usuario" });

            foreach (MovimientoReporteFila f in filas)
            {
                t.Filas.Add(new[]
                {
                    f.Fecha.ToString("dd/MM/yyyy HH:mm"),
                    Medicamento.ComponerNombre(f.Medicamento, f.Concentracion, f.Forma, f.Presentacion),
                    f.Tipo.ToString(),
                    Medicamento.FormatoCantidad(f.Cantidad, f.Unidad),
                    f.Motivo,
                    f.Usuario
                });
            }

            int ingresos = filas.Where(f => f.Tipo == TipoMovimiento.Ingreso).Sum(f => f.Cantidad);
            int salidas = filas.Where(f => f.Tipo == TipoMovimiento.Salida).Sum(f => f.Cantidad);
            t.Resumen = "Del " + desde.ToString("dd/MM/yyyy") + " al " + hasta.ToString("dd/MM/yyyy") + ": " +
                        filas.Count + " movimiento(s). Ingresaron " + ingresos + " unidad(es) y salieron " + salidas + ".";
            return t;
        }

        private static void ValidarRango(DateTime desde, DateTime hasta)
        {
            if (desde.Date > hasta.Date)
                throw new ValidacionException("La fecha 'Desde' no puede ser posterior a 'Hasta'.", "Desde");
        }
    }
}
