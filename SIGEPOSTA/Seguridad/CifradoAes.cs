using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SIGEPOSTA.Seguridad
{
    /// <summary>
    /// Cifrado simétrico AES-256 (modo CBC) con CryptoStream, para el DNI y el diagnóstico.
    /// Decisiones de diseño:
    ///  - IV aleatorio de 16 bytes POR CADA DATO: cifrar dos veces el mismo texto da resultados distintos,
    ///    así no se pueden deducir patrones ni repeticiones.
    ///  - El IV no es secreto: se guarda pegado al inicio del cifrado (IV + datos).
    ///  - Hay dos formatos: bytes (para columnas VARBINARY) y Base64 (para texto).
    ///  - Como el cifrado no es determinista, NO sirve para buscar: para eso está HmacDni.
    /// </summary>
    public static class CifradoAes
    {
        private const int TamanoIv = 16;

        /// <summary>Cifra y devuelve IV + datos como bytes (para columnas VARBINARY).</summary>
        public static byte[] CifrarABytes(string textoPlano)
        {
            if (textoPlano == null) throw new ArgumentNullException("textoPlano");

            byte[] llave = GestorLlaves.ObtenerLlaveAes();
            try
            {
                using (Aes aes = Aes.Create())
                {
                    aes.KeySize = 256;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Key = llave;
                    aes.GenerateIV(); // IV nuevo y aleatorio para este dato

                    using (MemoryStream salida = new MemoryStream())
                    {
                        salida.Write(aes.IV, 0, aes.IV.Length); // el IV va al inicio
                        using (ICryptoTransform cifrador = aes.CreateEncryptor())
                        using (CryptoStream cs = new CryptoStream(salida, cifrador, CryptoStreamMode.Write))
                        {
                            byte[] datos = Encoding.UTF8.GetBytes(textoPlano);
                            cs.Write(datos, 0, datos.Length);
                            cs.FlushFinalBlock();
                        }
                        return salida.ToArray();
                    }
                }
            }
            finally
            {
                Array.Clear(llave, 0, llave.Length);
            }
        }

        /// <summary>Descifra bytes en formato IV + datos.</summary>
        public static string DescifrarDeBytes(byte[] todo)
        {
            if (todo == null || todo.Length <= TamanoIv)
                throw new CryptographicException("El dato cifrado es inválido.");

            byte[] iv = new byte[TamanoIv];
            Buffer.BlockCopy(todo, 0, iv, 0, TamanoIv);

            byte[] llave = GestorLlaves.ObtenerLlaveAes();
            try
            {
                using (Aes aes = Aes.Create())
                {
                    aes.KeySize = 256;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Key = llave;
                    aes.IV = iv;

                    using (MemoryStream entrada = new MemoryStream(todo, TamanoIv, todo.Length - TamanoIv))
                    using (ICryptoTransform descifrador = aes.CreateDecryptor())
                    using (CryptoStream cs = new CryptoStream(entrada, descifrador, CryptoStreamMode.Read))
                    using (StreamReader lector = new StreamReader(cs, Encoding.UTF8))
                    {
                        return lector.ReadToEnd();
                    }
                }
            }
            finally
            {
                Array.Clear(llave, 0, llave.Length);
            }
        }

        public static string Cifrar(string textoPlano)
        {
            return Convert.ToBase64String(CifrarABytes(textoPlano));
        }

        public static string Descifrar(string textoCifrado)
        {
            if (string.IsNullOrEmpty(textoCifrado))
                throw new ArgumentException("No hay texto cifrado para descifrar.", "textoCifrado");
            return DescifrarDeBytes(Convert.FromBase64String(textoCifrado));
        }
    }
}
