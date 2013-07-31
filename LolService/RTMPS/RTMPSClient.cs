using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluorineFx;
using FluorineFx.Messaging.Rtmp;
using FluorineFx.Messaging.Api.Event;
using FluorineFx.Messaging.Rtmp.Event;
using FluorineFx.Messaging.Rtmp.Service;
using FluorineFx.Messaging.Messages;
using FluorineFx.Util;
using LolService.Util;

namespace LolService.RTMPS
{
    public class RTMPSClient : SSLClient
    {
        protected RtmpContext sourcecontext = new RtmpContext(RtmpMode.Server) { ObjectEncoding = ObjectEncoding.AMF3 };
        protected RtmpContext remotecontext = new RtmpContext(RtmpMode.Client) { State = RtmpState.Connected };

        protected AtomicInteger CurrentInvoke = new AtomicInteger();

        protected List<CallResultWait> WaitInvokeList = new List<CallResultWait>();
        protected readonly object WaitLock = new object();

        public RTMPSClient() : base()
        {
        }

        protected void Start(string Server, int Port)
        {
            Task connectTask =  base.Connect(Server, Port);
            connectTask.Wait();
            this.RTMPHandshake();
        }

        protected void RTMPHandshake()
        {
            Random rand = new Random();

            byte C0 = 0x03;
            SSLStream.WriteByte(C0);

            int TimestampC1 = TimeUtil.EpochTime();
            byte[] RandC1 = new byte[1528];
            rand.NextBytes(RandC1);

            SSLStream.Write(BitConverter.GetBytes(TimestampC1));
            SSLStream.Write(BitConverter.GetBytes((int)0));
            SSLStream.Write(RandC1);

            SSLStream.Flush();

            byte S0 = (byte)SSLStream.ReadByte();
            if (S0 != 0x03)
                throw new IOException("Server returned incorrect version in handshake: " + S0);

            byte[] S1 = new byte[1536];
            for (int i = 0; i < 1536; i++)
            {
                S1[i] = (byte)SSLStream.ReadByte();
            }

            int TimestampS1 = TimeUtil.EpochTime();
            SSLStream.Write(S1, 0, 4);
            SSLStream.Write(BitConverter.GetBytes(TimestampS1));
            SSLStream.Write(S1, 8, 1528);

            SSLStream.Flush();

            byte[] S2 = new byte[1536];
            for (int i = 0; i < 1536; i++)
            {
                S2[i] = (byte)SSLStream.ReadByte();
            }

            bool Valid = true;
            for (int i = 8; i < 1536; i++)
            {
                if (RandC1[i - 8] != S2[i])
                {
                    Valid = false;
                    break;
                }
            }

            if (!Valid)
                throw new IOException("Server returned invalid handshake");

            base.StartReading();
        }

        public Notify InvokeRemotingMessage(string service, string operation, params object[] args)
        {
            var msg = new RemotingMessage();
            msg.operation = operation;
            msg.destination = service;
            msg.headers["DSRequestTimeout"] = 60;
            msg.headers["DSId"] = RtmpUtil.RandomUidString();
            msg.headers["DSEndpoint"] = "my-rtmps";
            msg.body = args;
            msg.messageId = RtmpUtil.RandomUidString();

            return Invoke(msg);
        }

        protected Notify InvokeCommandMessage(string service, int operation, object body, string DSId)
        {
            var msg = new CommandMessage();
            msg.operation = operation;
            msg.correlationId = "";
            msg.timestamp = 0;
            msg.clientId = null;
            msg.timeToLive = 0;
            msg.messageId = RtmpUtil.RandomUidString();
            msg.destination = service;
            msg.body = body;
            msg.SetHeader("DSId", DSId);
            msg.SetHeader("DSEndpoint", "my-rtmps");

            return Invoke(msg);
        }

        protected Notify InvokeSubscribeMessage(string DSSubtopic, string ClientID)
        {
            var msg = new CommandMessage();
            msg.body = new ASObject();
            msg.destination = "messagingDestination";
            msg.operation = CommandMessage.SubscribeOperation;
            msg.headers["DSSubtopic"] = DSSubtopic;
            msg.clientId = ClientID;

            return Invoke(msg);
        }

        public Notify Invoke(object msg)
        {
            var inv = new FlexInvoke();
            inv.EventType = (EventType)2;
            inv.ServiceCall = new PendingCall(null, new[] { msg });
            return Call(inv);
        }

        /// <summary>
        /// Call blocks until the result is received. Use Send for a nonblocking call.
        /// </summary>
        /// <param name="notify">Call</param>
        /// <returns>Result or null if failed</returns>
        protected Notify Call(Notify notify)
        {
            var callresult = new CallResultWait(notify, true);
            lock (WaitLock)
            {
                WaitInvokeList.Add(callresult);
            }
            notify.InvokeId = CurrentInvoke.Increment();

            SendPacket(notify);

            callresult.Wait.WaitOne(-1);
            return callresult.Result;
        }

        /// <summary>
        /// Send does not block and returns immediately. Use Call for a blocking call
        /// </summary>
        /// <param name="notify">Call</param>
        protected void Send(Notify notify)
        {
            //Might as well use the waitlist so InternalReceive doesn't freak out about the invoke id not being found.
            lock (WaitLock)
            {
                WaitInvokeList.Add(new CallResultWait(notify, false));
            }

            notify.InvokeId = CurrentInvoke.Increment();

            SendPacket(notify);
        }

        protected void SendPacket(Notify notify)
        {
            var buf = RtmpProtocolEncoder.Encode(sourcecontext, CreatePacket(notify));
            if (buf == null)
            {
                //StaticLogger.Fatal("Unable to encode " + notify);
            }
            else
            {
                var buff = buf.ToArray();
                base.Send(buff, 0, buff.Length);
            }
        }

        protected RtmpPacket CreatePacket(Notify notify)
        {
            var header = new RtmpHeader
            {
                ChannelId = 3,
                DataType = notify.DataType,
                Timer = notify.Timestamp
            };
            return new RtmpPacket(header, notify);
        }

        protected override void OnReceive(byte[] buffer, int idx, int len)
        {
            byte[] ReceiveBuffer = new byte[len];
            Array.Copy(buffer, idx, ReceiveBuffer, 0, len);

            var objs = RtmpProtocolDecoder.DecodeBuffer(remotecontext, new ByteBuffer(new MemoryStream(ReceiveBuffer)));
            if (objs != null)
            {
                foreach (var obj in objs)
                {
                    RtmpPacket packet = obj as RtmpPacket;
                    if (packet != null)
                    {
                        var result = packet.Message as Notify;
                        if (result != null)
                        {
                            CallResultWait callresult = null;
                            if (RtmpUtil.IsResult(result))
                            {
                                lock (WaitLock)
                                {
                                    int fidx = WaitInvokeList.FindIndex(crw => crw.Call.InvokeId == result.InvokeId);
                                    if (fidx != -1)
                                    {
                                        callresult = WaitInvokeList[fidx];
                                        WaitInvokeList.RemoveAt(fidx);
                                    }
                                }

                                if (callresult != null)
                                {
                                    callresult.Result = result;
                                    callresult.Wait.Set();

                                    if (!callresult.Blocking)
                                        OnCall(callresult.Call, callresult.Result);
                                }
                                else
                                {
                                    OnNotify(result);
                                }
                            }
                            else
                            {
                                OnNotify(result);
                            }
                        }
                    }
                }
            }
        }

        protected virtual void OnCall(Notify call, Notify result)
        {
        }

        protected virtual void OnNotify(Notify notify)
        {
        }
    }
}
