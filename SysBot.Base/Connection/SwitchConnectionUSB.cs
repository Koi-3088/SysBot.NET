using LibUsbDotNet;
using LibUsbDotNet.Main;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SysBot.Base
{
    public class SwitchConnectionUSB : SwitchBotConfig
    {
        public SwitchBotConfig Config;
        private UsbDevice? SwDevice;
        private UsbEndpointReader? reader;
        private UsbEndpointWriter? writer;
        private const int MaximumTransferSize = 468;
        private readonly object _sync = new();

        public SwitchConnectionUSB(SwitchBotConfig cfg)
        {
            Config = cfg;
        }

        public void ConnectUSB()
        {
            lock (_sync)
            {
                foreach (UsbRegistry ur in UsbDevice.AllLibUsbDevices)
                {
                    ur.DeviceProperties.TryGetValue("Address", out object addr);
                    if (ur.Vid == 0x057E && ur.Pid == 0x3000 && Config.UsbPortIndex == addr.ToString())
                        SwDevice = ur.Device;
                }

                if (SwDevice == null)
                {
                    throw new Exception("USB device not found.");
                }

                if (SwDevice.IsOpen)
                    SwDevice.Close();
                SwDevice.Open();

                if (SwDevice is IUsbDevice wholeUsbDevice)
                {
                    wholeUsbDevice.SetConfiguration(1);
                    bool resagain = wholeUsbDevice.ClaimInterface(0);
                    if (!resagain)
                    {
                        wholeUsbDevice.ReleaseInterface(0);
                        wholeUsbDevice.ClaimInterface(0);
                    }
                }
                else
                {
                    DisconnectUSB();
                    throw new Exception("Device is using a WinUSB driver. Use libusbK and create a filter.");
                }

                reader = SwDevice.OpenEndpointReader(ReadEndpointID.Ep01);
                writer = SwDevice.OpenEndpointWriter(WriteEndpointID.Ep01);
            }
        }

        public void DisconnectUSB()
        {
            lock (_sync)
            {
                if (SwDevice != null)
                {
                    SendUSB(SwitchCommand.DetachController());
                    if (SwDevice.IsOpen)
                    {
                        if (SwDevice is IUsbDevice wholeUsbDevice)
                            wholeUsbDevice.ReleaseInterface(0);
                        SwDevice.Close();
                    }
                }

                reader?.Dispose();
                writer?.Dispose();
            }
        }

        public int SendUSB(byte[] buffer)
        {
            lock (_sync)
                return SendInternal(buffer);
        }

        public int ReadUSB(byte[] buffer)
        {
            lock (_sync)
                return ReadInternal(buffer);
        }

        public byte[] ReadBytesUSB(uint offset, int length)
        {
            if (length > MaximumTransferSize)
                return ReadBytesLarge(offset, length);
            lock (_sync)
            {
                var cmd = SwitchCommand.PeekUSB(offset, length);
                SendInternal(cmd);
                Thread.Sleep(1);

                var buffer = new byte[length];
                var _ = ReadInternal(buffer);
                return buffer;
            }
        }

        public void WriteBytesUSB(byte[] data, uint offset)
        {
            if (data.Length > MaximumTransferSize)
                WriteBytesLarge(data, offset);
            lock (_sync)
            {
                SendInternal(SwitchCommand.PokeUSB(offset, data));
                Thread.Sleep(1);
            }
        }

        private int ReadInternal(byte[] buffer)
        {
            byte[] sizeOfReturn = new byte[4];
            if (reader == null)
                throw new Exception("USB device not found or not connected.");

            reader.Read(sizeOfReturn, 5000, out _);
            reader.Read(buffer, 5000, out var lenVal);
            return lenVal;
        }

        private int SendInternal(byte[] buffer)
        {
            if (writer == null)
                throw new Exception("USB device not found or not connected.");

            uint pack = (uint)buffer.Length + 2;
            var ec = writer.Write(BitConverter.GetBytes(pack), 2000, out _);
            if (ec != ErrorCode.None)
            {
                DisconnectUSB();
                throw new Exception(UsbDevice.LastErrorString);
            }
            ec = writer.Write(buffer, 2000, out var l);
            if (ec != ErrorCode.None)
            {
                DisconnectUSB();
                throw new Exception(UsbDevice.LastErrorString);
            }
            return l;
        }

        private void WriteBytesLarge(byte[] data, uint offset)
        {
            int byteCount = data.Length;
            for (int i = 0; i < byteCount; i += MaximumTransferSize)
                WriteBytesUSB(SubArray(data, i, MaximumTransferSize), offset + (uint)i);
        }

        private byte[] ReadBytesLarge(uint offset, int length)
        {
            List<byte> read = new();
            for (int i = 0; i < length; i += MaximumTransferSize)
                read.AddRange(ReadBytesUSB(offset + (uint)i, Math.Min(MaximumTransferSize, length - i)));
            return read.ToArray();
        }

        private A[] SubArray<A>(A[] data, int index, int length)
        {
            if (index + length > data.Length)
                length = data.Length - index;
            A[] result = new A[length];
            Array.Copy(data, index, result, 0, length);
            return result;
        }
    }
}
