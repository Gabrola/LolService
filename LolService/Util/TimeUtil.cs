using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LolService.Util
{
    public class TimeUtil
    {
        public static int EpochTime()
        {
            TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
            return (int)t.TotalSeconds;
        }
    }
}
