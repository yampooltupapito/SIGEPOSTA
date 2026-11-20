using System;
using System.Security.Cryptography;

namespace SIGEPOSTA.Seguridad
{
    /// <summary>
    /// Hash de contraseñas con PBKDF2 (Rfc2898DeriveBytes + SHA-256).
    /// Decisiones de diseño:
    ///  - Salt aleatorio de 16 bytes por contraseña: dos usuarios con la misma clave tienen hashes distintos.
    ///  - 100000 iteraciones: encarece los ataques de fuerza bruta.
    ///  - Hash de 32 bytes (256 bits).
    ///  - El resultado se guarda como "iteraciones.salt.hash" (Base64), así si en el futuro se suben
    ///    las iteraciones, las contraseñas antiguas se siguen pudiendo verificar.
    /// </summary>
    public static class HashContrasena
    {
        private const int TamanoSalt = 16;
        private const int TamanoHash = 32;
        public const int Iteraciones = 100000;

        /// <summary>
        /// Partes del hash por separado, tal como se guardan en la tabla Usuario
        /// (PasswordHash VARBINARY(32), Salt VARBINARY(16), Iteraciones INT).
        /// </summary>
        public sealed class DatosHash
        {
            public byte[] Hash { get; set; }
            public byte[] Salt { get; set; }
            public int Iteraciones { get; set; }
        }

        /// <summary>Genera hash + salt + iteraciones para guardarlos en columnas separadas.</summary>
        public static DatosHash GenerarPartes(string contrasena)
        {
            if (string.IsNullOrEmpty(contrasena))
                throw new ArgumentException("La contraseña no puede estar vacía.", "contrasena");

            byte[] salt = new byte[TamanoSalt];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            return new DatosHash
            {
                Salt = salt,
                Iteraciones = Iteraciones,
                Hash = Derivar(contrasena, salt, Iteraciones, TamanoHash)
            };
        }

        /// <summary>Verifica una contraseña contra las partes guardadas, con comparación en tiempo constante.</summary>
        public static bool Verificar(string contrasena, byte[] hashGuardado, byte[] salt, int iteraciones)
        {
            if (string.IsNullOrEmpty(contrasena) || hashGuardado == null || salt == null || iteraciones <= 0)
                return false;
            byte[] calculado = Derivar(contrasena, salt, iteraciones, hashGuardado.Length);
            return ComparacionConstante(hashGuardado, calculado);
        }

        /// <summary>Genera el texto a guardar en la BD para una contraseña.</summary>
        public static string Generar(string contrasena)
        {
            if (string.IsNullOrEmpty(contrasena))
                throw new ArgumentException("La contraseña no puede estar vacía.", "contrasena");

            byte[] salt = new byte[TamanoSalt];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            byte[] hash = Derivar(contrasena, salt, Iteraciones, TamanoHash);
            return Iteraciones + "." + Convert.ToBase64String(salt) + "." + Convert.ToBase64String(hash);
        }

        /// <summary>Verifica una contraseña contra el texto guardado. Nunca lanza excepción por formato inválido.</summary>
        public static bool Verificar(string contrasena, string almacenado)
        {
            if (string.IsNullOrEmpty(contrasena) || string.IsNullOrEmpty(almacenado))
                return false;

            string[] partes = almacenado.Split('.');
            if (partes.Length != 3) return false;

            int iteraciones;
            if (!int.TryParse(partes[0], out iteraciones) || iteraciones <= 0) return false;

            try
            {
                byte[] salt = Convert.FromBase64String(partes[1]);
                byte[] hashEsperado = Convert.FromBase64String(partes[2]);
                byte[] hashCalculado = Derivar(contrasena, salt, iteraciones, hashEsperado.Length);
                return ComparacionConstante(hashEsperado, hashCalculado);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static byte[] Derivar(string contrasena, byte[] salt, int iteraciones, int longitud)
        {
            using (Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(contrasena, salt, iteraciones, HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(longitud);
            }
        }

        /// <summary>
        /// Comparación en tiempo constante: no se detiene en el primer byte distinto,
        /// así el tiempo de respuesta no revela cuántos bytes coinciden (evita ataques de temporización).
        /// </summary>
        public static bool ComparacionConstante(byte[] a, byte[] b)
        {
            if (a == null || b == null) return false;
            uint diferencia = (uint)a.Length ^ (uint)b.Length;
            int max = Math.Min(a.Length, b.Length);
            for (int i = 0; i < max; i++)
            {
                diferencia |= (uint)(a[i] ^ b[i]);
            }
            return diferencia == 0;
        }
    }
}
