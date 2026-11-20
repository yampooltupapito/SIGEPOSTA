using System;
using System.Collections.Generic;
using System.Linq;
using SIGEPOSTA.Datos;

namespace SIGEPOSTA.Negocio
{
    /// <summary>
    /// Reglas de los catálogos Forma farmacéutica y Unidad de medida.
    /// Decisiones de diseño:
    ///  - CADA método público vuelve a validar el rol (Administrador y Farmacia): ocultar botones no es seguridad.
    ///  - Nombre obligatorio y sin duplicados (también contra los dados de baja: la base tiene UNIQUE sobre Nombre).
    ///  - "Sin definir" (valor de los medicamentos antiguos) no se puede renombrar ni dar de baja.
    ///  - La baja es lógica y se permite aunque haya medicamentos que la usen: la pantalla avisa cuántos son antes
    ///    de confirmar, y esos medicamentos conservan su forma o unidad. Solo deja de ofrecerse en medicamentos nuevos.
    ///  - Auditoría: solo "Elemento Id N" (el nombre del elemento no es dato sensible, pero no hace falta copiarlo).
    /// </summary>
    public class CatalogoServicio
    {
        private readonly CatalogoDatos _datos;

        public CatalogoServicio() : this(new CatalogoDatos()) { }

        public CatalogoServicio(CatalogoDatos datos)
        {
            if (datos == null) throw new ArgumentNullException("datos");
            _datos = datos;
        }

        public static string NombreTipo(TipoCatalogo tipo)
        {
            return tipo == TipoCatalogo.Forma ? "forma farmacéutica" : "unidad de medida";
        }

        // ---------- Consultas ----------

        /// <summary>Elementos ACTIVOS con el número de medicamentos que usa cada uno (los dados de baja no se listan).</summary>
        public List<ItemCatalogo> ListarActivos(TipoCatalogo tipo)
        {
            Permisos.ExigirAccesoCatalogosMedicamento();
            return _datos.Listar(tipo).Where(i => i.Activo).ToList();
        }

        public int ContarMedicamentos(TipoCatalogo tipo, int id)
        {
            Permisos.ExigirAccesoCatalogosMedicamento();
            return _datos.ContarMedicamentos(tipo, id);
        }

        // ---------- Validación ----------

        /// <summary>Valida el nombre. Clave: Nombre. No consulta la base (los duplicados se revisan al guardar).</summary>
        public Dictionary<string, string> Validar(string nombre)
        {
            Dictionary<string, string> errores = new Dictionary<string, string>();
            try { new ItemCatalogo().Nombre = nombre; }
            catch (ArgumentException ex) { errores["Nombre"] = ex.Message; }
            return errores;
        }

        // ---------- Altas, cambios y bajas ----------

        public ItemCatalogo Registrar(TipoCatalogo tipo, string nombre)
        {
            Permisos.ExigirAccesoCatalogosMedicamento();
            LanzarPrimerError(Validar(nombre));

            ItemCatalogo item = new ItemCatalogo(nombre);
            if (_datos.ExisteNombre(tipo, item.Nombre, 0)) throw Duplicado(tipo);

            item.Id = _datos.Insertar(tipo, item.Nombre);   // si dos usuarios lo crean a la vez, el UNIQUE de la base lo impide
            AuditoriaServicio.Registrar(Accion(tipo, 0), "Catalogo", item.Id, "Elemento Id " + item.Id, AuditoriaServicio.Ok);
            return item;
        }

        public void Actualizar(TipoCatalogo tipo, int id, string nombre)
        {
            Permisos.ExigirAccesoCatalogosMedicamento();
            if (id <= 0) throw new ReglaNegocioException("No hay ningún elemento seleccionado.");
            LanzarPrimerError(Validar(nombre));

            ItemCatalogo actual = _datos.Obtener(tipo, id);
            if (actual == null) throw new ReglaNegocioException("El elemento ya no existe.");
            if (actual.EsSinDefinir) throw new ReglaNegocioException("\"" + Medicamento.SinDefinir + "\" no se puede modificar.");

            ItemCatalogo nuevo = new ItemCatalogo(nombre);
            if (_datos.ExisteNombre(tipo, nuevo.Nombre, id)) throw Duplicado(tipo);

            _datos.ActualizarNombre(tipo, id, nuevo.Nombre);
            AuditoriaServicio.Registrar(Accion(tipo, 1), "Catalogo", id, "Elemento Id " + id, AuditoriaServicio.Ok);
        }

        /// <summary>Baja lógica. "Sin definir" no se puede dar de baja.</summary>
        public void DarDeBaja(TipoCatalogo tipo, int id)
        {
            Permisos.ExigirAccesoCatalogosMedicamento();
            if (id <= 0) throw new ReglaNegocioException("No hay ningún elemento seleccionado.");

            ItemCatalogo actual = _datos.Obtener(tipo, id);
            if (actual == null) throw new ReglaNegocioException("El elemento ya no existe.");
            if (actual.EsSinDefinir) throw new ReglaNegocioException("\"" + Medicamento.SinDefinir + "\" no se puede dar de baja.");

            _datos.BajaLogica(tipo, id);
            AuditoriaServicio.Registrar(Accion(tipo, 2), "Catalogo", id, "Elemento Id " + id, AuditoriaServicio.Ok);
        }

        // ---------- Auxiliares ----------

        private static ValidacionException Duplicado(TipoCatalogo tipo)
        {
            return new ValidacionException(
                tipo == TipoCatalogo.Forma ? "Ya existe una forma farmacéutica con ese nombre." : "Ya existe una unidad de medida con ese nombre.",
                "Nombre");
        }

        /// <summary>0 = alta, 1 = edición, 2 = baja.</summary>
        private static string Accion(TipoCatalogo tipo, int que)
        {
            if (tipo == TipoCatalogo.Forma)
                return que == 0 ? AccionAuditoria.AltaForma : que == 1 ? AccionAuditoria.EdicionForma : AccionAuditoria.BajaForma;
            return que == 0 ? AccionAuditoria.AltaUnidad : que == 1 ? AccionAuditoria.EdicionUnidad : AccionAuditoria.BajaUnidad;
        }

        private static void LanzarPrimerError(Dictionary<string, string> errores)
        {
            foreach (KeyValuePair<string, string> par in errores)
                throw new ValidacionException(par.Value, par.Key);
        }
    }
}
