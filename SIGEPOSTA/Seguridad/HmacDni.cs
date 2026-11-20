using System;
using System.Security.Cryptography;
using System.Text;

namespace SIGEPOSTA.Seguridad
{
    /// <summary>
    /// HMAC-SHA256 del DNI en hexadecimal (64 caracteres).
    /// Decisión de diseño: el DNI se guarda cifrado (AES, no determinista), por lo que no se puede
    /// buscar con WHERE. Este "índice ciego" sí es determinista (mismo DNI = mismo HMAC) pero no se
    /// puede revertir sin la llave, y se usa una llave distinta a la de AES.
    /// </summary>
    public static class HmacDni
    {
        public static string Calcular(string dni)
        {
            if (string.IsNullOrWhiteSpace(dni))
                throw new ArgumentException("El DNI es obligatorio.", "dni");

            byte[] llave = GestorLlaves.ObtenerLlaveHmac();
            try
            {
                using (HMACSHA256 hmac = new HMACSHA256(llave))
                {
                    byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(dni.Trim()));
                    StringBuilder sb = new StringBuilder(64);
                    foreach (byte b in hash)
                    {
                        sb.Append(b.ToString("x2")); // minúsculas, 2 caracteres por byte = 64 en total
                    }
                    return sb.ToString();
                }
            }
            finally
            {
                Array.Clear(llave, 0, llave.Length);
            }
        }
    }
}
