using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LolService.Util
{
    public class JsonDownloader
    {
        public static T Get<T>(string URL, NameValueCollection query = null) where T : class
        {
            try
            {
                WebRequestUtil request;
                if (query == null)
                    request = new WebRequestUtil(URL, "GET");
                else
                    request = new WebRequestUtil(URL + "?" + query.ToQueryString(), "GET");

                string json = request.GetResponse();
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch
            {
                return null;
            }
        }

        public static T Post<T>(string URL, NameValueCollection query) where T : class
        {
            try
            {
                WebRequestUtil request = new WebRequestUtil(URL, "POST", query);
                string json = request.GetResponse();
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch
            {
                return null;
            }
        }
    }
}
