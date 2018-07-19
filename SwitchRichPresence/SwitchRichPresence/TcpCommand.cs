﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SwitchRichPresence
{
    [Serializable]
    public class TcpCommandException : Exception
    {
        public TcpCommandException() { }
        public TcpCommandException(string message) : base(message) { }
        public TcpCommandException(string message, Exception inner) : base(message, inner) { }
        protected TcpCommandException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }

    public class TcpCommand
    {
        private const uint SRV_MAGIC = 0x11223300;
        private const uint CLT_MAGIC = 0x33221100;

        public enum SendCommandType : byte
        {
            SendBuffer      = 0,
            Confirm         = 1,
            GetControlData  = 2,
            ListApps        = 3,
            GetActiveUser   = 4,
            GetCurrentApp   = 5,
            GetVersion      = 6,
            Disconnect      = 0xFF,
        }

        //send the command type only
        public static void SendCommand(Socket client, SendCommandType type)
        {
            byte[] buffer = BitConverter.GetBytes(CLT_MAGIC | (byte)type);
            int total = 0;
            while (total < buffer.Length)
            {
                int count = client.Send(buffer, total, buffer.Length - total, SocketFlags.None);
                if (count <= 0)
                    throw new Exception("Error while receiving data !");
                total += count;
            }
        }
        public static void ReceiveConfirm(Socket client)
        {
            byte[] buffer = new byte[4];

            int total = 0;
            while (total < buffer.Length)
            {
                int count = client.Receive(buffer, total, buffer.Length - total, SocketFlags.None);
                if (count <= 0)
                    throw new Exception("Error while receiving data !");

                total += count;
            }

            uint magic = BitConverter.ToUInt32(buffer, 0);

            if ((magic & 0xFFFFFF00) != SRV_MAGIC)
                throw new TcpCommandException(string.Format("Invalid Response magic : 0x{0} instead of 0x{1}", (magic & 0x00FFFFFF).ToString("X"), SRV_MAGIC.ToString("X")));
        }

        //validate command and return the buffer
        public static byte[] ReceiveBuffer(Socket client, int size)
        {
            byte[] buffer = new byte[4 + size];//magic + buffer
            byte[] data;

            int total = 0;
            while (total < buffer.Length)
            {
                int count = client.Receive(buffer, total, buffer.Length - total, SocketFlags.None);
                if (count <= 0)
                    throw new Exception("Error while receiving data !");
                total += count;
            }

            //validate command
            using (MemoryStream ms = new MemoryStream(buffer))
            {
                BinaryReader br = new BinaryReader(ms);
                uint magic = br.ReadUInt32();
                data = br.ReadBytes(size);

                //throws exceptions if needed
                if ((magic & 0xFFFFFF00) != SRV_MAGIC)
                    throw new TcpCommandException(string.Format("Invalid Response magic : 0x{0} instead of 0x{1}", (magic & 0x00FFFFFF).ToString("X"), SRV_MAGIC.ToString("X")));
            }

            buffer = null;

            GC.Collect();
            return data;
        }
        //send data only
        public static void SendBuffer(Socket client, byte[] data)
        {
            // send the buffer
            byte[] cmdBuff = new byte[data.Length + 4];
            using (MemoryStream ms = new MemoryStream(cmdBuff))
            {
                BinaryWriter bw = new BinaryWriter(ms);
                bw.Write(CLT_MAGIC | (byte)SendCommandType.SendBuffer);
                bw.Write(data);


                byte[] buffer = ms.ToArray();
                int total = 0;
                while (total < buffer.Length)
                {
                    int count = client.Send(buffer, total, buffer.Length - total, SocketFlags.None);
                    if (count <= 0)
                        throw new Exception("Error while receiving data !");
                    total += count;
                }
            }
            GC.Collect();
        }

        //wrappers
        public static void SendBuffer(Socket client, ulong nb)
        {
            SendBuffer(client, BitConverter.GetBytes(nb));
        }
        public static int ReceiveInt32(Socket client)
        {
            return BitConverter.ToInt32(ReceiveBuffer(client, 4), 0);
        }
        public static ulong ReceiveUInt64(Socket client)
        {
            return BitConverter.ToUInt64(ReceiveBuffer(client, 8), 0);
        }
        public static bool ReceiveBool(Socket client)
        {
            byte[] buff = ReceiveBuffer(client, 1);
            return BitConverter.ToBoolean(buff, 0);
        }
    }
}
