using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using cdn_api;
using Serilog;
using Serilog.Core;

namespace PinquarkWMSSynchro.Infrastructure
{
    public class XlApiService
    {
        [DllImport("ClaRUN.dll")]
        public static extern void AttachThreadToClarion(int _flag);
        private int _sessionId;
        public int Login()
        {
            AttachThreadToClarion(1);
            XLLoginInfo_20241 xLLoginInfo = new XLLoginInfo_20241()
            {
                Wersja = Convert.ToInt32(ConfigurationManager.AppSettings["XlApiVersion"]),
                ProgramID = ConfigurationManager.AppSettings["XlApiProgramName"],
                Baza = ConfigurationManager.AppSettings["XlApiDatabase"],
                OpeIdent = ConfigurationManager.AppSettings["XlApiUsername"],
                OpeHaslo = ConfigurationManager.AppSettings["XlApiPassword"],
                TrybWsadowy = 1,
                TrybNaprawy = 0,
                Winieta = -1
            };

            int result = cdn_api.cdn_api.XLLogin(xLLoginInfo, ref _sessionId);
            return result;
        }

        public int Logout()
        {
            AttachThreadToClarion(1);
            XLLogoutInfo_20241 xLLogoutInfo = new XLLogoutInfo_20241()
            {
                Wersja = Convert.ToInt32(ConfigurationManager.AppSettings["XlApiVersion"]),
            };

            int result = cdn_api.cdn_api.XLLogout(_sessionId);
            return result;
        }

        public int AddAttribute(int obiNumer, int obiType, string className, string value)
        {
            AttachThreadToClarion(1);
            XLAtrybutInfo_20241 xLAtrybut = new XLAtrybutInfo_20241()
            {
                Wersja = Convert.ToInt32(ConfigurationManager.AppSettings["XlApiVersion"]),
                Klasa = className,
                Wartosc = value,
                GIDNumer = obiNumer,
                GIDTyp = obiType,
                GIDLp = 0,
                GIDSubLp = 0,
                GIDFirma = 449892,
            };

            int result = cdn_api.cdn_api.XLDodajAtrybut(_sessionId, xLAtrybut);

            return result;
        }
    }
}
