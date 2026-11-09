using System.Globalization;
using System;
using SAPbouiCOM;

namespace MiprimerAddon
{
    public class Addon
    {
        public static SAPbouiCOM.Application SBO_Application;

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
            SBO_Application.SetStatusBarMessage("MiprimerAddon conectado", BoMessageTime.bmt_Short, false);
        }

        private void AddMenu()
        {
            // Si el menú ya existía (por una ejecución anterior), se elimina antes de crearlo
            if (SBO_Application.Menus.Exists("MPA_Main"))
                SBO_Application.Menus.RemoveEx("MPA_Main");

            // "43520" es el menú "Módulos"
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
        }

        private void OnMenuEvent(ref MenuEvent pVal, out bool BubbleEvent)
        {
            BubbleEvent = true;

            if (!pVal.BeforeAction && pVal.MenuUID == "MPA_Form")
                OpenForm();
        }

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

            // Fuentes de datos: una para la fecha y una por cada resultado
            form.DataSources.UserDataSources.Add("dsFecha", BoDataType.dt_DATE, 10);
            form.DataSources.UserDataSources.Add("dsUsd", BoDataType.dt_SHORT_TEXT, 50);
            form.DataSources.UserDataSources.Add("dsEur", BoDataType.dt_SHORT_TEXT, 50);
            form.DataSources.UserDataSources.Add("dsUf", BoDataType.dt_SHORT_TEXT, 50);

            // Fecha
            AddLabel(form, "lblFecha", "Fecha:", 20, 20);
            Item txtFecha = form.Items.Add("txtFecha", BoFormItemTypes.it_EDIT);
            txtFecha.Left = 90; txtFecha.Top = 20; txtFecha.Width = 120; txtFecha.Height = 16;
            ((EditText)txtFecha.Specific).DataBind.SetBound(true, "", "dsFecha");
            form.DataSources.UserDataSources.Item("dsFecha").ValueEx =
                DateTime.Today.ToString("yyyyMMdd");

            // Botón
            Item btn = form.Items.Add("btnOk", BoFormItemTypes.it_BUTTON);
            btn.Left = 20; btn.Top = 55; btn.Width = 100; btn.Height = 20;
            ((SAPbouiCOM.Button)btn.Specific).Caption = "Consultar";

            // Resultados (solo lectura)
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

        private void OnItemEvent(string FormUID, ref ItemEvent pVal, out bool BubbleEvent)
        {
            BubbleEvent = true;

            if (pVal.FormTypeEx == "MPA_Form"
                && pVal.ItemUID == "btnOk"
                && pVal.EventType == BoEventTypes.et_ITEM_PRESSED
                && !pVal.BeforeAction)
            {
                ConsultarTipoCambios(FormUID);
            }
        }
        private void ConsultarTipoCambios(string formUID)
        {
            SAPbobsCOM.Recordset rs = null;
            try
            {
                SAPbouiCOM.Form form = SBO_Application.Forms.Item(formUID);

                // Validar la fecha: debe ser una fecha real en formato yyyyMMdd
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