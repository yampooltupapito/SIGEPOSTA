using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace SIGEPOSTA.Seguridad
{
    /// <summary>El archivo de respaldo no se pudo verificar o leer. El mensaje es apto para mostrarse al usuario.</summary>
    public class RespaldoInvalidoException : Exception
    {
        public RespaldoInvalidoException(string mensaje) : base(mensaje) { }
    }

    /// <summary>
    /// Formato del archivo .sigebak: XML (XmlSerializer) → GZip → AES-256-CBC (CryptoStream) → HMAC.
    ///
    ///   [marca "SIGEBAK1" 8 bytes][IV 16 bytes][datos cifrados][HMAC-SHA256 32 bytes]
    ///
    /// Decisiones de diseño:
    ///  - Se comprime ANTES de cifrar (el texto cifrado no se puede comprimir).
    ///  - "Cifrar y luego autenticar": el HMAC cubre marca + IV + datos cifrados. Al leer se verifica
    ///    primero el HMAC, así se detecta un archivo alterado, truncado o creado con OTRA llave sin
    ///    intentar descifrar basura. AES-CBC por sí solo no detecta modificaciones.
    ///  - Las llaves (AES y HMAC, distintas entre sí) salen de GestorLlaves, protegidas con DPAPI: un
    ///    respaldo solo se puede abrir con el mismo usuario de Windows en el mismo equipo.
    ///  - Se escribe primero a un archivo temporal y luego se renombra, para no dejar un respaldo a medias.
    /// </summary>
    public static class ArchivoRespaldoSeguro
    {
        public const string Extension = ".sigebak";

        private static readonly byte[] Marca = Encoding.ASCII.GetBytes("SIGEBAK1");
        private const int TamanoIv = 16;
        private const int TamanoMac = 32;
        private const int TamanoBloque = 16;

        /// <summary>Serializa, comprime, cifra y guarda el objeto en la ruta indicada.</summary>
        public static void Guardar<T>(string ruta, T objeto)
        {
            if (string.IsNullOrWhiteSpace(ruta)) throw new ArgumentException("Falta la ruta del archivo.");

            byte[] llaveAes = GestorLlaves.ObtenerLlaveAes();
            byte[] llaveMac = GestorLlaves.ObtenerLlaveHmac();
            try
            {
                byte[] iv;
                byte[] cifrado;
                using (Aes aes = Aes.Create())
                {
                    aes.KeySize = 256;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Key = llaveAes;
                    aes.GenerateIV(); // IV nuevo y aleatorio para este archivo
                    iv = aes.IV;

                    using (MemoryStream destino = new MemoryStream())
                    {
                        // Orden de cierre (de adentro hacia afuera): XML → GZip → CryptoStream (relleno final).
                        using (ICryptoTransform cifrador = aes.CreateEncryptor())
                        using (CryptoStream cs = new CryptoStream(destino, cifrador, CryptoStreamMode.Write))
                        using (GZipStream gz = new GZipStream(cs, CompressionMode.Compress))
                        {
                            new XmlSerializer(typeof(T)).Serialize(gz, objeto);
                        }
                        cifrado = destino.ToArray();
                    }
                }

                byte[] cuerpo = new byte[Marca.Length + TamanoIv + cifrado.Length];
                Buffer.BlockCopy(Marca, 0, cuerpo, 0, Marca.Length);
                Buffer.BlockCopy(iv, 0, cuerpo, Marca.Length, TamanoIv);
                Buffer.BlockCopy(cifrado, 0, cuerpo, Marca.Length + TamanoIv, cifrado.Length);

                byte[] mac;
                using (HMACSHA256 hmac = new HMACSHA256(llaveMac))
                {
                    mac = hmac.ComputeHash(cuerpo);
                }

                string temporal = ruta + ".tmp";
                using (FileStream fs = new FileStream(temporal, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    fs.Write(cuerpo, 0, cuerpo.Length);
                    fs.Write(mac, 0, mac.Length);
                }
                if (File.Exists(ruta)) File.Delete(ruta);
                File.Move(temporal, ruta);
            }
            finally
            {
                Array.Clear(llaveAes, 0, llaveAes.Length);
                Array.Clear(llaveMac, 0, llaveMac.Length);
            }
        }

        /// <summary>
        /// Verifica, descifra, descomprime y deserializa. Lanza RespaldoInvalidoException con un mensaje claro
        /// si no es un respaldo, si está dañado/alterado, si la llave no coincide o si el contenido no se puede leer.
        /// </summary>
        public static T Leer<T>(string ruta) where T : class
        {
            byte[] datos = File.ReadAllBytes(ruta); // los errores de acceso al archivo los maneja quien llama

            int minimo = Marca.Length + TamanoIv + TamanoBloque + TamanoMac;
            if (datos.Length < minimo)
                throw new RespaldoInvalidoException("El archivo está incompleto o no es un respaldo de SIGEPOSTA (.sigebak).");

            for (int i = 0; i < Marca.Length; i++)
            {
                if (datos[i] != Marca[i])
                    throw new RespaldoInvalidoException("El archivo no es un respaldo de SIGEPOSTA (.sigebak) o es de una versión no compatible.");
            }

            byte[] llaveAes = GestorLlaves.ObtenerLlaveAes();
            byte[] llaveMac = GestorLlaves.ObtenerLlaveHmac();
            try
            {
                int largoCuerpo = datos.Length - TamanoMac;
                byte[] macGuardado = new byte[TamanoMac];
                Buffer.BlockCopy(datos, largoCuerpo, macGuardado, 0, TamanoMac);

                byte[] macCalculado;
                using (HMACSHA256 hmac = new HMACSHA256(llaveMac))
                {
                    macCalculado = hmac.ComputeHash(datos, 0, largoCuerpo);
                }
                if (!HashContrasena.ComparacionConstante(macGuardado, macCalculado))
                    throw new RespaldoInvalidoException(
                        "No se pudo verificar el respaldo: el archivo está dañado o fue modificado, " +
                        "o fue creado con otra llave (otro usuario de Windows u otro equipo).");

                byte[] iv = new byte[TamanoIv];
                Buffer.BlockCopy(datos, Marca.Length, iv, 0, TamanoIv);
                int inicio = Marca.Length + TamanoIv;

                try
                {
                    using (Aes aes = Aes.Create())
                    {
                        aes.KeySize = 256;
                        aes.Mode = CipherMode.CBC;
                        aes.Padding = PaddingMode.PKCS7;
                        aes.Key = llaveAes;
                        aes.IV = iv;

                        using (MemoryStream origen = new MemoryStream(datos, inicio, largoCuerpo - inicio))
                        using (ICryptoTransform descifrador = aes.CreateDecryptor())
                        using (CryptoStream cs = new CryptoStream(origen, descifrador, CryptoStreamMode.Read))
                        using (GZipStream gz = new GZipStream(cs, CompressionMode.Decompress))
                        {
                            XmlReaderSettings cfg = new XmlReaderSettings();
                            cfg.DtdProcessing = DtdProcessing.Prohibit; // sin DTD: defensa extra ante XML malicioso
                            cfg.XmlResolver = null;
                            using (XmlReader lector = XmlReader.Create(gz, cfg))
                            {
                                T resultado = new XmlSerializer(typeof(T)).Deserialize(lector) as T;
                                if (resultado == null) throw new InvalidDataException();
                                return resultado;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (ex is CryptographicException || ex is InvalidDataException ||
                        ex is InvalidOperationException || ex is XmlException)
                    {
                        throw new RespaldoInvalidoException("El contenido del respaldo está dañado y no se puede leer.");
                    }
                    throw;
                }
            }
            finally
            {
                Array.Clear(llaveAes, 0, llaveAes.Length);
                Array.Clear(llaveMac, 0, llaveMac.Length);
            }
        }
    }
}
