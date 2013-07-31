using System.Threading;
using FluorineFx.Messaging.Rtmp.Event;

namespace LolService.RTMPS
{
    public class CallResultWait
    {
        public Notify Call { get; set; }
        public Notify Result { get; set; }
        public AutoResetEvent Wait { get; set; }
        public bool Blocking { get; set; }
        public CallResultWait(Notify call, bool blocking)
        {
            Blocking = blocking;
            Call = call;
            Wait = new AutoResetEvent(false);
        }
    }
}
