using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

namespace LolService.Util
{
    public static class NameValueCollectionHelper
    {
        public static string ToQueryString(this NameValueCollection nvc)
        {
            return string.Join("&", Array.ConvertAll(nvc.AllKeys, key => string.Format("{0}={1}", WebUtility.UrlEncode(key), WebUtility.UrlEncode(nvc[key]))));
        }
    }
}
