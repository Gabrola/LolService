using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Threading;
using System.Linq;
using FluorineFx;
using FluorineFx.Messaging.Messages;
using FluorineFx.Messaging.Api.Event;
using FluorineFx.Messaging.Rtmp.Service;
using FluorineFx.Messaging.Rtmp.Event;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using LolService.Util;

namespace LolService.RTMPS
{
    public class LolRTMPSClient : RTMPSClient
    {
        private string ClientVersion;
        private string AuthToken;
        public int AccountID;
        private string DSId = "";
        private string Server;
        private string Region;
        private string Username;
        private string Password;
        private string IPAddress;
        public string SessionToken;
        public bool LoggedIn { get; private set; }

        private LCDSHeartbeat Heartbeat;

        public LolRTMPSClient(string _Region, string User, string Pass, string Version) : base()
        {
            string LoginQueue;
            if (_Region == "EUW")
            {
                Server = "prod.eu.lol.riotgames.com";
                LoginQueue = "https://lq.eu.lol.riotgames.com/";
            }
            else if (_Region == "EUNE")
            {
                Server = "prod.eun1.lol.riotgames.com";
                LoginQueue = "https://lq.eun1.lol.riotgames.com/";
            }
            else if (_Region == "NA")
            {
                Server = "prod.na1.lol.riotgames.com";
                LoginQueue = "https://lq.na1.lol.riotgames.com/";
            }
            else if (_Region == "PBE")
            {
                Server = "prod.pbe1.lol.riotgames.com";
                LoginQueue = "https://lq.pbe1.lol.riotgames.com/";
            }
            else
            {
                return;
            }

            this.Username = User;
            this.Password = Pass;
            this.Region = _Region;
            this.ClientVersion = Version;

            this.IPAddress = this.GetIP();
            this.AuthToken = this.GetAuthCode(User, Pass, LoginQueue);
            base.Start(Server, 2099);
            this.PingOperation();
            this.SendLoginInfo();
            this.LoginOperation();
        }

        private void PingOperation()
        {
            var Message = RtmpUtil.MakeCommandMessage("", CommandMessage.ClientPingOperation, DSId, new ASObject() { TypeName = null });
            var inv = new Invoke();
            inv.ServiceCall = new PendingCall(null, "connect", new object[] { false, "nil", "", Message });

            inv.ConnectionParameters = new Dictionary<string, object>();
            inv.ConnectionParameters.Add("app", "");
            inv.ConnectionParameters.Add("flashVer", "WIN 10,1,85,3");
            inv.ConnectionParameters.Add("swfUrl", "app:/mod_ser.dat");
            inv.ConnectionParameters.Add("tcUrl", "rtmps://" + Server + ":2099");
            inv.ConnectionParameters.Add("fpad", false);
            inv.ConnectionParameters.Add("capabilities", 239);
            inv.ConnectionParameters.Add("audioCodecs", 3191);
            inv.ConnectionParameters.Add("videoCodecs", 252);
            inv.ConnectionParameters.Add("videoFunction", 1);
            inv.ConnectionParameters.Add("pageUrl", null);
            inv.ConnectionParameters.Add("objectEncoding", 3);
            inv.EventType = (EventType)2;

            Notify reply = base.Call(inv);
            ASObject args = reply.ServiceCall.Arguments[0] as ASObject;
            DSId = args["id"].ToString();
            Form1.Log("DSId = " + DSId);
        }

        private void SendLoginInfo()
        {
            ASObject body = new ASObject();
            body.Add("username", Username.ToLower());
            body.Add("password", Password);
            body.Add("authToken", AuthToken);
            body.Add("clientVersion", ClientVersion);
            body.Add("ipAddress", IPAddress);
            body.Add("locale", "en_US");
            body.Add("domain", "lolclient.lol.riotgames.com");
            body.Add("operatingSystem", "LolService");
            body.Add("securityAnswer", null);
            body.Add("oldPassword", null);
            body.Add("partnerCredentials", null);
            body.TypeName = "com.riotgames.platform.login.AuthenticationCredentials";

            Notify result = base.InvokeRemotingMessage("loginService", "login", new object[] { body });
            if (RtmpUtil.IsError(result))
            {
                ErrorMessage error = RtmpUtil.GetError(result);
                Form1.Log("Error = " + error.faultString);
                return;
            }

            ASObject args = (ASObject)RtmpUtil.GetBodies(result).FirstOrDefault().Item1;
            ASObject AccountSummary = (ASObject)args["accountSummary"];
            this.SessionToken = (string)args["token"];
            this.AccountID = Convert.ToInt32(AccountSummary["accountId"]);
            Form1.Log("SessionToken = " + SessionToken);
            Form1.Log("Account ID: " + AccountID);
        }

        private void LoginOperation()
        {
            byte[] LoginBody = System.Text.Encoding.UTF8.GetBytes(Username.ToLower() + ":" + SessionToken);
            var Invoke = base.InvokeCommandMessage("auth", CommandMessage.LoginOperation, Convert.ToBase64String(LoginBody), DSId);
            var Body = RtmpUtil.GetBodies(Invoke).FirstOrDefault();
            if (Body == null || !(Body.Item1 is string))
            {
                Form1.Log("Invalid login");
                return;
            }
            base.InvokeSubscribeMessage("bc", "bc-" + AccountID);
            base.InvokeSubscribeMessage("cn-" + AccountID, "cn-" + AccountID);
            base.InvokeSubscribeMessage("gn-" + AccountID, "gn-" + AccountID);
            LoggedIn = true;
            Heartbeat = new LCDSHeartbeat(this);
        }

        private string GetIP()
        {
            JToken IPRequest = JsonDownloader.Get<JToken>("http://ll.leagueoflegends.com/services/connection_info");
            if (IPRequest == null || IPRequest["ip_address"] == null)
                return "127.0.0.1";

            Form1.Log("IP Address = " + (string)IPRequest["ip_address"]);
            return (string)IPRequest["ip_address"];
        }

        private string GetAuthCode(string Username, string Password, string LoginQueue)
        {
            NameValueCollection query = new NameValueCollection();
            query.Add("payload", string.Format("user={0},password={1}", Username, Password));
            JToken login = JsonDownloader.Post<JToken>(LoginQueue + "login-queue/rest/queue/authenticate", query);

            string Status = (string)login["status"];

            if(Status == "FAILED")
                throw new Exception("Error logging in: " + (string)login["reason"]);

            if (Status == "LOGIN")
            {
                Form1.Log("Auth Token = " + login["token"]);
                return (string)login["token"];
            }

            int node = (int)login["node"];
            string champ = (string)login["champ"];
            int rate = (int)login["rate"];
            int delay = (int)login["delay"];

            int id = 0;
            int cur = 0;

            JArray tickers = (JArray)login["tickers"];
            foreach(JToken token in tickers)
            {
                int tnode = (int)token["node"];
                if (tnode != node)
                    continue;

                id = (int)token["id"];
                cur = (int)token["current"];
                break;
            }

            Form1.Log("In login queue for " + Region + ", #" + (id - cur) + " in line");

            while (id - cur > rate) {
                Thread.Sleep(delay);
                JToken result = JsonDownloader.Get<JToken>(LoginQueue + "login-queue/rest/queue/ticker/" + champ);
                if (result == null)
                    continue;

                cur = Convert.ToInt32((string)result[node.ToString()], 16);
                Form1.Log("In login queue for " + Region + ", #" + (int)Math.Max(1, id - cur) + " in line");
            }

            JToken login2 = JsonDownloader.Get<JToken>(LoginQueue + "login-queue/rest/queue/authToken/" + Username.ToLower());
            while (login2 == null || login2["token"] == null)
            {
                Thread.Sleep(delay / 10);
                login2 = JsonDownloader.Get<JToken>(LoginQueue + "login-queue/rest/queue/authToken/" + Username.ToLower());
            }

            Form1.Log("Auth Token = " + login2["token"]);
            return (string)login2["token"]; 
        }

        protected override void OnCall(Notify call, Notify result)
        {
            Form1.Log("OnCall");
        }

        protected override void OnNotify(Notify notfiy)
        {
            Form1.Log("OnNotify");
        }
    }
}
