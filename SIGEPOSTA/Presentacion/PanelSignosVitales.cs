using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using SIGEPOSTA.Negocio;

namespace SIGEPOSTA.Presentacion
{
    /// <summary>
    /// Recuadro "Signos vitales" de la nueva atención: peso, talla, IMC (calculado), temperatura, presión
    /// (sistólica y diastólica), frecuencia cardíaca y saturación.
    /// Decisiones de diseño:
    ///  - Todos los campos son opcionales. Los rangos permitidos los valida SignosVitales (Negocio), los mismos de la base.
    ///  - Los colores ámbar y rojo son una AYUDA VISUAL orientativa (referencia de adulto). NO son un diagnóstico, y el
    ///    recuadro lo dice explícitamente. No impiden guardar: el criterio clínico es del profesional.
    ///  - Este control solo lee y pinta; no sabe de bases de datos.
    /// </summary>
    public class PanelSignosVitales : GroupBox
    {
        private TextBox _txtPeso, _txtTalla, _txtImc, _txtTemperatura, _txtSistolica, _txtDiastolica, _txtFrecuencia, _txtSaturacion;
        private Label _errPeso, _errTalla, _errTemperatura, _errSistolica, _errDiastolica, _errFrecuencia, _errSaturacion;

        public PanelSignosVitales()
        {
            Text = "Signos vitales";
            Font = Estilo.FuenteGrupo;
            ForeColor = Estilo.Morado;
            BackColor = Estilo.Recuadro;
            Padding = new Padding(Estilo.MargenInterior);
            Margin = new Padding(0, 0, 0, Estilo.EspacioCampos);
            Dock = DockStyle.Fill;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Construir();
        }

        private void Construir()
        {
            _txtPeso = CrearDecimal();
            _txtTalla = CrearDecimal();
            _txtImc = Estilo.CrearTextoSoloLectura();
            _txtTemperatura = CrearDecimal();
            _txtSistolica = CrearEntero();
            _txtDiastolica = CrearEntero();
            _txtFrecuencia = CrearEntero();
            _txtSaturacion = CrearEntero();

            TableLayoutPanel cuadricula = new TableLayoutPanel();
            cuadricula.ColumnCount = 4;
            for (int i = 0; i < 4; i++) cuadricula.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            cuadricula.RowCount = 3;
            cuadricula.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            cuadricula.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            cuadricula.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            cuadricula.Dock = DockStyle.Fill;
            cuadricula.AutoSize = true;
            cuadricula.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            cuadricula.Margin = Padding.Empty;

            // Fila 1: peso, talla, IMC (solo lectura), temperatura. Fila 2: presión (2), frecuencia, saturación.
            Agregar(cuadricula, "Peso (kg)", _txtPeso, out _errPeso, 0, 0);
            Agregar(cuadricula, "Talla (cm)", _txtTalla, out _errTalla, 1, 0);
            Label sinError;
            Agregar(cuadricula, "IMC (calculado)", _txtImc, out sinError, 2, 0);
            Agregar(cuadricula, "Temperatura (°C)", _txtTemperatura, out _errTemperatura, 3, 0);
            Agregar(cuadricula, "Presión sistólica (mmHg)", _txtSistolica, out _errSistolica, 0, 1);
            Agregar(cuadricula, "Presión diastólica (mmHg)", _txtDiastolica, out _errDiastolica, 1, 1);
            Agregar(cuadricula, "Frec. cardíaca (lpm)", _txtFrecuencia, out _errFrecuencia, 2, 1);
            Agregar(cuadricula, "Saturación O₂ (%)", _txtSaturacion, out _errSaturacion, 3, 1);

            Label aviso = Estilo.CrearTexto(
                "Todos los campos son opcionales. Los colores ámbar y rojo son solo una ayuda visual para valores fuera de lo " +
                "habitual (referencia de adulto): NO son un diagnóstico ni reemplazan el criterio clínico.", 880);
            aviso.Font = Estilo.FuentePequena;
            aviso.ForeColor = Estilo.Gris;
            aviso.Margin = Padding.Empty;
            cuadricula.Controls.Add(aviso, 0, 2);
            cuadricula.SetColumnSpan(aviso, 4);

            Controls.Add(cuadricula);

            foreach (TextBox t in new[] { _txtPeso, _txtTalla, _txtTemperatura, _txtSistolica, _txtDiastolica, _txtFrecuencia, _txtSaturacion })
                t.TextChanged += delegate { ActualizarColores(); };
        }

        private static void Agregar(TableLayoutPanel g, string etiqueta, TextBox caja, out Label error, int col, int fila)
        {
            TableLayoutPanel campo = Estilo.CrearCampoConError(etiqueta, caja, out error);
            campo.Margin = new Padding(0, 0, col < 3 ? Estilo.EspacioCampos : 0, Estilo.EspacioCampos);
            g.Controls.Add(campo, col, fila);
        }

        // ---------- Cajas con filtro de teclas ----------

        private static TextBox CrearEntero()
        {
            TextBox t = Estilo.CrearTextBox(false, 3);
            t.KeyPress += delegate (object s, KeyPressEventArgs e)
            {
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true;
            };
            return t;
        }

        private static TextBox CrearDecimal()
        {
            TextBox t = Estilo.CrearTextBox(false, 7);
            t.KeyPress += delegate (object s, KeyPressEventArgs e)
            {
                TextBox caja = (TextBox)s;
                bool separador = e.KeyChar == '.' || e.KeyChar == ',';
                if (char.IsControl(e.KeyChar) || char.IsDigit(e.KeyChar)) return;
                // Un solo separador decimal.
                if (separador && caja.Text.IndexOfAny(new[] { '.', ',' }) < 0) return;
                e.Handled = true;
            };
            return t;
        }

        // ---------- Lectura ----------

        /// <summary>
        /// Lee los campos. Devuelve null si están todos vacíos. Si algo no es un número, lo anota en
        /// <paramref name="errores"/> (los rangos los valida después el servicio).
        /// </summary>
        public SignosVitales Leer(Dictionary<string, string> errores)
        {
            SignosVitales s = new SignosVitales();
            s.PesoKg = LeerDecimal(_txtPeso, "PesoKg", errores);
            s.TallaCm = LeerDecimal(_txtTalla, "TallaCm", errores);
            s.PresionSistolica = LeerEntero(_txtSistolica, "PresionSistolica", errores);
            s.PresionDiastolica = LeerEntero(_txtDiastolica, "PresionDiastolica", errores);
            s.Temperatura = LeerDecimal(_txtTemperatura, "Temperatura", errores);
            s.FrecuenciaCardiaca = LeerEntero(_txtFrecuencia, "FrecuenciaCardiaca", errores);
            s.SaturacionO2 = LeerEntero(_txtSaturacion, "SaturacionO2", errores);
            return s.TieneDatos ? s : null;
        }

        private static decimal? LeerDecimal(TextBox caja, string campo, Dictionary<string, string> errores)
        {
            string texto = caja.Text.Trim().Replace(',', '.');
            if (texto.Length == 0) return null;
            decimal v;
            if (decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out v)) return v;
            if (errores != null) errores[campo] = "Ingrese un número (ej. 36.5).";
            return null;
        }

        private static int? LeerEntero(TextBox caja, string campo, Dictionary<string, string> errores)
        {
            string texto = caja.Text.Trim();
            if (texto.Length == 0) return null;
            int v;
            if (int.TryParse(texto, NumberStyles.None, CultureInfo.InvariantCulture, out v)) return v;
            if (errores != null) errores[campo] = "Ingrese un número entero.";
            return null;
        }

        // ---------- Errores y limpieza ----------

        public void MostrarErrores(Dictionary<string, string> errores)
        {
            Poner(_errPeso, errores, "PesoKg");
            Poner(_errTalla, errores, "TallaCm");
            Poner(_errSistolica, errores, "PresionSistolica");
            Poner(_errDiastolica, errores, "PresionDiastolica");
            Poner(_errTemperatura, errores, "Temperatura");
            Poner(_errFrecuencia, errores, "FrecuenciaCardiaca");
            Poner(_errSaturacion, errores, "SaturacionO2");
        }

        public bool TieneClave(string campo)
        {
            return campo == "PesoKg" || campo == "TallaCm" || campo == "PresionSistolica" || campo == "PresionDiastolica" ||
                   campo == "Temperatura" || campo == "FrecuenciaCardiaca" || campo == "SaturacionO2";
        }

        private static void Poner(Label etiqueta, Dictionary<string, string> errores, string clave)
        {
            string m;
            Estilo.MostrarErrorCampo(etiqueta, errores != null && errores.TryGetValue(clave, out m) ? m : null);
        }

        public void LimpiarErrores()
        {
            MostrarErrores(null);
        }

        public void Limpiar()
        {
            foreach (TextBox t in new[] { _txtPeso, _txtTalla, _txtTemperatura, _txtSistolica, _txtDiastolica, _txtFrecuencia, _txtSaturacion })
                t.Clear();
            LimpiarErrores();
            ActualizarColores();
        }

        // ---------- Ayuda visual (colores) ----------

        private void ActualizarColores()
        {
            Dictionary<string, string> ignorar = new Dictionary<string, string>();
            SignosVitales s = Leer(ignorar) ?? new SignosVitales();

            Pintar(_txtPeso, NivelSigno.Normal);
            Pintar(_txtTalla, NivelSigno.Normal);
            Pintar(_txtTemperatura, s.Temperatura.HasValue ? SignosVitales.NivelTemperatura(s.Temperatura.Value) : NivelSigno.Normal);
            Pintar(_txtFrecuencia, s.FrecuenciaCardiaca.HasValue ? SignosVitales.NivelFrecuencia(s.FrecuenciaCardiaca.Value) : NivelSigno.Normal);
            Pintar(_txtSaturacion, s.SaturacionO2.HasValue ? SignosVitales.NivelSaturacion(s.SaturacionO2.Value) : NivelSigno.Normal);

            NivelSigno presion = (s.PresionSistolica.HasValue && s.PresionDiastolica.HasValue)
                ? SignosVitales.NivelPresion(s.PresionSistolica.Value, s.PresionDiastolica.Value) : NivelSigno.Normal;
            Pintar(_txtSistolica, presion);
            Pintar(_txtDiastolica, presion);

            decimal? imc = s.Imc;
            if (imc.HasValue)
            {
                _txtImc.Text = imc.Value.ToString("0.0", CultureInfo.InvariantCulture) + " (" + SignosVitales.ClasificarImc(imc.Value) + ")";
                Pintar(_txtImc, SignosVitales.NivelImc(imc.Value));
            }
            else
            {
                _txtImc.Text = string.Empty;
                Pintar(_txtImc, NivelSigno.Normal);
            }
        }

        private void Pintar(TextBox caja, NivelSigno nivel)
        {
            bool esImc = caja == _txtImc;
            switch (nivel)
            {
                case NivelSigno.Ambar:
                    caja.BackColor = Estilo.AlertaStock;
                    caja.ForeColor = Estilo.Texto;
                    break;
                case NivelSigno.Rojo:
                    caja.BackColor = Estilo.Error;
                    caja.ForeColor = Color.White;
                    break;
                default:
                    caja.BackColor = esImc ? Estilo.Fondo : Estilo.Recuadro;
                    caja.ForeColor = Estilo.Texto;
                    break;
            }
        }
    }
}
