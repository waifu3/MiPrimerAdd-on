using System;
using System.Collections.Generic;
using System.Globalization;
using SAPbouiCOM;

namespace MiprimerAddon
{
    public class Addon
    {
        public static SAPbouiCOM.Application SBO_Application;
        private bool logHabilitado = false;
        private bool bloqueosHabilitado = false;
        private bool baseDePrueba = false;

        // Parámetros de configuración (tabla @MPA_CONFIG)
        private const string CfgLimite = "LIMITE_COMENTARIO";            // monto desde el cual se exige comentario
        private const string CfgActiva = "VALIDACION_ACTIVA";            // 1 = validar, 0 = no validar
        private const string CfgCondPago = "EXIGIR_CONDICION_PAGO";      // 1 = exigir condición distinta de FONDO
        private const string CfgExentos = "USUARIOS_EXENTOS";            // texto: códigos de usuario separados por ;
        private const double LimiteDefecto = 1000;
        private const double ActivaDefecto = 1;
        private const double CondPagoDefecto = 1;

        // Código de la condición de pago por omisión de SAP (en esta empresa se llama FONDO)
        private const string CondicionPagoPorDefecto = "-1";

        // Tipo de formulario de SAP -> tabla de cabecera del documento (botón "Dólar del doc.")
        private static readonly Dictionary<string, string> DocTablas =
            new Dictionary<string, string>
            {
                { "133", "OINV" },   // Factura de ventas
                { "139", "ORDR" },   // Orden de venta
                { "141", "OPCH" }    // Factura de compra
            };

        // Documentos que se validan al grabar o actualizar
        private static readonly Dictionary<string, string> ValidaComentario =
            new Dictionary<string, string>
            {
                { "133", "OINV" },   // Factura de ventas
                { "139", "ORDR" }    // Orden de venta
            };

        public Addon()
        {
            SboGuiApi gui = new SboGuiApi();
            string[] args = Environment.GetCommandLineArgs();

            string cs = args.Length > 1
                ? args[1]
                : "0030002C0030002C00530041005000420044005F00440061007400650076002C0050004C006F006D0056004900490056";

            gui.Connect(cs);
            SBO_Application = gui.GetApplication(-1);

            SBO_Application.AppEvent += OnAppEvent;
            SBO_Application.MenuEvent += OnMenuEvent;
            SBO_Application.ItemEvent += OnItemEvent;

            AddMenu();
            AsegurarTablas();
            SBO_Application.SetStatusBarMessage("MiprimerAddon conectado", BoMessageTime.bmt_Short, false);
        }

        // ---------------------------------------------------------------
        // MENÚ
        // ---------------------------------------------------------------
        private void AddMenu()
        {
            if (SBO_Application.Menus.Exists("MPA_Main"))
                SBO_Application.Menus.RemoveEx("MPA_Main");

            MenuItem modulos = SBO_Application.Menus.Item("43520");

            MenuCreationParams cp = (MenuCreationParams)SBO_Application
                .CreateObject(BoCreatableObjectType.cot_MenuCreationParams);
            cp.Type = BoMenuType.mt_POPUP;
            cp.UniqueID = "MPA_Main";
            cp.String = "Mi Primer Addon";
            cp.Enabled = true;
            cp.Position = -1;
            modulos.SubMenus.AddEx(cp);

            MenuItem main = SBO_Application.Menus.Item("MPA_Main");
            AddSubMenu(main, "MPA_Form", "Abrir formulario");
            AddSubMenu(main, "MPA_Log", "Ver log de consultas");
            AddSubMenu(main, "MPA_Bloq", "Ver bloqueos");
            AddSubMenu(main, "MPA_Cfg", "Configuración");
        }

        private void AddSubMenu(MenuItem padre, string id, string texto)
        {
            MenuCreationParams sub = (MenuCreationParams)SBO_Application
                .CreateObject(BoCreatableObjectType.cot_MenuCreationParams);
            sub.Type = BoMenuType.mt_STRING;
            sub.UniqueID = id;
            sub.String = texto;
            sub.Enabled = true;
            padre.SubMenus.AddEx(sub);
        }

        private void OnMenuEvent(ref MenuEvent pVal, out bool BubbleEvent)
        {
            BubbleEvent = true;

            if (pVal.BeforeAction) return;

            try
            {
                if (pVal.MenuUID == "MPA_Form")
                    OpenForm();
                else if (pVal.MenuUID == "MPA_Log")
                    OpenLogForm();
                else if (pVal.MenuUID == "MPA_Bloq")
                    OpenBloqForm();
                else if (pVal.MenuUID == "MPA_Cfg")
                    OpenCfgForm();
            }
            catch (Exception ex)
            {
                SBO_Application.MessageBox("Error al abrir '" + pVal.MenuUID + "': " + ex.Message);
            }
        }

        // ---------------------------------------------------------------
        // FORMULARIO PROPIO: TIPOS DE CAMBIO
        // ---------------------------------------------------------------
        private void OpenForm()
        {
            foreach (SAPbouiCOM.Form f in SBO_Application.Forms)
            {
                if (f.TypeEx == "MPA_Form")
                {
                    f.Select();
                    return;
                }
            }

            FormCreationParams fcp = (FormCreationParams)SBO_Application
                .CreateObject(BoCreatableObjectType.cot_FormCreationParams);
            fcp.BorderStyle = BoFormBorderStyle.fbs_Sizable;
            fcp.FormType = "MPA_Form";

            SAPbouiCOM.Form form = SBO_Application.Forms.AddEx(fcp);
            form.Title = "Tipos de cambio";
            form.Left = 400;
            form.Top = 100;
            form.Width = 340;
            form.Height = 230;

            form.DataSources.UserDataSources.Add("dsFecha", BoDataType.dt_DATE, 10);
            form.DataSources.UserDataSources.Add("dsUsd", BoDataType.dt_SHORT_TEXT, 50);
            form.DataSources.UserDataSources.Add("dsEur", BoDataType.dt_SHORT_TEXT, 50);
            form.DataSources.UserDataSources.Add("dsUf", BoDataType.dt_SHORT_TEXT, 50);

            AddLabel(form, "lblFecha", "Fecha:", 20, 20);
            Item txtFecha = form.Items.Add("txtFecha", BoFormItemTypes.it_EDIT);
            txtFecha.Left = 90; txtFecha.Top = 20; txtFecha.Width = 120; txtFecha.Height = 16;
            ((EditText)txtFecha.Specific).DataBind.SetBound(true, "", "dsFecha");
            form.DataSources.UserDataSources.Item("dsFecha").ValueEx =
                DateTime.Today.ToString("yyyyMMdd");

            Item btn = form.Items.Add("btnOk", BoFormItemTypes.it_BUTTON);
            btn.Left = 20; btn.Top = 55; btn.Width = 100; btn.Height = 20;
            ((SAPbouiCOM.Button)btn.Specific).Caption = "Consultar";

            AddResultRow(form, "Usd", "Dólar (US$):", "dsUsd", 100);
            AddResultRow(form, "Eur", "Euro (EUR):", "dsEur", 125);
            AddResultRow(form, "Uf", "UF:", "dsUf", 150);

            form.Visible = true;
        }

        private void AddLabel(SAPbouiCOM.Form form, string uid, string caption, int left, int top)
        {
            Item lbl = form.Items.Add(uid, BoFormItemTypes.it_STATIC);
            lbl.Left = left; lbl.Top = top; lbl.Width = 70; lbl.Height = 16;
            ((StaticText)lbl.Specific).Caption = caption;
        }

        private void AddResultRow(SAPbouiCOM.Form form, string sufijo, string caption, string ds, int top)
        {
            AddLabel(form, "lbl" + sufijo, caption, 20, top);

            Item txt = form.Items.Add("txt" + sufijo, BoFormItemTypes.it_EDIT);
            txt.Left = 90; txt.Top = top; txt.Width = 200; txt.Height = 16;
            ((EditText)txt.Specific).DataBind.SetBound(true, "", ds);
            txt.Enabled = false;
        }

        // ---------------------------------------------------------------
        // FORMULARIO PROPIO: LOG DE CONSULTAS (GRILLA)
        // ---------------------------------------------------------------
        private void OpenLogForm()
        {
            foreach (SAPbouiCOM.Form f in SBO_Application.Forms)
            {
                if (f.TypeEx == "MPA_LogForm")
                {
                    f.Select();
                    CargarLog(f);
                    return;
                }
            }

            FormCreationParams fcp = (FormCreationParams)SBO_Application
                .CreateObject(BoCreatableObjectType.cot_FormCreationParams);
            fcp.BorderStyle = BoFormBorderStyle.fbs_Sizable;
            fcp.FormType = "MPA_LogForm";

            SAPbouiCOM.Form form = SBO_Application.Forms.AddEx(fcp);
            form.Title = "Log de consultas de tipo de cambio";
            form.Left = 300;
            form.Top = 80;
            form.Width = 640;
            form.Height = 330;

            form.DataSources.DataTables.Add("dtLog");

            Item btn = form.Items.Add("btnRef", BoFormItemTypes.it_BUTTON);
            btn.Left = 10; btn.Top = 10; btn.Width = 100; btn.Height = 20;
            ((SAPbouiCOM.Button)btn.Specific).Caption = "Actualizar";

            Item grd = form.Items.Add("grdLog", BoFormItemTypes.it_GRID);
            grd.Left = 10; grd.Top = 40; grd.Width = 610; grd.Height = 240;
            ((SAPbouiCOM.Grid)grd.Specific).DataTable = form.DataSources.DataTables.Item("dtLog");

            CargarLog(form);
            form.Visible = true;
        }

        private void CargarLog(SAPbouiCOM.Form form)
        {
            try
            {
                SAPbouiCOM.DataTable dt = form.DataSources.DataTables.Item("dtLog");

                // Los alias de la consulta se convierten en los títulos de las columnas
                dt.ExecuteQuery(
                    "SELECT TOP 50 \"Code\" AS \"Nro\", " +
                    "\"U_Fecha\" AS \"Fecha consultada\", " +
                    "\"U_Moneda\" AS \"Moneda\", " +
                    "\"U_Tasa\" AS \"Tipo de cambio\", " +
                    "\"U_FechaTC\" AS \"Vigencia del valor\", " +
                    "\"U_Usuario\" AS \"Usuario\" " +
                    "FROM \"@MPA_LOG\" ORDER BY \"Code\" DESC");

                SAPbouiCOM.Grid grid = (SAPbouiCOM.Grid)form.Items.Item("grdLog").Specific;

                for (int i = 0; i < grid.Columns.Count; i++)
                    grid.Columns.Item(i).Editable = false;

                grid.AutoResizeColumns();
                SBO_Application.SetStatusBarMessage("Log actualizado: " + dt.Rows.Count + " filas",
                    BoMessageTime.bmt_Short, false);
            }
            catch (Exception ex)
            {
                SBO_Application.MessageBox("No se pudo cargar el log: " + ex.Message);
            }
        }

        // ---------------------------------------------------------------
        // FORMULARIO PROPIO: INTENTOS BLOQUEADOS (GRILLA)
        // ---------------------------------------------------------------
        private void OpenBloqForm()
        {
            if (!bloqueosHabilitado)
            {
                SBO_Application.MessageBox("El registro de bloqueos solo está disponible en bases de prueba.");
                return;
            }

            foreach (SAPbouiCOM.Form f in SBO_Application.Forms)
            {
                if (f.TypeEx == "MPA_BloqForm")
                {
                    f.Select();
                    CargarBloqueos(f);
                    return;
                }
            }

            FormCreationParams fcp = (FormCreationParams)SBO_Application
                .CreateObject(BoCreatableObjectType.cot_FormCreationParams);
            fcp.BorderStyle = BoFormBorderStyle.fbs_Sizable;
            fcp.FormType = "MPA_BloqForm";

            SAPbouiCOM.Form form = SBO_Application.Forms.AddEx(fcp);
            form.Title = "Intentos bloqueados por las validaciones";
            form.Left = 250;
            form.Top = 80;
            form.Width = 780;
            form.Height = 330;

            form.DataSources.DataTables.Add("dtBloq");

            Item btn = form.Items.Add("btnRefB", BoFormItemTypes.it_BUTTON);
            btn.Left = 10; btn.Top = 10; btn.Width = 100; btn.Height = 20;
            ((SAPbouiCOM.Button)btn.Specific).Caption = "Actualizar";

            Item grd = form.Items.Add("grdBloq", BoFormItemTypes.it_GRID);
            grd.Left = 10; grd.Top = 40; grd.Width = 750; grd.Height = 240;
            ((SAPbouiCOM.Grid)grd.Specific).DataTable = form.DataSources.DataTables.Item("dtBloq");

            CargarBloqueos(form);
            form.Visible = true;
        }

        private void CargarBloqueos(SAPbouiCOM.Form form)
        {
            try
            {
                SAPbouiCOM.DataTable dt = form.DataSources.DataTables.Item("dtBloq");

                dt.ExecuteQuery(
                    "SELECT TOP 100 \"Code\" AS \"Nro\", " +
                    "\"U_Fecha\" AS \"Fecha\", " +
                    "\"U_Hora\" AS \"Hora\", " +
                    "\"U_Usuario\" AS \"Usuario\", " +
                    "\"U_Documento\" AS \"Documento\", " +
                    "\"U_Accion\" AS \"Acción\", " +
                    "\"U_NroDoc\" AS \"Nro doc.\", " +
                    "\"U_Motivo\" AS \"Motivo\" " +
                    "FROM \"@MPA_BLOQ\" ORDER BY \"Code\" DESC");

                SAPbouiCOM.Grid grid = (SAPbouiCOM.Grid)form.Items.Item("grdBloq").Specific;

                for (int i = 0; i < grid.Columns.Count; i++)
                    grid.Columns.Item(i).Editable = false;

                grid.AutoResizeColumns();
            }
            catch (Exception ex)
            {
                SBO_Application.MessageBox("No se pudo cargar el registro de bloqueos: " + ex.Message);
            }
        }

        // Refresca la grilla de bloqueos si el formulario está abierto
        private void RefrescarBloqueos()
        {
            foreach (SAPbouiCOM.Form f in SBO_Application.Forms)
                if (f.TypeEx == "MPA_BloqForm")
                    CargarBloqueos(f);
        }

        // ---------------------------------------------------------------
        // FORMULARIO PROPIO: CONFIGURACIÓN
        // ---------------------------------------------------------------
        private void OpenCfgForm()
        {
            if (!baseDePrueba)
            {
                SBO_Application.MessageBox("La configuración solo está disponible en bases de prueba.");
                return;
            }

            foreach (SAPbouiCOM.Form f in SBO_Application.Forms)
            {
                if (f.TypeEx == "MPA_CfgForm")
                {
                    f.Select();
                    return;
                }
            }

            FormCreationParams fcp = (FormCreationParams)SBO_Application
                .CreateObject(BoCreatableObjectType.cot_FormCreationParams);
            fcp.BorderStyle = BoFormBorderStyle.fbs_Sizable;
            fcp.FormType = "MPA_CfgForm";

            SAPbouiCOM.Form form = SBO_Application.Forms.AddEx(fcp);
            form.Title = "Configuración de Mi Primer Addon";
            form.Left = 400;
            form.Top = 120;
            form.Width = 500;
            form.Height = 270;

            form.DataSources.UserDataSources.Add("dsLimite", BoDataType.dt_SHORT_TEXT, 20);
            form.DataSources.UserDataSources.Add("dsActiva", BoDataType.dt_SHORT_TEXT, 1);
            form.DataSources.UserDataSources.Add("dsCondPago", BoDataType.dt_SHORT_TEXT, 1);
            form.DataSources.UserDataSources.Add("dsExentos", BoDataType.dt_SHORT_TEXT, 254);

            Item info = form.Items.Add("lblInfo", BoFormItemTypes.it_STATIC);
            info.Left = 20; info.Top = 15; info.Width = 460; info.Height = 16;
            ((StaticText)info.Specific).Caption =
                "Las facturas y órdenes de venta sobre este monto exigen un comentario.";

            AddLabel(form, "lblLimite", "Monto límite:", 20, 45);

            Item txt = form.Items.Add("txtLimite", BoFormItemTypes.it_EDIT);
            txt.Left = 100; txt.Top = 45; txt.Width = 150; txt.Height = 16;
            ((EditText)txt.Specific).DataBind.SetBound(true, "", "dsLimite");

            // Interruptor general de las validaciones (guarda "Y" o "N")
            Item chk = form.Items.Add("chkActiva", BoFormItemTypes.it_CHECK_BOX);
            chk.Left = 20; chk.Top = 75; chk.Width = 460; chk.Height = 16;
            SAPbouiCOM.CheckBox cb = (SAPbouiCOM.CheckBox)chk.Specific;
            cb.Caption = "Validaciones activas (comentario y empleado de ventas)";
            cb.ValOn = "Y";
            cb.ValOff = "N";
            cb.DataBind.SetBound(true, "", "dsActiva");

            // Regla opcional: exigir una condición de pago distinta de FONDO
            // (el identificador del ítem no puede superar los 10 caracteres)
            Item chk2 = form.Items.Add("chkCond", BoFormItemTypes.it_CHECK_BOX);
            chk2.Left = 20; chk2.Top = 100; chk2.Width = 460; chk2.Height = 16;
            SAPbouiCOM.CheckBox cb2 = (SAPbouiCOM.CheckBox)chk2.Specific;
            cb2.Caption = "Exigir condición de pago distinta de FONDO";
            cb2.ValOn = "Y";
            cb2.ValOff = "N";
            cb2.DataBind.SetBound(true, "", "dsCondPago");

            // Usuarios que se saltan todas las reglas (códigos separados por punto y coma)
            Item lblEx = form.Items.Add("lblExent", BoFormItemTypes.it_STATIC);
            lblEx.Left = 20; lblEx.Top = 130; lblEx.Width = 460; lblEx.Height = 16;
            ((StaticText)lblEx.Specific).Caption =
                "Usuarios exentos de las validaciones (códigos separados por ;):";

            Item txtEx = form.Items.Add("txtExent", BoFormItemTypes.it_EDIT);
            txtEx.Left = 20; txtEx.Top = 150; txtEx.Width = 440; txtEx.Height = 16;
            ((EditText)txtEx.Specific).DataBind.SetBound(true, "", "dsExentos");

            // Valores actuales desde la tabla de configuración
            double limiteActual = LeerConfig(CfgLimite, LimiteDefecto);
            bool activaActual = LeerConfig(CfgActiva, ActivaDefecto) == 1;
            bool condPagoActual = LeerConfig(CfgCondPago, CondPagoDefecto) == 1;

            form.DataSources.UserDataSources.Item("dsLimite").ValueEx =
                limiteActual.ToString("N0", new CultureInfo("es-CL"));
            form.DataSources.UserDataSources.Item("dsActiva").ValueEx = activaActual ? "Y" : "N";
            form.DataSources.UserDataSources.Item("dsCondPago").ValueEx = condPagoActual ? "Y" : "N";
            form.DataSources.UserDataSources.Item("dsExentos").ValueEx = LeerConfigTexto(CfgExentos, "");

            Item btn = form.Items.Add("btnGuardar", BoFormItemTypes.it_BUTTON);
            btn.Left = 20; btn.Top = 185; btn.Width = 100; btn.Height = 20;
            ((SAPbouiCOM.Button)btn.Specific).Caption = "Guardar";

            form.Visible = true;
        }

        private void GuardarCfg(string formUID)
        {
            try
            {
                SAPbouiCOM.Form form = SBO_Application.Forms.Item(formUID);
                string texto = form.DataSources.UserDataSources.Item("dsLimite").ValueEx;
                bool activa = form.DataSources.UserDataSources.Item("dsActiva").ValueEx == "Y";
                bool condPago = form.DataSources.UserDataSources.Item("dsCondPago").ValueEx == "Y";
                string exentos = NormalizarUsuarios(form.DataSources.UserDataSources.Item("dsExentos").ValueEx);

                // Se queda solo con los dígitos, así "1.000.000" y "1000000" valen lo mismo
                string digitos = "";
                foreach (char c in texto)
                    if (char.IsDigit(c)) digitos += c;

                double nuevo;
                if (digitos.Length == 0 || digitos.Length > 15
                    || !double.TryParse(digitos, NumberStyles.None, CultureInfo.InvariantCulture, out nuevo)
                    || nuevo <= 0)
                {
                    SBO_Application.MessageBox("Ingresa un monto válido, mayor que cero.");
                    return;
                }

                SAPbobsCOM.Company oCompany =
                    (SAPbobsCOM.Company)SBO_Application.Company.GetDICompany();

                GuardarParametro(oCompany, CfgLimite, "Limite sin comentario", nuevo);
                GuardarParametro(oCompany, CfgActiva, "Validacion activa", activa ? 1 : 0);
                GuardarParametro(oCompany, CfgCondPago, "Exigir condicion de pago", condPago ? 1 : 0);
                GuardarParametroTexto(oCompany, CfgExentos, "Usuarios exentos", exentos);

                form.DataSources.UserDataSources.Item("dsLimite").ValueEx =
                    nuevo.ToString("N0", new CultureInfo("es-CL"));
                form.DataSources.UserDataSources.Item("dsExentos").ValueEx = exentos;

                SBO_Application.SetStatusBarMessage(
                    "Configuración guardada (validaciones " + (activa ? "activas" : "desactivadas") +
                    ", condición de pago " + (condPago ? "exigida" : "no exigida") +
                    ", exentos: " + (exentos.Length == 0 ? "ninguno" : exentos) + ")",
                    BoMessageTime.bmt_Short, false);
            }
            catch (Exception ex)
            {
                SBO_Application.MessageBox("No se pudo guardar: " + ex.Message);
            }
        }

        // Deja la lista como "usuario1;usuario2": sin espacios sobrantes, sin vacíos y con ; como separador
        private string NormalizarUsuarios(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return "";

            List<string> usuarios = new List<string>();
            foreach (string u in texto.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string limpio = u.Trim();
                if (limpio.Length > 0) usuarios.Add(limpio);
            }

            string resultado = string.Join(";", usuarios);
            return resultado.Length > 254 ? resultado.Substring(0, 254) : resultado;
        }

        // Guarda un parámetro numérico: si ya existe lo actualiza y si no, lo crea
        private void GuardarParametro(SAPbobsCOM.Company oCompany, string codigo, string nombre, double valor)
        {
            SAPbobsCOM.UserTable ut = oCompany.UserTables.Item("MPA_CONFIG");
            try
            {
                if (ut.GetByKey(codigo))
                {
                    ut.UserFields.Fields.Item("U_Valor").Value = valor;
                    if (ut.Update() != 0)
                        LanzarErrorDI(oCompany);
                }
                else
                {
                    ut.Code = codigo;
                    ut.Name = nombre;
                    ut.UserFields.Fields.Item("U_Valor").Value = valor;
                    if (ut.Add() != 0)
                        LanzarErrorDI(oCompany);
                }
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.ReleaseComObject(ut);
            }
        }

        // Guarda un parámetro de texto (campo U_Texto): si ya existe lo actualiza y si no, lo crea
        private void GuardarParametroTexto(SAPbobsCOM.Company oCompany, string codigo, string nombre, string texto)
        {
            SAPbobsCOM.UserTable ut = oCompany.UserTables.Item("MPA_CONFIG");
            try
            {
                if (ut.GetByKey(codigo))
                {
                    ut.UserFields.Fields.Item("U_Texto").Value = texto;
                    if (ut.Update() != 0)
                        LanzarErrorDI(oCompany);
                }
                else
                {
                    ut.Code = codigo;
                    ut.Name = nombre;
                    ut.UserFields.Fields.Item("U_Texto").Value = texto;
                    if (ut.Add() != 0)
                        LanzarErrorDI(oCompany);
                }
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.ReleaseComObject(ut);
            }
        }

        // Lee un parámetro numérico de @MPA_CONFIG; si no existe o hay error, devuelve el valor por defecto
        private double LeerConfig(string codigo, double defecto)
        {
            try
            {
                SAPbobsCOM.Company oCompany =
                    (SAPbobsCOM.Company)SBO_Application.Company.GetDICompany();

                SAPbobsCOM.Recordset r = (SAPbobsCOM.Recordset)oCompany
                    .GetBusinessObject(SAPbobsCOM.BoObjectTypes.BoRecordset);
                try
                {
                    r.DoQuery("SELECT \"U_Valor\" FROM \"@MPA_CONFIG\" WHERE \"Code\" = '" + codigo + "'");
                    if (r.EoF) return defecto;
                    return Convert.ToDouble(r.Fields.Item("U_Valor").Value);
                }
                finally
                {
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(r);
                }
            }
            catch
            {
                return defecto;
            }
        }

        // Lee un parámetro de texto de @MPA_CONFIG; si no existe o hay error, devuelve el valor por defecto
        private string LeerConfigTexto(string codigo, string defecto)
        {
            try
            {
                SAPbobsCOM.Company oCompany =
                    (SAPbobsCOM.Company)SBO_Application.Company.GetDICompany();

                SAPbobsCOM.Recordset r = (SAPbobsCOM.Recordset)oCompany
                    .GetBusinessObject(SAPbobsCOM.BoObjectTypes.BoRecordset);
                try
                {
                    r.DoQuery("SELECT \"U_Texto\" FROM \"@MPA_CONFIG\" WHERE \"Code\" = '" + codigo + "'");
                    if (r.EoF) return defecto;

                    object valor = r.Fields.Item("U_Texto").Value;
                    return valor == null ? defecto : Convert.ToString(valor).Trim();
                }
                finally
                {
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(r);
                }
            }
            catch
            {
                return defecto;
            }
        }

        // ¿El usuario está en la lista de exentos? (se compara sin distinguir mayúsculas)
        private bool UsuarioExento(string usuario)
        {
            if (string.IsNullOrEmpty(usuario)) return false;

            string lista = LeerConfigTexto(CfgExentos, "");
            if (lista.Length == 0) return false;

            foreach (string u in lista.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.Equals(u.Trim(), usuario.Trim(), StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        // ---------------------------------------------------------------
        // EVENTOS DE ÍTEMS
        // ---------------------------------------------------------------
        private void OnItemEvent(string FormUID, ref ItemEvent pVal, out bool BubbleEvent)
        {
            BubbleEvent = true;

            // 1) Botón Consultar de mi formulario de tipos de cambio
            if (pVal.FormTypeEx == "MPA_Form"
                && pVal.ItemUID == "btnOk"
                && pVal.EventType == BoEventTypes.et_ITEM_PRESSED
                && !pVal.BeforeAction)
            {
                ConsultarTipoCambios(FormUID);
                return;
            }

            // 1b) Botón Actualizar del formulario de log
            if (pVal.FormTypeEx == "MPA_LogForm"
                && pVal.ItemUID == "btnRef"
                && pVal.EventType == BoEventTypes.et_ITEM_PRESSED
                && !pVal.BeforeAction)
            {
                CargarLog(SBO_Application.Forms.Item(FormUID));
                return;
            }

            // 1b2) Botón Actualizar del formulario de bloqueos
            if (pVal.FormTypeEx == "MPA_BloqForm"
                && pVal.ItemUID == "btnRefB"
                && pVal.EventType == BoEventTypes.et_ITEM_PRESSED
                && !pVal.BeforeAction)
            {
                CargarBloqueos(SBO_Application.Forms.Item(FormUID));
                return;
            }

            // 1c) Validar al grabar o actualizar un documento de la lista
            //     (antes de que SAP ejecute la acción)
            if (ValidaComentario.ContainsKey(pVal.FormTypeEx)
                && pVal.ItemUID == "1"
                && pVal.EventType == BoEventTypes.et_ITEM_PRESSED
                && pVal.BeforeAction)
            {
                BubbleEvent = ValidarDocumento(FormUID, ValidaComentario[pVal.FormTypeEx]);
                return;
            }

            // 1d) Botón Guardar del formulario de configuración
            if (pVal.FormTypeEx == "MPA_CfgForm"
                && pVal.ItemUID == "btnGuardar"
                && pVal.EventType == BoEventTypes.et_ITEM_PRESSED
                && !pVal.BeforeAction)
            {
                GuardarCfg(FormUID);
                return;
            }

            // 2) Al abrirse un documento de la lista, agregarle el botón
            if (DocTablas.ContainsKey(pVal.FormTypeEx)
                && pVal.EventType == BoEventTypes.et_FORM_LOAD
                && !pVal.BeforeAction)
            {
                AgregarBotonFactura(FormUID);
                return;
            }

            // 3) Clic en el botón agregado al documento
            if (DocTablas.ContainsKey(pVal.FormTypeEx)
                && pVal.ItemUID == "btnMPA"
                && pVal.EventType == BoEventTypes.et_ITEM_PRESSED
                && !pVal.BeforeAction)
            {
                MostrarDolarDocumento(FormUID, DocTablas[pVal.FormTypeEx]);
            }
        }

        // ---------------------------------------------------------------
        // CONSULTA DE TIPOS DE CAMBIO
        // ---------------------------------------------------------------
        private void ConsultarTipoCambios(string formUID)
        {
            SAPbobsCOM.Recordset rs = null;
            try
            {
                SAPbouiCOM.Form form = SBO_Application.Forms.Item(formUID);

                string fecha = form.DataSources.UserDataSources.Item("dsFecha").ValueEx;
                DateTime dt;
                if (!DateTime.TryParseExact(fecha, "yyyyMMdd", CultureInfo.InvariantCulture,
                                            DateTimeStyles.None, out dt))
                {
                    SBO_Application.MessageBox("Ingresa una fecha válida.");
                    return;
                }

                SAPbobsCOM.Company oCompany =
                    (SAPbobsCOM.Company)SBO_Application.Company.GetDICompany();
                rs = (SAPbobsCOM.Recordset)oCompany
                    .GetBusinessObject(SAPbobsCOM.BoObjectTypes.BoRecordset);

                form.DataSources.UserDataSources.Item("dsUsd").ValueEx = ObtenerTipoCambio(rs, "US$", fecha);
                form.DataSources.UserDataSources.Item("dsEur").ValueEx = ObtenerTipoCambio(rs, "EUR", fecha);
                form.DataSources.UserDataSources.Item("dsUf").ValueEx = ObtenerTipoCambio(rs, "UF", fecha);

                // Registrar la consulta en el log, con la fecha de vigencia de cada tasa
                foreach (string m in new[] { "US$", "EUR", "UF" })
                {
                    DateTime vigencia;
                    double tasa = ObtenerTasaFecha(rs, m, fecha, out vigencia);
                    RegistrarConsulta(oCompany, rs, m, fecha, tasa, vigencia);
                }

                // Si el formulario de log está abierto, refrescarlo
                foreach (SAPbouiCOM.Form f in SBO_Application.Forms)
                    if (f.TypeEx == "MPA_LogForm")
                        CargarLog(f);

                SBO_Application.SetStatusBarMessage("Consulta realizada",
                    BoMessageTime.bmt_Short, false);
            }
            catch (Exception ex)
            {
                SBO_Application.MessageBox("Error: " + ex.Message);
            }
            finally
            {
                if (rs != null)
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(rs);
            }
        }

        private string ObtenerTipoCambio(SAPbobsCOM.Recordset rs, string moneda, string fecha)
        {
            DateTime vigencia;
            double valor = ObtenerTasaFecha(rs, moneda, fecha, out vigencia);

            if (valor <= 0)
                return "Sin datos";

            return valor.ToString("N2", new CultureInfo("es-CL"))
                 + "  (" + vigencia.ToString("dd-MM-yyyy") + ")";
        }

        // Devuelve la tasa vigente a la fecha pedida y, en 'vigencia', la fecha real de esa tasa
        private double ObtenerTasaFecha(SAPbobsCOM.Recordset rs, string moneda, string fecha,
            out DateTime vigencia)
        {
            vigencia = DateTime.MinValue;

            rs.DoQuery("SELECT TOP 1 \"RateDate\", \"Rate\" FROM \"ORTT\" " +
                       "WHERE \"Currency\" = '" + moneda + "' AND \"RateDate\" <= '" + fecha + "' " +
                       "ORDER BY \"RateDate\" DESC");

            if (rs.EoF)
                return 0;

            vigencia = (DateTime)rs.Fields.Item("RateDate").Value;
            return Convert.ToDouble(rs.Fields.Item("Rate").Value);
        }

        private double ObtenerTasa(SAPbobsCOM.Recordset rs, string moneda, string fecha)
        {
            DateTime vigencia;
            return ObtenerTasaFecha(rs, moneda, fecha, out vigencia);
        }

        // ---------------------------------------------------------------
        // BOTÓN EN LOS DOCUMENTOS DE SAP
        // ---------------------------------------------------------------
        private void AgregarBotonFactura(string formUID)
        {
            try
            {
                SAPbouiCOM.Form form = SBO_Application.Forms.Item(formUID);

                Item cancelar = form.Items.Item("2");

                Item btn = form.Items.Add("btnMPA", BoFormItemTypes.it_BUTTON);
                btn.Left = cancelar.Left + cancelar.Width + 10;
                btn.Top = cancelar.Top;
                btn.Width = 110;
                btn.Height = cancelar.Height;
                ((SAPbouiCOM.Button)btn.Specific).Caption = "Dólar del doc.";
            }
            catch (Exception ex)
            {
                SBO_Application.SetStatusBarMessage("No se pudo agregar el botón: " + ex.Message,
                    BoMessageTime.bmt_Short, true);
            }
        }

        private void MostrarDolarDocumento(string formUID, string tabla)
        {
            SAPbobsCOM.Recordset rs = null;
            try
            {
                SAPbouiCOM.Form form = SBO_Application.Forms.Item(formUID);

                SAPbouiCOM.DBDataSource ds = form.DataSources.DBDataSources.Item(tabla);
                string cliente = ds.GetValue("CardName", 0).Trim();
                string fecha = ds.GetValue("DocDate", 0).Trim();
                string moneda = ds.GetValue("DocCur", 0).Trim();
                string totalTxt = ds.GetValue("DocTotal", 0).Trim();

                DateTime dt;
                if (!DateTime.TryParseExact(fecha, "yyyyMMdd", CultureInfo.InvariantCulture,
                                            DateTimeStyles.None, out dt))
                {
                    SBO_Application.MessageBox("El documento aún no tiene una fecha válida.");
                    return;
                }

                double total;
                double.TryParse(totalTxt, NumberStyles.Any, CultureInfo.InvariantCulture, out total);

                SAPbobsCOM.Company oCompany =
                    (SAPbobsCOM.Company)SBO_Application.Company.GetDICompany();
                rs = (SAPbobsCOM.Recordset)oCompany
                    .GetBusinessObject(SAPbobsCOM.BoObjectTypes.BoRecordset);

                string detalleDolar = ObtenerTipoCambio(rs, "US$", fecha);
                double tasa = ObtenerTasa(rs, "US$", fecha);

                CultureInfo cl = new CultureInfo("es-CL");
                string msg = (tabla == "OPCH" ? "Proveedor: " : "Cliente: ") + cliente
                           + "\nFecha del documento: " + dt.ToString("dd-MM-yyyy")
                           + "\nTotal: " + total.ToString("N2", cl) + " " + moneda;

                if (tasa <= 0)
                {
                    msg += "\nNo hay dólar cargado hasta esa fecha.";
                }
                else
                {
                    msg += "\nDólar vigente: " + detalleDolar;
                    if (moneda != "US$")
                        msg += "\nEquivalente aprox.: US$ " + (total / tasa).ToString("N2", cl);
                }

                SBO_Application.MessageBox(msg);
            }
            catch (Exception ex)
            {
                SBO_Application.MessageBox("Error: " + ex.Message);
            }
            finally
            {
                if (rs != null)
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(rs);
            }
        }

        // ---------------------------------------------------------------
        // VALIDACIÓN AL GRABAR O ACTUALIZAR UN DOCUMENTO DE VENTAS
        // ---------------------------------------------------------------
        // 'tabla' es la cabecera del documento (OINV, ORDR...).
        // Devuelve true para dejar pasar y false para cancelar la acción.
        private bool ValidarDocumento(string formUID, string tabla)
        {
            if (!baseDePrueba) return true;

            try
            {
                // Interruptor general, configurable desde el formulario de Configuración
                if (LeerConfig(CfgActiva, ActivaDefecto) != 1) return true;

                // Los usuarios de la lista de exentos se saltan todas las reglas
                string usuario = SBO_Application.Company.UserName;
                if (UsuarioExento(usuario)) return true;

                SAPbouiCOM.Form form = SBO_Application.Forms.Item(formUID);

                // Se valida al crear (ADD) y al actualizar (UPDATE).
                // En OK_MODE el botón "1" solo cierra el formulario, y en FIND_MODE busca.
                bool creando = form.Mode == BoFormMode.fm_ADD_MODE;
                bool actualizando = form.Mode == BoFormMode.fm_UPDATE_MODE;
                if (!creando && !actualizando) return true;

                SAPbouiCOM.DBDataSource ds = form.DataSources.DBDataSources.Item(tabla);

                double total;
                double.TryParse(ds.GetValue("DocTotal", 0).Trim(),
                    NumberStyles.Any, CultureInfo.InvariantCulture, out total);
                string comentario = ds.GetValue("Comments", 0).Trim();
                string empleado = ds.GetValue("SlpCode", 0).Trim();
                string condPago = ds.GetValue("GroupNum", 0).Trim();
                string nroDoc = ds.GetValue("DocNum", 0).Trim();

                double limite = LeerConfig(CfgLimite, LimiteDefecto);
                bool exigirCondPago = LeerConfig(CfgCondPago, CondPagoDefecto) == 1;
                CultureInfo cl = new CultureInfo("es-CL");

                // Se juntan todos los problemas para mostrarlos en un solo mensaje
                List<string> problemas = new List<string>();

                if (total > limite && comentario.Length == 0)
                    problemas.Add("El total supera " + limite.ToString("N0", cl)
                                  + ": completa el campo Comentarios.");

                // En SAP, el valor -1 significa "ningún empleado del departamento de ventas"
                if (empleado.Length == 0 || empleado == "-1")
                    problemas.Add("Falta asignar el empleado del departamento de ventas.");

                // -1 es la condición de pago por omisión (FONDO): obliga a elegir una real
                if (exigirCondPago && (condPago.Length == 0 || condPago == CondicionPagoPorDefecto))
                    problemas.Add("Falta elegir la condición de pago (no puede quedar en FONDO).");

                if (problemas.Count == 0) return true;

                string documento = tabla == "ORDR" ? "la orden de venta" : "la factura de ventas";
                string nombreDoc = tabla == "ORDR" ? "Orden de venta" : "Factura de ventas";
                string accion = creando ? "grabar" : "actualizar";

                // Se deja constancia del intento bloqueado
                RegistrarBloqueo(usuario, nombreDoc, accion, nroDoc, problemas);

                string detalle = "";
                foreach (string p in problemas)
                    detalle += "\n- " + p;

                SBO_Application.MessageBox("No se puede " + accion + " " + documento + ":" + detalle);
                return false;   // cancela la acción
            }
            catch (Exception ex)
            {
                // Si la validación falla por un error del add-on, no se bloquea al usuario
                SBO_Application.SetStatusBarMessage("Validación omitida: " + ex.Message,
                    BoMessageTime.bmt_Short, true);
                return true;
            }
        }

        // Guarda una fila en @MPA_BLOQ. Si falla, solo avisa en la barra de estado y nunca cambia
        // el resultado de la validación.
        private void RegistrarBloqueo(string usuario, string documento, string accion,
            string nroDoc, List<string> problemas)
        {
            if (!bloqueosHabilitado) return;

            SAPbobsCOM.UserTable ut = null;
            try
            {
                SAPbobsCOM.Company oCompany =
                    (SAPbobsCOM.Company)SBO_Application.Company.GetDICompany();

                int sig = SiguienteCodigo(oCompany, "@MPA_BLOQ");

                string motivo = string.Join(" | ", problemas);
                if (motivo.Length > 254) motivo = motivo.Substring(0, 254);

                ut = oCompany.UserTables.Item("MPA_BLOQ");
                ut.Code = sig.ToString("D8");
                ut.Name = ut.Code;
                ut.UserFields.Fields.Item("U_Fecha").Value = DateTime.Today;
                ut.UserFields.Fields.Item("U_Hora").Value = DateTime.Now.ToString("HH:mm:ss");
                ut.UserFields.Fields.Item("U_Usuario").Value = usuario;
                ut.UserFields.Fields.Item("U_Documento").Value = documento;
                ut.UserFields.Fields.Item("U_Accion").Value = accion;
                ut.UserFields.Fields.Item("U_NroDoc").Value = nroDoc;
                ut.UserFields.Fields.Item("U_Motivo").Value = motivo;

                if (ut.Add() != 0)
                    LanzarErrorDI(oCompany);

                RefrescarBloqueos();
            }
            catch (Exception ex)
            {
                SBO_Application.SetStatusBarMessage("No se pudo registrar el bloqueo: " + ex.Message,
                    BoMessageTime.bmt_Short, true);
            }
            finally
            {
                if (ut != null)
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(ut);
            }
        }

        // Siguiente código correlativo de una tabla de usuario (por ejemplo "@MPA_BLOQ")
        private int SiguienteCodigo(SAPbobsCOM.Company oCompany, string tablaSql)
        {
            SAPbobsCOM.Recordset r = (SAPbobsCOM.Recordset)oCompany
                .GetBusinessObject(SAPbobsCOM.BoObjectTypes.BoRecordset);
            try
            {
                r.DoQuery("SELECT IFNULL(MAX(TO_INTEGER(\"Code\")), 0) + 1 AS \"Sig\" " +
                          "FROM \"" + tablaSql + "\"");
                return Convert.ToInt32(r.Fields.Item("Sig").Value);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.ReleaseComObject(r);
            }
        }

        // ---------------------------------------------------------------
        // TABLAS DE USUARIO: @MPA_LOG, @MPA_CONFIG Y @MPA_BLOQ
        // ---------------------------------------------------------------
        private void AsegurarTablas()
        {
            string paso = "inicio";
            try
            {
                SAPbobsCOM.Company oCompany =
                    (SAPbobsCOM.Company)SBO_Application.Company.GetDICompany();

                // Resguardo: solo bases de prueba
                if (!oCompany.CompanyDB.ToUpper().Contains("TEST"))
                {
                    SBO_Application.MessageBox("Log, validaciones y configuración deshabilitados. Base actual: "
                        + oCompany.CompanyDB);
                    return;
                }

                baseDePrueba = true;

                // --- Tabla de log ---
                paso = "tabla MPA_LOG";
                CrearTabla(oCompany, "MPA_LOG", "MPA Log consultas TC");

                paso = "campo U_Fecha";
                CrearCampo(oCompany, "@MPA_LOG", "Fecha", "Fecha consultada",
                    SAPbobsCOM.BoFieldTypes.db_Date, 0, SAPbobsCOM.BoFldSubTypes.st_None);

                paso = "campo U_Moneda";
                CrearCampo(oCompany, "@MPA_LOG", "Moneda", "Moneda",
                    SAPbobsCOM.BoFieldTypes.db_Alpha, 10, SAPbobsCOM.BoFldSubTypes.st_None);

                paso = "campo U_Tasa";
                CrearCampo(oCompany, "@MPA_LOG", "Tasa", "Tipo de cambio",
                    SAPbobsCOM.BoFieldTypes.db_Float, 0, SAPbobsCOM.BoFldSubTypes.st_Rate);

                paso = "campo U_Usuario";
                CrearCampo(oCompany, "@MPA_LOG", "Usuario", "Usuario que consulta",
                    SAPbobsCOM.BoFieldTypes.db_Alpha, 30, SAPbobsCOM.BoFldSubTypes.st_None);

                paso = "campo U_FechaTC";
                CrearCampo(oCompany, "@MPA_LOG", "FechaTC", "Vigencia del valor",
                    SAPbobsCOM.BoFieldTypes.db_Date, 0, SAPbobsCOM.BoFldSubTypes.st_None);

                logHabilitado = true;

                // --- Tabla de configuración ---
                paso = "tabla MPA_CONFIG";
                CrearTabla(oCompany, "MPA_CONFIG", "MPA Configuracion");

                paso = "campo U_Valor";
                CrearCampo(oCompany, "@MPA_CONFIG", "Valor", "Valor del parametro",
                    SAPbobsCOM.BoFieldTypes.db_Float, 0, SAPbobsCOM.BoFldSubTypes.st_Sum);

                // Campo de texto para parámetros que no son números (por ejemplo la lista de exentos)
                paso = "campo U_Texto";
                CrearCampo(oCompany, "@MPA_CONFIG", "Texto", "Texto del parametro",
                    SAPbobsCOM.BoFieldTypes.db_Alpha, 254, SAPbobsCOM.BoFldSubTypes.st_None);

                // Los parámetros iniciales no son críticos: si fallan, LeerConfig usa el valor por defecto
                SembrarSeguro(oCompany, CfgLimite, "Limite sin comentario", LimiteDefecto);
                SembrarSeguro(oCompany, CfgActiva, "Validacion activa", ActivaDefecto);
                SembrarSeguro(oCompany, CfgCondPago, "Exigir condicion de pago", CondPagoDefecto);

                // --- Tabla de intentos bloqueados ---
                paso = "tabla MPA_BLOQ";
                CrearTabla(oCompany, "MPA_BLOQ", "MPA Intentos bloqueados");

                paso = "campo U_Fecha (bloqueos)";
                CrearCampo(oCompany, "@MPA_BLOQ", "Fecha", "Fecha del bloqueo",
                    SAPbobsCOM.BoFieldTypes.db_Date, 0, SAPbobsCOM.BoFldSubTypes.st_None);

                paso = "campo U_Hora";
                CrearCampo(oCompany, "@MPA_BLOQ", "Hora", "Hora del bloqueo",
                    SAPbobsCOM.BoFieldTypes.db_Alpha, 8, SAPbobsCOM.BoFldSubTypes.st_None);

                paso = "campo U_Usuario (bloqueos)";
                CrearCampo(oCompany, "@MPA_BLOQ", "Usuario", "Usuario",
                    SAPbobsCOM.BoFieldTypes.db_Alpha, 30, SAPbobsCOM.BoFldSubTypes.st_None);

                paso = "campo U_Documento";
                CrearCampo(oCompany, "@MPA_BLOQ", "Documento", "Tipo de documento",
                    SAPbobsCOM.BoFieldTypes.db_Alpha, 30, SAPbobsCOM.BoFldSubTypes.st_None);

                paso = "campo U_Accion";
                CrearCampo(oCompany, "@MPA_BLOQ", "Accion", "Accion intentada",
                    SAPbobsCOM.BoFieldTypes.db_Alpha, 15, SAPbobsCOM.BoFldSubTypes.st_None);

                paso = "campo U_NroDoc";
                CrearCampo(oCompany, "@MPA_BLOQ", "NroDoc", "Numero del documento",
                    SAPbobsCOM.BoFieldTypes.db_Alpha, 20, SAPbobsCOM.BoFldSubTypes.st_None);

                paso = "campo U_Motivo";
                CrearCampo(oCompany, "@MPA_BLOQ", "Motivo", "Motivo del bloqueo",
                    SAPbobsCOM.BoFieldTypes.db_Alpha, 254, SAPbobsCOM.BoFldSubTypes.st_None);

                bloqueosHabilitado = true;

                SBO_Application.SetStatusBarMessage("Tablas del add-on listas",
                    BoMessageTime.bmt_Short, false);
            }
            catch (Exception ex)
            {
                SBO_Application.MessageBox("Falló en el paso '" + paso + "': " + ex.Message);
            }
        }

        // Siembra un parámetro sin cortar el arranque si algo falla
        private void SembrarSeguro(SAPbobsCOM.Company oCompany, string codigo, string nombre, double valor)
        {
            try
            {
                SembrarParametro(oCompany, codigo, nombre, valor);
            }
            catch (Exception ex)
            {
                SBO_Application.SetStatusBarMessage(
                    "Parámetro " + codigo + " no creado (reinicia el add-on): " + ex.Message,
                    BoMessageTime.bmt_Short, true);
            }
        }

        // Crea una tabla de usuario si todavía no existe (sin Recordsets abiertos durante el Add)
        private void CrearTabla(SAPbobsCOM.Company oCompany, string nombre, string descripcion)
        {
            if (Contar(oCompany, "SELECT COUNT(*) AS \"N\" FROM \"OUTB\" WHERE \"TableName\" = '" + nombre + "'") > 0)
                return;

            SAPbobsCOM.UserTablesMD tabla = (SAPbobsCOM.UserTablesMD)oCompany
                .GetBusinessObject(SAPbobsCOM.BoObjectTypes.oUserTables);
            try
            {
                tabla.TableName = nombre;
                tabla.TableDescription = descripcion;
                tabla.TableType = SAPbobsCOM.BoUTBTableType.bott_NoObject;
                if (tabla.Add() != 0)
                    LanzarErrorDI(oCompany);
            }
            finally
            {
                LiberarCom(tabla);
                tabla = null;
            }
        }

        // Crea un campo de usuario en la tabla indicada (por ejemplo "@MPA_LOG") si no existe
        private void CrearCampo(SAPbobsCOM.Company oCompany, string tablaId, string alias,
            string descripcion, SAPbobsCOM.BoFieldTypes tipo, int tamano,
            SAPbobsCOM.BoFldSubTypes subtipo)
        {
            if (Contar(oCompany, "SELECT COUNT(*) AS \"N\" FROM \"CUFD\" " +
                                 "WHERE \"TableID\" = '" + tablaId + "' AND \"AliasID\" = '" + alias + "'") > 0)
                return;

            SAPbobsCOM.UserFieldsMD campo = (SAPbobsCOM.UserFieldsMD)oCompany
                .GetBusinessObject(SAPbobsCOM.BoObjectTypes.oUserFields);
            try
            {
                campo.TableName = tablaId;
                campo.Name = alias;
                campo.Description = descripcion;
                campo.Type = tipo;
                if (tamano > 0) campo.EditSize = tamano;
                campo.SubType = subtipo;

                if (campo.Add() != 0)
                    LanzarErrorDI(oCompany);
            }
            finally
            {
                LiberarCom(campo);
                campo = null;
            }
        }

        // Inserta el parámetro con su valor inicial, solo si todavía no existe
        private void SembrarParametro(SAPbobsCOM.Company oCompany, string codigo, string nombre, double valor)
        {
            if (Contar(oCompany, "SELECT COUNT(*) AS \"N\" FROM \"@MPA_CONFIG\" WHERE \"Code\" = '" + codigo + "'") > 0)
                return;

            SAPbobsCOM.UserTable ut = oCompany.UserTables.Item("MPA_CONFIG");
            try
            {
                ut.Code = codigo;
                ut.Name = nombre;
                ut.UserFields.Fields.Item("U_Valor").Value = valor;
                if (ut.Add() != 0)
                    LanzarErrorDI(oCompany);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.ReleaseComObject(ut);
            }
        }

        // Cuenta filas con un Recordset propio que se libera de inmediato
        private int Contar(SAPbobsCOM.Company oCompany, string sql)
        {
            SAPbobsCOM.Recordset r = (SAPbobsCOM.Recordset)oCompany
                .GetBusinessObject(SAPbobsCOM.BoObjectTypes.BoRecordset);
            try
            {
                r.DoQuery(sql);
                return Convert.ToInt32(r.Fields.Item("N").Value);
            }
            finally
            {
                LiberarCom(r);
                r = null;
            }
        }

        private void LiberarCom(object obj)
        {
            if (obj != null)
                System.Runtime.InteropServices.Marshal.ReleaseComObject(obj);
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        private void LanzarErrorDI(SAPbobsCOM.Company oCompany)
        {
            int codigo;
            string mensaje;
            oCompany.GetLastError(out codigo, out mensaje);
            throw new Exception(codigo + " - " + mensaje);
        }

        private void RegistrarConsulta(SAPbobsCOM.Company oCompany, SAPbobsCOM.Recordset rs,
            string moneda, string fecha, double tasa, DateTime vigencia)
        {
            if (!logHabilitado || tasa <= 0) return;

            SAPbobsCOM.UserTable ut = null;
            try
            {
                rs.DoQuery("SELECT IFNULL(MAX(TO_INTEGER(\"Code\")), 0) + 1 AS \"Sig\" " +
                           "FROM \"@MPA_LOG\"");
                int sig = Convert.ToInt32(rs.Fields.Item("Sig").Value);

                ut = oCompany.UserTables.Item("MPA_LOG");
                ut.Code = sig.ToString("D8");
                ut.Name = ut.Code;
                ut.UserFields.Fields.Item("U_Fecha").Value =
                    DateTime.ParseExact(fecha, "yyyyMMdd", CultureInfo.InvariantCulture);
                ut.UserFields.Fields.Item("U_Moneda").Value = moneda;
                ut.UserFields.Fields.Item("U_Tasa").Value = tasa;
                ut.UserFields.Fields.Item("U_FechaTC").Value = vigencia;
                ut.UserFields.Fields.Item("U_Usuario").Value = oCompany.UserName;

                if (ut.Add() != 0)
                    LanzarErrorDI(oCompany);
            }
            catch (Exception ex)
            {
                SBO_Application.SetStatusBarMessage("No se pudo registrar en el log: " + ex.Message,
                    BoMessageTime.bmt_Short, true);
            }
            finally
            {
                if (ut != null)
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(ut);
            }
        }

        // ---------------------------------------------------------------
        // EVENTO DE APLICACIÓN
        // ---------------------------------------------------------------
        private void OnAppEvent(BoAppEventTypes eventType)
        {
            if (eventType == BoAppEventTypes.aet_ShutDown ||
                eventType == BoAppEventTypes.aet_CompanyChanged ||
                eventType == BoAppEventTypes.aet_ServerTerminition)
            {
                System.Windows.Forms.Application.Exit();
            }
        }
    }
}