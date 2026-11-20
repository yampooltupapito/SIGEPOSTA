using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using SIGEPOSTA.Negocio;

namespace SIGEPOSTA.Datos
{
    /// <summary>
    /// Acceso a datos de FormaFarmaceutica(IdForma, Nombre, Activo) y UnidadMedida(IdUnidad, Nombre, Activo).
    /// Las dos tablas tienen la misma forma, así que una sola clase atiende a ambas; el SQL de cada una es una
    /// cadena CONSTANTE (se elige con un switch sobre TipoCatalogo, nunca se arma con texto del usuario).
    /// Todo va parametrizado con tipo y tamaño explícitos. Baja lógica (Activo = 0): nunca se borra una fila.
    /// </summary>
    public class CatalogoDatos
    {
        private const int ErrorLlaveDuplicada1 = 2627;
        private const int ErrorLlaveDuplicada2 = 2601;

        private class Sql
        {
            public string Listar, Obtener, ExisteNombre, Insertar, Actualizar, Baja, ContarUsan;
        }

        // El número de medicamentos ACTIVOS que usan cada elemento sale de una subconsulta correlacionada.
        private static readonly Sql SqlForma = new Sql
        {
            Listar = "SELECT t.IdForma, t.Nombre, t.Activo, " +
                     "(SELECT COUNT(*) FROM Medicamento m WHERE m.IdForma = t.IdForma AND m.Activo = 1) " +
                     "FROM FormaFarmaceutica t ORDER BY t.Nombre",
            Obtener = "SELECT t.IdForma, t.Nombre, t.Activo, " +
                      "(SELECT COUNT(*) FROM Medicamento m WHERE m.IdForma = t.IdForma AND m.Activo = 1) " +
                      "FROM FormaFarmaceutica t WHERE t.IdForma = @id",
            ExisteNombre = "SELECT COUNT(*) FROM FormaFarmaceutica WHERE Nombre = @n AND IdForma <> @id",
            Insertar = "INSERT INTO FormaFarmaceutica (Nombre, Activo) OUTPUT INSERTED.IdForma VALUES (@n, 1)",
            Actualizar = "UPDATE FormaFarmaceutica SET Nombre = @n WHERE IdForma = @id",
            Baja = "UPDATE FormaFarmaceutica SET Activo = 0 WHERE IdForma = @id",
            ContarUsan = "SELECT COUNT(*) FROM Medicamento WHERE IdForma = @id AND Activo = 1"
        };

        private static readonly Sql SqlUnidad = new Sql
        {
            Listar = "SELECT t.IdUnidad, t.Nombre, t.Activo, " +
                     "(SELECT COUNT(*) FROM Medicamento m WHERE m.IdUnidad = t.IdUnidad AND m.Activo = 1) " +
                     "FROM UnidadMedida t ORDER BY t.Nombre",
            Obtener = "SELECT t.IdUnidad, t.Nombre, t.Activo, " +
                      "(SELECT COUNT(*) FROM Medicamento m WHERE m.IdUnidad = t.IdUnidad AND m.Activo = 1) " +
                      "FROM UnidadMedida t WHERE t.IdUnidad = @id",
            ExisteNombre = "SELECT COUNT(*) FROM UnidadMedida WHERE Nombre = @n AND IdUnidad <> @id",
            Insertar = "INSERT INTO UnidadMedida (Nombre, Activo) OUTPUT INSERTED.IdUnidad VALUES (@n, 1)",
            Actualizar = "UPDATE UnidadMedida SET Nombre = @n WHERE IdUnidad = @id",
            Baja = "UPDATE UnidadMedida SET Activo = 0 WHERE IdUnidad = @id",
            ContarUsan = "SELECT COUNT(*) FROM Medicamento WHERE IdUnidad = @id AND Activo = 1"
        };

        private static Sql Elegir(TipoCatalogo tipo)
        {
            switch (tipo)
            {
                case TipoCatalogo.Forma: return SqlForma;
                case TipoCatalogo.Unidad: return SqlUnidad;
                default: throw new ArgumentOutOfRangeException("tipo");
            }
        }

        /// <summary>Todos los elementos (activos y dados de baja) con el número de medicamentos activos que los usan.</summary>
        public virtual List<ItemCatalogo> Listar(TipoCatalogo tipo)
        {
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(Elegir(tipo).Listar, cn))
            {
                cn.Open();
                return Leer(cmd);
            }
        }

        /// <summary>El elemento por Id (activo o no). Null si no existe.</summary>
        public virtual ItemCatalogo Obtener(TipoCatalogo tipo, int id)
        {
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(Elegir(tipo).Obtener, cn))
            {
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = id;
                cn.Open();
                List<ItemCatalogo> r = Leer(cmd);
                return r.Count == 0 ? null : r[0];
            }
        }

        /// <summary>¿Hay OTRO elemento (activo o no) con ese nombre? excluirId = el que se edita (0 si es nuevo).</summary>
        public virtual bool ExisteNombre(TipoCatalogo tipo, string nombre, int excluirId)
        {
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(Elegir(tipo).ExisteNombre, cn))
            {
                cmd.Parameters.Add("@n", SqlDbType.NVarChar, ItemCatalogo.MaxNombre).Value = nombre;
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = excluirId;
                cn.Open();
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        public virtual int Insertar(TipoCatalogo tipo, string nombre)
        {
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(Elegir(tipo).Insertar, cn))
            {
                cmd.Parameters.Add("@n", SqlDbType.NVarChar, ItemCatalogo.MaxNombre).Value = nombre;
                cn.Open();
                try
                {
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
                catch (SqlException ex)
                {
                    if (EsDuplicado(ex)) throw new ValidacionException("Ya existe un elemento con ese nombre.", "Nombre");
                    throw;
                }
            }
        }

        public virtual void ActualizarNombre(TipoCatalogo tipo, int id, string nombre)
        {
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(Elegir(tipo).Actualizar, cn))
            {
                cmd.Parameters.Add("@n", SqlDbType.NVarChar, ItemCatalogo.MaxNombre).Value = nombre;
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = id;
                cn.Open();
                try
                {
                    cmd.ExecuteNonQuery();
                }
                catch (SqlException ex)
                {
                    if (EsDuplicado(ex)) throw new ValidacionException("Ya existe un elemento con ese nombre.", "Nombre");
                    throw;
                }
            }
        }

        public virtual void BajaLogica(TipoCatalogo tipo, int id)
        {
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(Elegir(tipo).Baja, cn))
            {
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = id;
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>Medicamentos activos que usan el elemento (para avisar antes de una baja).</summary>
        public virtual int ContarMedicamentos(TipoCatalogo tipo, int id)
        {
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(Elegir(tipo).ContarUsan, cn))
            {
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = id;
                cn.Open();
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        private static bool EsDuplicado(SqlException ex)
        {
            return ex.Number == ErrorLlaveDuplicada1 || ex.Number == ErrorLlaveDuplicada2;
        }

        private static List<ItemCatalogo> Leer(SqlCommand cmd)
        {
            List<ItemCatalogo> lista = new List<ItemCatalogo>();
            using (SqlDataReader r = cmd.ExecuteReader())
            {
                while (r.Read())
                    lista.Add(ItemCatalogo.Cargar(r.GetInt32(0), r.GetString(1), r.GetBoolean(2), Convert.ToInt32(r.GetValue(3))));
            }
            return lista;
        }
    }
}
