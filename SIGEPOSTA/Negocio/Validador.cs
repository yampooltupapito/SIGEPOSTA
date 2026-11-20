using System;
using System.Text.RegularExpressions;

namespace SIGEPOSTA.Negocio
{
    /// <summary>
    /// Reglas de validación compartidas por todas las entidades.
    /// Decisión de diseño: se centralizan aquí para no repetir la misma lógica
    /// en cada setter y para que un cambio de regla (ej. formato de DNI) se haga en un solo lugar.
    /// Las entidades lanzan ArgumentException; la capa de Presentación la captura y la muestra al usuario.
    /// </summary>
    internal static class Validador
    {
        private static readonly Regex RegexDni = new Regex(@"^\d{8}$", RegexOptions.Compiled);
        private static readonly Regex RegexTelefono = new Regex(@"^\d{6,9}$", RegexOptions.Compiled);

        public static string TextoObligatorio(string valor, string campo, int maxLongitud)
        {
            if (string.IsNullOrWhiteSpace(valor))
                throw new ValidacionException("El campo '" + campo + "' es obligatorio.", campo);
            valor = valor.Trim();
            if (valor.Length > maxLongitud)
                throw new ValidacionException("El campo '" + campo + "' no puede superar " + maxLongitud + " caracteres.", campo);
            return valor;
        }

        public static string TextoOpcional(string valor, string campo, int maxLongitud)
        {
            if (string.IsNullOrWhiteSpace(valor)) return string.Empty;
            valor = valor.Trim();
            if (valor.Length > maxLongitud)
                throw new ValidacionException("El campo '" + campo + "' no puede superar " + maxLongitud + " caracteres.", campo);
            return valor;
        }

        public static string Dni(string valor)
        {
            if (valor == null || !RegexDni.IsMatch(valor.Trim()))
                throw new ValidacionException("El DNI debe tener exactamente 8 dígitos numéricos.", "Dni");
            return valor.Trim();
        }

        public static string Telefono(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) return string.Empty; // el teléfono es opcional
            if (!RegexTelefono.IsMatch(valor.Trim()))
                throw new ValidacionException("El teléfono debe tener entre 6 y 9 dígitos.", "Telefono");
            return valor.Trim();
        }

        // ---------- RUC y correo (proveedores) ----------

        private static readonly Regex RegexCorreo = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$", RegexOptions.Compiled);
        private static readonly int[] PesosRuc = { 5, 4, 3, 2, 7, 6, 5, 4, 3, 2 };
        private static readonly string[] PrefijosRuc = { "10", "15", "17", "20" };

        /// <summary>
        /// Valida el RUC: 11 dígitos, prefijo 10, 15, 17 o 20 y dígito verificador (módulo 11).
        /// Devuelve null si es válido o el mensaje del primer problema (para poder reusarlo sin excepciones).
        /// </summary>
        public static string MensajeRuc(string valor)
        {
            string r = (valor ?? string.Empty).Trim();
            if (r.Length == 0) return "El RUC es obligatorio.";
            if (r.Length != 11 || !EsSoloDigitos(r)) return "El RUC debe tener exactamente 11 dígitos numéricos.";
            if (Array.IndexOf(PrefijosRuc, r.Substring(0, 2)) < 0) return "El RUC debe empezar con 10, 15, 17 o 20.";

            int suma = 0;
            for (int i = 0; i < 10; i++) suma += (r[i] - '0') * PesosRuc[i];
            int digito = 11 - (suma % 11);
            if (digito == 10) digito = 0;
            else if (digito == 11) digito = 1;
            if (digito != r[10] - '0') return "El RUC no es válido (el dígito verificador no coincide).";
            return null;
        }

        public static string Ruc(string valor)
        {
            string mensaje = MensajeRuc(valor);
            if (mensaje != null) throw new ValidacionException(mensaje, "Ruc");
            return valor.Trim();
        }

        /// <summary>Correo opcional: si se escribe, debe tener formato usuario@dominio.ext.</summary>
        public static string Correo(string valor, int maxLongitud)
        {
            if (string.IsNullOrWhiteSpace(valor)) return string.Empty;
            valor = valor.Trim();
            if (valor.Length > maxLongitud)
                throw new ValidacionException("El campo 'Correo' no puede superar " + maxLongitud + " caracteres.", "Correo");
            if (!RegexCorreo.IsMatch(valor))
                throw new ValidacionException("Ingrese un correo válido (ejemplo: ventas@empresa.com).", "Correo");
            return valor;
        }

        // ---------- CIE-10 ----------

        private static readonly Regex RegexCie10 = new Regex(@"^[A-Z]\d{2}(\.[A-Z0-9]{1,2})?$", RegexOptions.Compiled);

        /// <summary>Normaliza (recorta y pasa a mayúsculas). Devuelve vacío si no hay texto.</summary>
        public static string NormalizarCie10(string valor)
        {
            return string.IsNullOrWhiteSpace(valor) ? string.Empty : valor.Trim().ToUpperInvariant();
        }

        /// <summary>Formato CIE-10: una letra, 2 dígitos y, opcional, punto y 1 o 2 caracteres (J06, J06.9, S72.00).</summary>
        public static bool EsCodigoCie10(string valor)
        {
            return RegexCie10.IsMatch(NormalizarCie10(valor));
        }

        /// <summary>Valida y devuelve el código normalizado (mayúsculas). El mensaje NO incluye el valor escrito.</summary>
        public static string Cie10Codigo(string valor, string campo)
        {
            string c = NormalizarCie10(valor);
            if (c.Length == 0) throw new ValidacionException("El código CIE-10 es obligatorio.", campo);
            if (!RegexCie10.IsMatch(c))
                throw new ValidacionException("El código CIE-10 debe tener una letra, dos dígitos y, si lleva, un punto con 1 o 2 caracteres (ejemplo: J06.9).", campo);
            return c;
        }

        private static bool EsSoloDigitos(string t)
        {
            foreach (char c in t)
            {
                if (c < '0' || c > '9') return false;
            }
            return true;
        }

        public static int NoNegativo(int valor, string campo)
        {
            if (valor < 0)
                throw new ValidacionException("El campo '" + campo + "' no puede ser negativo.", campo);
            return valor;
        }

        public static int Positivo(int valor, string campo)
        {
            if (valor <= 0)
                throw new ValidacionException("El campo '" + campo + "' debe ser mayor que cero.", campo);
            return valor;
        }

        public static DateTime FechaNoFutura(DateTime valor, string campo)
        {
            if (valor.Date > DateTime.Today)
                throw new ValidacionException("El campo '" + campo + "' no puede ser una fecha futura.", campo);
            return valor;
        }
    }
}
