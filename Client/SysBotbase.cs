using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using SysBot.Base;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Threading.Tasks;
using System.Threading;

namespace SysDVR.Client
{

    public static class SwitchExtendedCommand
    {
        private static readonly Encoding Encoder = Encoding.ASCII;

        private static byte[] Encode(string command, bool crlf = true)
        {
            if (crlf)
                command += "\r\n";
            return Encoder.GetBytes(command);
        }
        public static byte[] PixelPeek(bool crlf = true)
            => Encode("pixelPeek", crlf);
        public static byte[] Touch(double x, double y, bool crlf = true)
            => Encode($"touch {Math.Clamp(x, 0, 1) * 1280} {Math.Clamp(y, 0, 1) * 720}", crlf);
    }
    public class SwitchSocketConnector(IWirelessConnectionConfig cfg) : SwitchSocketAsync(cfg)
    {
        public CancellationToken cancel;

        public void Send(byte[] buffer, bool wait = true) {
            var thread = new Thread(async () => await SendAsync(buffer, cancel));
            thread.Start();
            if (wait) thread.Join();
        }

        public byte[] ReadResponse()
        {
            var buffer = new List<byte>();

            while (Connection.Available > 0)
            {
                var currByte = new Byte[1];
                var byteCounter = Connection.Receive(currByte, currByte.Length, SocketFlags.None);

                if (byteCounter.Equals(1))
                {
                    if (currByte[0] == '\n')
                    {
                        break;
                    }
                    buffer.Add(currByte[0]);
                }
            }
            Flush();

            return buffer.ToArray();
        }

        public void Flush() {
            while (Connection.Available > 0)
            {
                var currByte = new Byte[1];
                Connection.Receive(currByte, currByte.Length, SocketFlags.None);
            }
        }

        public void WaitResponse()
        {
            while (Connection.Available == 0)
            {
                System.Threading.Thread.Sleep(10);
            }
        }

        private static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        public byte[] PixelPeek()
        {
            Send(SwitchExtendedCommand.PixelPeek());
            WaitResponse();
            var response = ReadResponse();
            var str = Encoding.UTF8.GetString(response);
            return StringToByteArray(str);
        }
    }

    public class SysBotbase : IDisposable
    {
        public SwitchSocketConnector? Socket { get; set; }
        public event Action<bool>? OnState;
        public readonly string StreamName = "SysBotbase";
		const int MaxConnectionAttempts = 5;
		const int ConnFailDelayMs = 2000;
        readonly Action<string> reportMessage;
        CancellationToken cancel;
        string host;
        public SysBotbase(string host, CancellationToken token, Action<string> reportMessage) {
            cancel = token;
            this.host = host;
            this.reportMessage = reportMessage;
        }

        public async Task Connect()
        {
            if (Socket == null) {
                var cfg = new SwitchConnectionConfig { IP = host, Port = 6000 };
                Socket = new SwitchSocketConnector(cfg);
                
                for (int i = 0; i < MaxConnectionAttempts && !cancel.IsCancellationRequested; i++)
                {
                    reportMessage($"[{StreamName}] {string.Format(Program.Strings.Connection.ConnectionInProgress, i, MaxConnectionAttempts)}");

                    try
                    {
                        Socket.Connect();
                        Socket.Flush();
                        Socket.Send(SwitchCommand.GetBotbaseVersion());
                        Socket.Flush();
                        OnState?.Invoke(true);
                        break;
                    }
                    catch
                    {
                        if (i == MaxConnectionAttempts) {
                            Dispose();
                            throw;
                        }

                        await Task.Delay(ConnFailDelayMs, cancel).ConfigureAwait(false);
                    }
                }
            }
        }

        public void Disconnect() {
            Dispose();
            Console.WriteLine("[SysBotbase] Disconnected!");
        }

        public bool Connected {
            get {
                return Socket?.Connected == true;
            }
        }

        public void Dispose() {
            if (Socket != null) {
                var _Socket = Socket;
                Socket = null;
                if (_Socket.Connected) {
                    _Socket.Flush();
                    _Socket.Disconnect();
                }
            }
            host = null;
            OnState?.Invoke(false);
        }

        public void Press(SwitchButton button)
        {
            Socket?.Send(SwitchCommand.Hold(button));
        }

        public void Release(SwitchButton button)
        {
            Socket?.Send(SwitchCommand.Release(button));
        }

        public Image<Rgba32> PixelPeek()
        {
            Image<Rgba32> returnImage;
            try
            {
                byte[] frame = Socket.PixelPeek();
                returnImage = Image.Load<Rgba32>(frame);
                return returnImage;
            }
            catch
            {
                return null;
            }
        }
    }
}
