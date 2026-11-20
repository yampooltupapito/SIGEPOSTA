using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using SIGEPOSTA.Negocio;

namespace SIGEPOSTA.Datos
{
    /// <summary>
    /// Acceso a datos de HistoriaClinica y Atencion (ADO.NET, SOLO consultas parametrizadas).
    /// Decisiones de diseño:
    ///  - La historia no tiene baja ni actualización propia: sus columnas (número, paciente, fecha de
    ///    apertura) no cambian, y si el paciente se da de baja la historia se conserva intacta.
    ///  - Las atenciones solo se agregan (nunca se editan ni borran): es un registro médico.
    ///  - El número de historia se calcula dentro de la transacción con un bloqueo de rango
    ///    (UPDLOCK, HOLDLOCK), de modo que dos registros simultáneos no puedan obtener el mismo número.
    /// </summary>
    public class HistoriaDatos
    {
        private const string Prefijo = "HC-";

        /// <summary>Todas las historias, ordenadas por número.</summary>
        public List<HistoriaClinica> Listar()
        {
            const string sql = "SELECT IdHistoria, NumeroHistoria, IdPaciente, FechaApertura, AntecedentesCifrado FROM HistoriaClinica ORDER BY NumeroHistoria";
            List<HistoriaClinica> lista = new List<HistoriaClinica>();
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(sql, cn))
            {
                cn.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read()) lista.Add(Mapear(r));
                }
            }
            return lista;
        }

        // Cada consulta es una cadena completa y constante: no se arma SQL pegando fragmentos.
        private const string SqlPorPaciente =
            "SELECT IdHistoria, NumeroHistoria, IdPaciente, FechaApertura, AntecedentesCifrado FROM HistoriaClinica WHERE IdPaciente = @v";
        private const string SqlPorNumero =
            "SELECT IdHistoria, NumeroHistoria, IdPaciente, FechaApertura, AntecedentesCifrado FROM HistoriaClinica WHERE NumeroHistoria = @v";

        public HistoriaClinica ObtenerPorPaciente(int idPaciente)
        {
            return ObtenerUna(SqlPorPaciente, SqlDbType.Int, 0, idPaciente);
        }

        /// <summary>Busca una historia por su número exacto (ej. HC-000001).</summary>
        public HistoriaClinica BuscarPorNumero(string numeroHistoria)
        {
            return ObtenerUna(SqlPorNumero, SqlDbType.NVarChar, 20, numeroHistoria);
        }

        /// <summary>
        /// Calcula el siguiente número (HC-000001, HC-000002...) bloqueando el rango dentro de la transacción.
        /// Debe llamarse con la misma transacción que luego inserta la historia.
        /// </summary>
        public string ObtenerSiguienteNumero(SqlConnection cn, SqlTransaction tx)
        {
            const string sql =
                "SELECT TOP 1 NumeroHistoria FROM HistoriaClinica WITH (UPDLOCK, HOLDLOCK) ORDER BY NumeroHistoria DESC";

            int ultimo = 0;
            using (SqlCommand cmd = Conexion.Comando(sql, cn, tx))
            {
                object o = cmd.ExecuteScalar();
                if (o != null && o != DBNull.Value)
                {
                    string texto = Convert.ToString(o).Trim();
                    if (texto.StartsWith(Prefijo, StringComparison.OrdinalIgnoreCase))
                        int.TryParse(texto.Substring(Prefijo.Length), out ultimo);
                }
            }
            return Prefijo + (ultimo + 1).ToString("D6");
        }

        /// <summary>Inserta la historia dentro de la transacción recibida y devuelve su IdHistoria.</summary>
        public int Insertar(HistoriaClinica h, SqlConnection cn, SqlTransaction tx)
        {
            if (h == null) throw new ArgumentNullException("h");

            const string sql =
                "INSERT INTO HistoriaClinica (NumeroHistoria, IdPaciente, FechaApertura) " +
                "OUTPUT INSERTED.IdHistoria VALUES (@num, @pac, SYSDATETIME())";

            using (SqlCommand cmd = Conexion.Comando(sql, cn, tx))
            {
                cmd.Parameters.Add("@num", SqlDbType.NVarChar, 20).Value = h.NumeroHistoria;
                cmd.Parameters.Add("@pac", SqlDbType.Int).Value = h.IdPaciente;
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        /// <summary>Atenciones de una historia (más recientes primero). El diagnóstico viene CIFRADO.</summary>
        public List<Atencion> ListarAtenciones(int idHistoria)
        {
            const string sql =
                "SELECT a.IdAtencion, a.IdHistoria, a.Fecha, a.Motivo, a.DiagnosticoCifrado, a.IdUsuario, u.NombreCompleto, " +
                "       a.IdMedicamento, a.CantidadRecetada, m.Nombre, " +
                "       a.PesoKg, a.TallaCm, a.PresionSistolica, a.PresionDiastolica, a.Temperatura, a.FrecuenciaCardiaca, a.SaturacionO2, a.Cie10Cifrado, " +
                "       m.Concentracion, ff.Nombre, um.Nombre " +
                "FROM Atencion a LEFT JOIN Usuario u ON u.IdUsuario = a.IdUsuario " +
                "LEFT JOIN Medicamento m ON m.IdMedicamento = a.IdMedicamento " +
                "LEFT JOIN FormaFarmaceutica ff ON ff.IdForma = m.IdForma " +
                "LEFT JOIN UnidadMedida um ON um.IdUnidad = m.IdUnidad " +
                "WHERE a.IdHistoria = @h ORDER BY a.Fecha DESC, a.IdAtencion DESC";

            List<Atencion> lista = new List<Atencion>();
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(sql, cn))
            {
                cmd.Parameters.Add("@h", SqlDbType.Int).Value = idHistoria;
                cn.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        Atencion a = new Atencion();
                        a.IdAtencion = r.GetInt32(0);
                        a.IdHistoria = r.GetInt32(1);
                        a.Fecha = r.GetDateTime(2);
                        a.Motivo = r.GetString(3);
                        a.DiagnosticoCifrado = r.IsDBNull(4) ? null : (byte[])r[4];
                        a.IdMedico = r.GetInt32(5);
                        a.NombreMedico = r.IsDBNull(6) ? string.Empty : r.GetString(6);
                        a.IdMedicamento = r.IsDBNull(7) ? (int?)null : r.GetInt32(7);
                        a.CantidadRecetada = r.IsDBNull(8) ? (int?)null : r.GetInt32(8);
                        // Receta con el nombre completo y la unidad: "Paracetamol 500 mg - Tableta x 10 tabletas".
                        a.RecetaTexto = (a.TieneReceta && !r.IsDBNull(9))
                            ? Medicamento.ComponerNombre(r.GetString(9), r.IsDBNull(18) ? null : r.GetString(18),
                                                         r.IsDBNull(19) ? null : r.GetString(19), null)
                              + " x " + Medicamento.FormatoCantidad(a.CantidadRecetada.Value, r.IsDBNull(20) ? null : r.GetString(20))
                            : string.Empty;
                        a.Signos = LeerSignos(r, 10);
                        a.Cie10Cifrado = r.IsDBNull(17) ? null : (byte[])r[17];
                        lista.Add(a);
                    }
                }
            }
            return lista;
        }

        // Las columnas numéricas se leen con Convert para no depender de si la columna es INT o DECIMAL.
        private static decimal? LeerDecimal(SqlDataReader r, int i)
        {
            return r.IsDBNull(i) ? (decimal?)null : Convert.ToDecimal(r.GetValue(i));
        }

        private static int? LeerEntero(SqlDataReader r, int i)
        {
            return r.IsDBNull(i) ? (int?)null : Convert.ToInt32(r.GetValue(i));
        }

        /// <summary>Lee los 7 signos vitales a partir de la columna indicada; null si todos son NULL.</summary>
        private static SignosVitales LeerSignos(SqlDataReader r, int desde)
        {
            SignosVitales s = new SignosVitales();
            s.PesoKg = LeerDecimal(r, desde);
            s.TallaCm = LeerDecimal(r, desde + 1);
            s.PresionSistolica = LeerEntero(r, desde + 2);
            s.PresionDiastolica = LeerEntero(r, desde + 3);
            s.Temperatura = LeerDecimal(r, desde + 4);
            s.FrecuenciaCardiaca = LeerEntero(r, desde + 5);
            s.SaturacionO2 = LeerEntero(r, desde + 6);
            return s.TieneDatos ? s : null;
        }

        /// <summary>
        /// Inserta una atención SIN receta (atajo con su propia conexión). El diagnóstico va YA cifrado
        /// (a.DiagnosticoCifrado). Devuelve su IdAtencion.
        /// </summary>
        public int InsertarAtencion(Atencion a)
        {
            using (SqlConnection cn = Conexion.Crear())
            {
                cn.Open();
                return InsertarAtencion(a, cn, null);
            }
        }

        /// <summary>
        /// Inserta la atención dentro de la transacción recibida (tx puede ser null si no hay transacción).
        /// Guarda también la receta: IdMedicamento y CantidadRecetada van juntos o ambos NULL.
        /// </summary>
        public int InsertarAtencion(Atencion a, SqlConnection cn, SqlTransaction tx)
        {
            if (a == null) throw new ArgumentNullException("a");

            const string sql =
                "INSERT INTO Atencion (IdHistoria, Fecha, Motivo, DiagnosticoCifrado, IdUsuario, IdMedicamento, CantidadRecetada, " +
                "                      PesoKg, TallaCm, PresionSistolica, PresionDiastolica, Temperatura, FrecuenciaCardiaca, SaturacionO2, Cie10Cifrado) " +
                "OUTPUT INSERTED.IdAtencion VALUES (@hist, @fecha, @motivo, @diag, @usr, @med, @cant, " +
                "                                   @peso, @talla, @sis, @dia, @temp, @fc, @sat, @cie)";

            using (SqlCommand cmd = Conexion.Comando(sql, cn, tx))
            {
                cmd.Parameters.Add("@hist", SqlDbType.Int).Value = a.IdHistoria;
                cmd.Parameters.Add("@fecha", SqlDbType.DateTime2).Value = a.Fecha;
                cmd.Parameters.Add("@motivo", SqlDbType.NVarChar, 200).Value = a.Motivo;
                cmd.Parameters.Add("@diag", SqlDbType.VarBinary, -1).Value = a.DiagnosticoCifrado;
                cmd.Parameters.Add("@usr", SqlDbType.Int).Value = a.IdMedico;
                // Código CIE-10 YA cifrado (IV aleatorio + AES); NULL en una atención sin código.
                cmd.Parameters.Add("@cie", SqlDbType.VarBinary, 128).Value =
                    a.Cie10Cifrado == null ? (object)DBNull.Value : a.Cie10Cifrado;
                cmd.Parameters.Add("@med", SqlDbType.Int).Value =
                    a.TieneReceta ? (object)a.IdMedicamento.Value : DBNull.Value;
                cmd.Parameters.Add("@cant", SqlDbType.Int).Value =
                    a.TieneReceta ? (object)a.CantidadRecetada.Value : DBNull.Value;

                // Signos vitales (todos opcionales): NULL si no se midieron. Parámetros con tipo y escala explícitos.
                SignosVitales s = a.Signos;
                AgregarDecimal(cmd, "@peso", 6, 2, s == null ? null : s.PesoKg);
                AgregarDecimal(cmd, "@talla", 5, 1, s == null ? null : s.TallaCm);
                AgregarEntero(cmd, "@sis", s == null ? null : s.PresionSistolica);
                AgregarEntero(cmd, "@dia", s == null ? null : s.PresionDiastolica);
                AgregarDecimal(cmd, "@temp", 4, 1, s == null ? null : s.Temperatura);
                AgregarEntero(cmd, "@fc", s == null ? null : s.FrecuenciaCardiaca);
                AgregarEntero(cmd, "@sat", s == null ? null : s.SaturacionO2);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        /// <summary>
        /// Dentro de la transacción: número de la historia (HC-000001) y posta del paciente dueño de la historia.
        /// Devuelve false si la historia no existe.
        /// </summary>
        public bool ObtenerNumeroYPosta(int idHistoria, SqlConnection cn, SqlTransaction tx, out string numeroHistoria, out int idPostaPaciente)
        {
            const string sql =
                "SELECT h.NumeroHistoria, ISNULL(p.IdPosta, 0) " +
                "FROM HistoriaClinica h INNER JOIN Paciente p ON p.IdPaciente = h.IdPaciente " +
                "WHERE h.IdHistoria = @h";

            numeroHistoria = null;
            idPostaPaciente = 0;
            using (SqlCommand cmd = Conexion.Comando(sql, cn, tx))
            {
                cmd.Parameters.Add("@h", SqlDbType.Int).Value = idHistoria;
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return false;
                    numeroHistoria = r.GetString(0);
                    idPostaPaciente = r.GetInt32(1);
                    return true;
                }
            }
        }

        private static void AgregarDecimal(SqlCommand cmd, string nombre, byte precision, byte escala, decimal? valor)
        {
            SqlParameter p = cmd.Parameters.Add(nombre, SqlDbType.Decimal);
            p.Precision = precision;
            p.Scale = escala;
            p.Value = valor.HasValue ? (object)valor.Value : DBNull.Value;
        }

        private static void AgregarEntero(SqlCommand cmd, string nombre, int? valor)
        {
            cmd.Parameters.Add(nombre, SqlDbType.Int).Value = valor.HasValue ? (object)valor.Value : DBNull.Value;
        }

        /// <summary>Guarda los antecedentes YA cifrados (null = sin antecedentes).</summary>
        public void ActualizarAntecedentes(int idHistoria, byte[] antecedentesCifrado)
        {
            const string sql = "UPDATE HistoriaClinica SET AntecedentesCifrado = @ant WHERE IdHistoria = @id";
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(sql, cn))
            {
                cmd.Parameters.Add("@ant", SqlDbType.VarBinary, -1).Value =
                    antecedentesCifrado == null ? (object)DBNull.Value : antecedentesCifrado;
                cmd.Parameters.Add("@id", SqlDbType.Int).Value = idHistoria;
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        // ---------- Auxiliares ----------

        private static HistoriaClinica ObtenerUna(string sql, SqlDbType tipo, int tamano, object valor)
        {
            using (SqlConnection cn = Conexion.Crear())
            using (SqlCommand cmd = Conexion.Comando(sql, cn))
            {
                SqlParameter p = tamano > 0 ? cmd.Parameters.Add("@v", tipo, tamano) : cmd.Parameters.Add("@v", tipo);
                p.Value = valor;
                cn.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    return r.Read() ? Mapear(r) : null;
                }
            }
        }

        private static HistoriaClinica Mapear(SqlDataReader r)
        {
            HistoriaClinica h = new HistoriaClinica();
            h.IdHistoria = r.GetInt32(0);
            h.NumeroHistoria = r.GetString(1);
            h.IdPaciente = r.GetInt32(2);
            h.FechaApertura = r.GetDateTime(3);
            h.AntecedentesCifrado = r.IsDBNull(4) ? null : (byte[])r[4];
            return h;
        }
    }
}
