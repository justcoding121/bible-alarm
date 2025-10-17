using Loggly.Config;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Loggly.Transports.Syslog
{
    internal class SyslogUdpTransport : SyslogTransportBase
    {
        private readonly UdpClientEx _udpClient;
        public SyslogUdpTransport()
        {
            IPEndPoint ipLocalEndPoint = null;

            try
            {
                var ipHostInfo = Dns.GetHostEntry(string.Empty);
                var ipAddress = ipHostInfo.AddressList.First(ip => ip.AddressFamily
                                                                    == AddressFamily.InterNetwork);
                ipLocalEndPoint = new IPEndPoint(ipAddress, 0);
            }
            catch { }

            _udpClient = ipLocalEndPoint != null ? new UdpClientEx(ipLocalEndPoint)
                                    : new UdpClientEx();
        }

        public bool IsActive
        {
            get { return _udpClient.IsActive; }
        }

        public void Close()
        {
            if (_udpClient.IsActive)
            {
#if NET_STANDARD
                _udpClient.Dispose();
#else
                _udpClient.Close();
#endif
            }
        }


        protected override async Task Send(SyslogMessage syslogMessage)
        {
            try
            {
                var hostEntry = Dns.GetHostEntryAsync(LogglyConfig.Instance.Transport.EndpointHostname).Result;
                var logglyEndpointIp = hostEntry.AddressList[0];
                var bytes = syslogMessage.GetBytes();
                await _udpClient.SendAsync(
                    bytes,
                    bytes.Length,
                    new IPEndPoint(logglyEndpointIp, LogglyConfig.Instance.Transport.EndpointPort)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogglyException.Throw(ex, "Error when sending data using Udp client.");
            }
            finally
            {
                Close();
            }
        }
    }
}
