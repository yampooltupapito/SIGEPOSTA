using System;
using System.IO;
using System.IO.IsolatedStorage;
using System.Security.Cryptography;

namespace SIGEPOSTA.Seguridad
{
    /// <summary>
    /// Administra las llaves criptográficas. La llave NUNCA está escrita en el código.
    /// Decisiones de diseño:
    ///  - En el primer uso se generan al azar 64 bytes: 32 para AES y 32 para HMAC.
    ///    Son llaves distintas a propósito: una sola llave para dos fines debilita ambos.
    ///  - Se protegen con DPAPI (ProtectedData, ámbito CurrentUser): solo el mismo usuario de Windows
    ///    en la misma máquina puede descifrarlas.
    ///  - Se guardan en almacenamiento aislado (IsolatedStorage) del usuario y del ensamblado.
    ///  - ADVERTENCIA: si se pierde este archivo o se cambia de usuario/equipo, los datos cifrados
    ///    ya no se podrán recuperar. Hay que respaldar la llave antes de pasar a producción.
    /// </summary>
    public static class GestorLlaves
    {
        private const string NombreArchivo = "sigeposta.keystore";
        // Entropía adicional para DPAPI: un "segundo secreto" de la aplicación (no es la llave).
        private static readonly byte[] EntropiaApp = System.Text.Encoding.UTF8.GetBytes("SIGEPOSTA.v1.KeyStore");

        private static readonly object Candado = new object();
        private static byte[] _llaveAes;
        private static byte[] _llaveHmac;

        /// <summary>Llave AES-256 (32 bytes). Se devuelve una copia para que nadie modifique la original.</summary>
        public static byte[] ObtenerLlaveAes()
        {
            Cargar();
            return (byte[])_llaveAes.Clone();
        }

        /// <summary>Llave HMAC (32 bytes).</summary>
        public static byte[] ObtenerLlaveHmac()
        {
            Cargar();
            return (byte[])_llaveHmac.Clone();
        }

        private static void Cargar()
        {
            if (_llaveAes != null && _llaveHmac != null) return;

            lock (Candado)
            {
                if (_llaveAes != null && _llaveHmac != null) return;

                byte[] material;
                using (IsolatedStorageFile almacen = IsolatedStorageFile.GetUserStoreForAssembly())
                {
                    if (almacen.FileExists(NombreArchivo))
                    {
                        byte[] protegido = LeerTodo(almacen);
                        material = ProtectedData.Unprotect(protegido, EntropiaApp, DataProtectionScope.CurrentUser);
                    }
                    else
                    {
                        material = new byte[64];
                        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                        {
                            rng.GetBytes(material);
                        }
                        byte[] protegido = ProtectedData.Protect(material, EntropiaApp, DataProtectionScope.CurrentUser);
                        using (IsolatedStorageFileStream fs = new IsolatedStorageFileStream(NombreArchivo, FileMode.CreateNew, FileAccess.Write, almacen))
                        {
                            fs.Write(protegido, 0, protegido.Length);
                        }
                    }
                }

                if (material.Length != 64)
                    throw new CryptographicException("El almacén de llaves está dañado.");

                byte[] aes = new byte[32];
                byte[] hmac = new byte[32];
                Buffer.BlockCopy(material, 0, aes, 0, 32);
                Buffer.BlockCopy(material, 32, hmac, 0, 32);
                Array.Clear(material, 0, material.Length); // no dejar el material en memoria más de lo necesario

                _llaveAes = aes;
                _llaveHmac = hmac;
            }
        }

        private static byte[] LeerTodo(IsolatedStorageFile almacen)
        {
            using (IsolatedStorageFileStream fs = new IsolatedStorageFileStream(NombreArchivo, FileMode.Open, FileAccess.Read, almacen))
            using (MemoryStream ms = new MemoryStream())
            {
                fs.CopyTo(ms);
                return ms.ToArray();
            }
        }
    }
}
