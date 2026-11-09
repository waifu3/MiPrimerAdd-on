using System;
using System.Globalization;
using SAPbouiCOM;

namespace MiprimerAddon
{
    public class Addon
    {
        public static SAPbouiCOM.Application SBO_Application;
        private bool logHabilitado = false;

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
            AsegurarTablaLog();
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

            MenuCreationParams sub = (MenuCreationParams)SBO_Application
                .CreateObject(BoCreatableObjectType.cot_MenuCreationParams);
            sub.Type = BoMenuType.mt_STRING;
            sub.UniqueID = "MPA_Form";
            sub.String = "Abrir formulario";
            sub.Enabled = true;
            main.SubMenus.AddEx(sub);

            MenuCreationParams sub2 = (MenuCreationParams)SBO_Application
                .CreateObject(BoCreatableObjectType.cot_MenuCreationParams);
            sub2.Type = BoMenuType.mt_STRING;
            sub2.UniqueID = "MPA_Log";
            sub2.String = "Ver log de consultas";
            sub2.Enabled = true;
            main.SubMenus.AddEx(sub2);
        }

        private void OnMenuEvent(ref MenuEvent pVal, out bool BubbleEvent)
        {
            BubbleEvent = true;

            if (pVal.BeforeAction) return;

            if (pVal.MenuUID == "MPA_Form")
                OpenForm();
            else if (pVal.MenuUID == "MPA_Log")
                OpenLogForm();
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
            form.Width = 560;
            form.Height = 330;

            form.DataSources.DataTables.Add("dtLog");

            Item btn = form.Items.Add("btnRef", BoFormItemTypes.it_BUTTON);
            btn.Left = 10; btn.Top = 10; btn.Width = 100; btn.Height = 20;
            ((SAPbouiCOM.Button)btn.Specific).Caption = "Actualizar";

            Item grd = form.Items.Add("grdLog", BoFormItemTypes.it_GRID);
            grd.Left = 10; grd.Top = 40; grd.Width = 530; grd.Height = 240;
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

            // 2) Al abrirse una Factura de ventas (tipo "133"), agregarle el botón
            if (pVal.FormTypeEx == "133"
                && pVal.EventType == BoEventTypes.et_FORM_LOAD
                && !pVal.BeforeAction)
            {
                AgregarBotonFactura(FormUID);
                return;
            }

            // 3) Clic en el botón agregado a la factura
            if (pVal.FormTypeEx == "133"
                && pVal.ItemUID == "btnMPA"
                && pVal.EventType == BoEventTypes.et_ITEM_PRESSED
                && !pVal.BeforeAction)
            {
                MostrarDolarDocumento(FormUID);
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

                // Registrar la consulta en la tabla de log (solo si está habilitada)
                foreach (string m in new[] { "US$", "EUR", "UF" })
                    RegistrarConsulta(oCompany, rs, m, fecha, ObtenerTasa(rs, m, fecha));

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
            rs.DoQuery("SELECT TOP 1 \"RateDate\", \"Rate\" FROM \"ORTT\" " +
                       "WHERE \"Currency\" = '" + moneda + "' AND \"RateDate\" <= '" + fecha + "' " +
                       "ORDER BY \"RateDate\" DESC");

            if (rs.EoF)
                return "Sin datos";

            double valor = Convert.ToDouble(rs.Fields.Item("Rate").Value);
            DateTime fechaTc = (DateTime)rs.Fields.Item("RateDate").Value;

            return valor.ToString("N2", new CultureInfo("es-CL"))
                 + "  (" + fechaTc.ToString("dd-MM-yyyy") + ")";
        }

        private double ObtenerTasa(SAPbobsCOM.Recordset rs, string moneda, string fecha)
        {
            rs.DoQuery("SELECT TOP 1 \"Rate\" FROM \"ORTT\" " +
                       "WHERE \"Currency\" = '" + moneda + "' AND \"RateDate\" <= '" + fecha + "' " +
                       "ORDER BY \"RateDate\" DESC");

            return rs.EoF ? 0 : Convert.ToDouble(rs.Fields.Item("Rate").Value);
        }

        // ---------------------------------------------------------------
        // BOTÓN EN LA FACTURA DE VENTAS
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

        private void MostrarDolarDocumento(string formUID)
        {
            SAPbobsCOM.Recordset rs = null;
            try
            {
                SAPbouiCOM.Form form = SBO_Application.Forms.Item(formUID);

                SAPbouiCOM.DBDataSource ds = form.DataSources.DBDataSources.Item("OINV");
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
                string msg = "Cliente: " + cliente
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
        // TABLA DE USUARIO @MPA_LOG
        // ---------------------------------------------------------------
        private void AsegurarTablaLog()
        {
            string paso = "inicio";
            try
            {
                SAPbobsCOM.Company oCompany =
                    (SAPbobsCOM.Company)SBO_Application.Company.GetDICompany();

                // Resguardo: solo bases de prueba
                if (!oCompany.CompanyDB.ToUpper().Contains("TEST"))
                {
                    SBO_Application.MessageBox("Log deshabilitado. Base actual: " + oCompany.CompanyDB);
                    return;
                }

                // 1) Tabla (sin ningún Recordset abierto durante el Add)
                paso = "tabla MPA_LOG";
                if (Contar(oCompany, "SELECT COUNT(*) AS \"N\" FROM \"OUTB\" WHERE \"TableName\" = 'MPA_LOG'") == 0)
                {
                    SAPbobsCOM.UserTablesMD tabla = (SAPbobsCOM.UserTablesMD)oCompany
                        .GetBusinessObject(SAPbobsCOM.BoObjectTypes.oUserTables);
                    try
                    {
                        tabla.TableName = "MPA_LOG";
                        tabla.TableDescription = "MPA Log consultas TC";
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

                // 2) Campos
                paso = "campo U_Fecha";
                CrearCampo(oCompany, "Fecha", "Fecha consultada",
                    SAPbobsCOM.BoFieldTypes.db_Date, 0, SAPbobsCOM.BoFldSubTypes.st_None);

                paso = "campo U_Moneda";
                CrearCampo(oCompany, "Moneda", "Moneda",
                    SAPbobsCOM.BoFieldTypes.db_Alpha, 10, SAPbobsCOM.BoFldSubTypes.st_None);

                paso = "campo U_Tasa";
                CrearCampo(oCompany, "Tasa", "Tipo de cambio",
                    SAPbobsCOM.BoFieldTypes.db_Float, 0, SAPbobsCOM.BoFldSubTypes.st_Rate);

                paso = "campo U_Usuario";
                CrearCampo(oCompany, "Usuario", "Usuario que consulta",
                    SAPbobsCOM.BoFieldTypes.db_Alpha, 30, SAPbobsCOM.BoFldSubTypes.st_None);

                logHabilitado = true;
                SBO_Application.SetStatusBarMessage("Log de consultas habilitado",
                    BoMessageTime.bmt_Short, false);
            }
            catch (Exception ex)
            {
                SBO_Application.MessageBox("Falló en el paso '" + paso + "': " + ex.Message);
            }
        }

        private void CrearCampo(SAPbobsCOM.Company oCompany, string alias, string descripcion,
            SAPbobsCOM.BoFieldTypes tipo, int tamano, SAPbobsCOM.BoFldSubTypes subtipo)
        {
            if (Contar(oCompany, "SELECT COUNT(*) AS \"N\" FROM \"CUFD\" " +
                                 "WHERE \"TableID\" = '@MPA_LOG' AND \"AliasID\" = '" + alias + "'") > 0)
                return;

            SAPbobsCOM.UserFieldsMD campo = (SAPbobsCOM.UserFieldsMD)oCompany
                .GetBusinessObject(SAPbobsCOM.BoObjectTypes.oUserFields);
            try
            {
                campo.TableName = "@MPA_LOG";
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
            string moneda, string fecha, double tasa)
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