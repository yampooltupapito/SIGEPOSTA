using System;
using System.Collections.Generic;
using System.Globalization;

namespace SIGEPOSTA.Negocio
{
    /// <summary>Resultado de la ayuda visual: Normal (sin color), Ambar (atención) o Rojo (muy fuera de lo habitual).</summary>
    public enum NivelSigno
    {
        Normal,
        Ambar,
        Rojo
    }

    /// <summary>
    /// Signos vitales de una atención. Todos son opcionales (null = no se midió).
    /// Decisiones de diseño:
    ///  - Los RANGOS PERMITIDOS están todos en este archivo (constantes Min/Max). Deben coincidir con los CHECK de la
    ///    tabla Atencion; si cambian en la base de datos, se cambian aquí y en ningún otro lugar.
    ///  - Los umbrales de la AYUDA VISUAL (ámbar / rojo) son otra cosa: señalan valores fuera de lo habitual para un
    ///    adulto y son solo una ayuda para quien atiende. NO son un diagnóstico y no reemplazan el criterio clínico.
    ///  - El IMC se calcula (peso / talla²) y no se guarda: se obtiene de los otros dos datos.
    /// </summary>
    public class SignosVitales
    {
        // ----- Rangos permitidos (iguales a los CHECK de la base de datos) -----
        public const decimal PesoMin = 0.5m, PesoMax = 500m;                 // kg
        public const decimal TallaMin = 20m, TallaMax = 250m;                // cm
        public const int SistolicaMin = 40, SistolicaMax = 300;              // mmHg
        public const int DiastolicaMin = 20, DiastolicaMax = 200;            // mmHg
        public const decimal TemperaturaMin = 30m, TemperaturaMax = 45m;     // °C
        public const int FrecuenciaMin = 20, FrecuenciaMax = 300;            // latidos por minuto
        public const int SaturacionMin = 50, SaturacionMax = 100;            // %

        public decimal? PesoKg { get; set; }
        public decimal? TallaCm { get; set; }
        public int? PresionSistolica { get; set; }
        public int? PresionDiastolica { get; set; }
        public decimal? Temperatura { get; set; }
        public int? FrecuenciaCardiaca { get; set; }
        public int? SaturacionO2 { get; set; }

        /// <summary>True si se registró al menos un signo vital.</summary>
        public bool TieneDatos
        {
            get
            {
                return PesoKg.HasValue || TallaCm.HasValue || PresionSistolica.HasValue || PresionDiastolica.HasValue ||
                       Temperatura.HasValue || FrecuenciaCardiaca.HasValue || SaturacionO2.HasValue;
            }
        }

        // ================== IMC ==================

        /// <summary>Índice de masa corporal (kg/m²) con 1 decimal; null si falta el peso o la talla.</summary>
        public decimal? Imc
        {
            get { return CalcularImc(PesoKg, TallaCm); }
        }

        public static decimal? CalcularImc(decimal? pesoKg, decimal? tallaCm)
        {
            if (!pesoKg.HasValue || !tallaCm.HasValue || tallaCm.Value <= 0) return null;
            decimal metros = tallaCm.Value / 100m;
            return Math.Round(pesoKg.Value / (metros * metros), 1, MidpointRounding.AwayFromZero);
        }

        /// <summary>Clasificación orientativa del IMC (criterio de adultos, OMS).</summary>
        public static string ClasificarImc(decimal imc)
        {
            if (imc < 18.5m) return "bajo peso";
            if (imc < 25m) return "normal";
            if (imc < 30m) return "sobrepeso";
            return "obesidad";
        }

        // ================== Validación (mismos rangos que la base) ==================

        /// <summary>
        /// Valida los rangos. Devuelve campo → mensaje (vacío si todo está bien).
        /// Claves: PesoKg, TallaCm, PresionSistolica, PresionDiastolica, Temperatura, FrecuenciaCardiaca, SaturacionO2.
        /// </summary>
        public Dictionary<string, string> Validar()
        {
            Dictionary<string, string> e = new Dictionary<string, string>();

            if (PesoKg.HasValue && (PesoKg.Value < PesoMin || PesoKg.Value > PesoMax))
                e["PesoKg"] = "El peso debe estar entre " + Formato(PesoMin) + " y " + Formato(PesoMax) + " kg.";
            if (TallaCm.HasValue && (TallaCm.Value < TallaMin || TallaCm.Value > TallaMax))
                e["TallaCm"] = "La talla debe estar entre " + Formato(TallaMin) + " y " + Formato(TallaMax) + " cm.";

            if (PresionSistolica.HasValue && (PresionSistolica.Value < SistolicaMin || PresionSistolica.Value > SistolicaMax))
                e["PresionSistolica"] = "La sistólica debe estar entre " + SistolicaMin + " y " + SistolicaMax + " mmHg.";
            if (PresionDiastolica.HasValue && (PresionDiastolica.Value < DiastolicaMin || PresionDiastolica.Value > DiastolicaMax))
                e["PresionDiastolica"] = "La diastólica debe estar entre " + DiastolicaMin + " y " + DiastolicaMax + " mmHg.";

            // La presión se registra completa: sistólica y diastólica juntas, y la sistólica mayor que la diastólica.
            if (PresionSistolica.HasValue != PresionDiastolica.HasValue)
            {
                string falta = PresionSistolica.HasValue ? "PresionDiastolica" : "PresionSistolica";
                if (!e.ContainsKey(falta))
                    e[falta] = PresionSistolica.HasValue ? "Ingrese también la diastólica." : "Ingrese también la sistólica.";
            }
            else if (PresionSistolica.HasValue && !e.ContainsKey("PresionSistolica") && !e.ContainsKey("PresionDiastolica") &&
                     PresionSistolica.Value <= PresionDiastolica.Value)
            {
                e["PresionSistolica"] = "La sistólica debe ser mayor que la diastólica.";
            }

            if (Temperatura.HasValue && (Temperatura.Value < TemperaturaMin || Temperatura.Value > TemperaturaMax))
                e["Temperatura"] = "La temperatura debe estar entre " + Formato(TemperaturaMin) + " y " + Formato(TemperaturaMax) + " °C.";
            if (FrecuenciaCardiaca.HasValue && (FrecuenciaCardiaca.Value < FrecuenciaMin || FrecuenciaCardiaca.Value > FrecuenciaMax))
                e["FrecuenciaCardiaca"] = "La frecuencia debe estar entre " + FrecuenciaMin + " y " + FrecuenciaMax + " lpm.";
            if (SaturacionO2.HasValue && (SaturacionO2.Value < SaturacionMin || SaturacionO2.Value > SaturacionMax))
                e["SaturacionO2"] = "La saturación debe estar entre " + SaturacionMin + " y " + SaturacionMax + " %.";
            return e;
        }

        // ================== Ayuda visual (NO es un diagnóstico) ==================

        public static NivelSigno NivelTemperatura(decimal t)
        {
            if (t >= 39m || t < 35m) return NivelSigno.Rojo;
            if (t > 37.5m || t < 36m) return NivelSigno.Ambar;
            return NivelSigno.Normal;
        }

        public static NivelSigno NivelSaturacion(int s)
        {
            if (s < 90) return NivelSigno.Rojo;
            if (s < 92) return NivelSigno.Ambar;
            return NivelSigno.Normal;
        }

        public static NivelSigno NivelFrecuencia(int fc)
        {
            if (fc < 40 || fc > 130) return NivelSigno.Rojo;
            if (fc < 60 || fc > 100) return NivelSigno.Ambar;
            return NivelSigno.Normal;
        }

        /// <summary>Nivel de la presión arterial (usa las dos cifras; vale el peor de los dos).</summary>
        public static NivelSigno NivelPresion(int sistolica, int diastolica)
        {
            if (sistolica >= 180 || diastolica >= 120 || sistolica < 80) return NivelSigno.Rojo;
            if (sistolica >= 140 || diastolica >= 90 || sistolica < 90) return NivelSigno.Ambar;
            return NivelSigno.Normal;
        }

        public static NivelSigno NivelImc(decimal imc)
        {
            if (imc < 16m || imc >= 35m) return NivelSigno.Rojo;
            if (imc < 18.5m || imc >= 25m) return NivelSigno.Ambar;
            return NivelSigno.Normal;
        }

        // ================== Texto ==================

        /// <summary>
        /// Resumen en una línea para listas y respaldos, ej.: "PA 120/80 · T 36.5 °C · FC 72 lpm · SatO₂ 98 % · 70 kg · 170 cm · IMC 24.2".
        /// Vacío si no hay datos.
        /// </summary>
        public string Resumen()
        {
            List<string> p = new List<string>();
            if (PresionSistolica.HasValue && PresionDiastolica.HasValue) p.Add("PA " + PresionSistolica.Value + "/" + PresionDiastolica.Value);
            if (Temperatura.HasValue) p.Add("T " + Formato(Temperatura.Value) + " °C");
            if (FrecuenciaCardiaca.HasValue) p.Add("FC " + FrecuenciaCardiaca.Value + " lpm");
            if (SaturacionO2.HasValue) p.Add("SatO₂ " + SaturacionO2.Value + " %");
            if (PesoKg.HasValue) p.Add(Formato(PesoKg.Value) + " kg");
            if (TallaCm.HasValue) p.Add(Formato(TallaCm.Value) + " cm");
            decimal? imc = Imc;
            if (imc.HasValue) p.Add("IMC " + Formato(imc.Value));
            return string.Join(" · ", p);
        }

        /// <summary>Número con punto decimal y sin ceros sobrantes (70, 36.5), sin depender de la configuración regional.</summary>
        private static string Formato(decimal valor)
        {
            return valor.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
