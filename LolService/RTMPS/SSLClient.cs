using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.Threading;


namespace LolService.RTMPS
{
    public class SSLClient
    {
        public const int BufferSize = 65535;

        protected bool _disposed = false;

        public TcpClient TCPClient { get; protected set; }
        public SslStream SSLStream { get; protected set; }

        byte[] ReceiveBuffer { get; set; }

        protected ProcessQueue<Tuple<byte[], int, int>> SendQueue = new ProcessQueue<Tuple<byte[], int, int>>();
        protected CancellationTokenSource CancellationSource;

        public SSLClient()
        {
            CancellationSource = new CancellationTokenSource();
            ReceiveBuffer = new byte[BufferSize];
            TCPClient = new TcpClient();
            SendQueue.Process += SendQueue_Process;
        }

        public async Task Connect(string Server, int Port)
        {
            try
            {
                TCPClient.Connect(Server, Port);
                SSLStream = new SslStream(TCPClient.GetStream(), false,
                    (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) =>
                    {
                        return true;
                    }) { ReadTimeout = 50000, WriteTimeout = 50000 };
                await SSLStream.AuthenticateAsClientAsync(Server);
            }
            catch
            {
            }
        }

        protected async void StartReading()
        {
            try
            {
                await this.StartRead(CancellationSource.Token);
            }
            catch
            {
            }
        }

        public void Close()
        {
            CancellationSource.Cancel();
            try
            {
                TCPClient.Close();
            }
            catch { }
        }

        async Task StartRead(CancellationToken ct)
        {
            int read = -1;
            while (read != 0 && !ct.IsCancellationRequested)
            {
                read = await SSLStream.ReadAsync(ReceiveBuffer, 0, BufferSize);
                OnReceive(ReceiveBuffer, 0, read);
            }

            throw new EndOfStreamException(string.Format("Socket closed"));
        }

        protected virtual void OnReceive(byte[] buffer, int idx, int len) { }

        protected void Send(byte[] buffer, int idx, int len)
        {
            byte[] Packet = new byte[len];
            Array.Copy(buffer, idx, Packet, 0, len);
            this.SendQueue.Enqueue(new Tuple<byte[], int, int>(Packet, 0, len));
        }

        void SendQueue_Process(object sender, ProcessQueueEventArgs<Tuple<byte[], int, int>> e)
        {
            try
            {
                var ar = SSLStream.BeginWrite(e.Item.Item1, e.Item.Item2, e.Item.Item3, null, null);
                using (ar.AsyncWaitHandle)
                {
                    if (ar.AsyncWaitHandle.WaitOne(-1))
                    {
                        SSLStream.EndWrite(ar);
                    }
                }
            }
            catch { }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                Close();
                SendQueue.Dispose();
            }
        }

        ~SSLClient()
        {
            Dispose(false);
        }
    }
}
