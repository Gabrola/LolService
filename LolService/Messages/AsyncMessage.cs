using FluorineFx.AMF3;
using LolService.Util;

namespace LolService.Messages
{
    public class AsyncMessage : AbstractMessage
    {
        const byte CORRELATION_ID_FLAG = 1;
        const byte CORRELATION_ID_BYTES_FLAG = 2;

        public string CorrelationId { get; set; }
        public ByteArray CorrelationIdBytes { get; set; }


        public override void ReadExternal(IDataInput input)
        {
            base.ReadExternal(input);
            var flags = ReadFlags(input);
            for (int i = 0; i < flags.Count; i++)
            {
                int bits = 0;
                if (i == 0)
                {
                    if ((flags[i] & CORRELATION_ID_FLAG) != 0)
                    {
                        CorrelationId = input.ReadObject() as string;
                    }
                    if ((flags[i] & CORRELATION_ID_BYTES_FLAG) != 0)
                    {
                        CorrelationId = RtmpUtil.FromByteArray(input.ReadObject() as ByteArray);
                    }
                    bits = 2;
                }
                ReadRemaining(input, flags[i], bits);
            }
        }

        public override void WriteExternal(IDataOutput output)
        {
            base.WriteExternal(output);

            if (CorrelationIdBytes == null)
                CorrelationIdBytes = RtmpUtil.ToByteArray(CorrelationId);

            int flag = 0;
            if (CorrelationId != null && CorrelationIdBytes == null)
                flag |= CORRELATION_ID_FLAG;
            if (CorrelationIdBytes != null)
                flag |= CORRELATION_ID_BYTES_FLAG;

            output.WriteByte((byte)flag);

            if (CorrelationId != null && CorrelationIdBytes == null)
                output.WriteObject(CorrelationId);
            if (CorrelationIdBytes != null)
                output.WriteObject(CorrelationIdBytes);
        }
    }
}
