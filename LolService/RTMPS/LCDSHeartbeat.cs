using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;

namespace LolService.RTMPS
{
    public class LCDSHeartbeat
    {
        private static Thread CurrentThread;
        private int HeartbeatCounter;
        private LolRTMPSClient Client;

        public LCDSHeartbeat(LolRTMPSClient client)
        {
            this.HeartbeatCounter = 1;
            this.Client = client;
            CurrentThread = new Thread(new ThreadStart(StartHeartbeat)) { IsBackground = true };
            CurrentThread.Start();
        }

        private void StartHeartbeat()
        {
            try
            {
                while (CurrentThread.IsAlive)
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    this.Client.InvokeRemotingMessage("loginService", "performLCDSHeartBeat", new object[] {
                            this.Client.AccountID,
                            this.Client.SessionToken,
                            this.HeartbeatCounter++,
                            DateTime.Now.ToString("ddd MMM d yyyy HH:mm:ss 'GMT'zz") + "00"
                        });

                    while (CurrentThread.IsAlive && sw.ElapsedMilliseconds < 120000)
                        Thread.Sleep(100);
                }
            }
            catch
            {
            }
        }
    }
}
