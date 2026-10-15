using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Draftline.Unity
{
    [Serializable] public sealed class LanAddressOption
    {
        public string Address,Adapter;
        public string Label => Adapter+" · "+Address;
    }
    public static class LanAddressUtility
    {
        public static bool TryReadEndpoint(string input,string portInput,out string address,out ushort port,out string error)
        {
            address=(input??"").Trim();port=0;error=null;
            if(address.Length==0){error="请输入房主的局域网 IP。";return false;}
            var parts=address.Split(':');
            if(parts.Length==2){address=parts[0].Trim();portInput=parts[1].Trim();}
            if(parts.Length>2||!IPAddress.TryParse(address,out var ip)||ip.AddressFamily!=AddressFamily.InterNetwork)
            {error="请输入有效的房主 IPv4 地址，例如 192.168.1.20。";return false;}
            if(!ushort.TryParse(portInput,out port)||port==0){error="请输入 1–65535 的端口。";return false;}
            address=ip.ToString();return true;
        }
        public static LanAddressOption[] Find()
        {
            try
            {
                return NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n=>n.OperationalStatus==OperationalStatus.Up&&n.NetworkInterfaceType!=NetworkInterfaceType.Loopback)
                    .OrderByDescending(n=>n.GetIPProperties().GatewayAddresses.Any(g=>g.Address.AddressFamily==AddressFamily.InterNetwork))
                    .ThenByDescending(n=>n.NetworkInterfaceType==NetworkInterfaceType.Wireless80211||n.NetworkInterfaceType==NetworkInterfaceType.Ethernet)
                    .SelectMany(n=>n.GetIPProperties().UnicastAddresses.Where(a=>IsUsable(a.Address)).Select(a=>new LanAddressOption{Address=a.Address.ToString(),Adapter=n.Name}))
                    .GroupBy(a=>a.Address).Select(g=>g.First()).ToArray();
            }
            catch{return Array.Empty<LanAddressOption>();}
        }
        public static bool IsUsable(IPAddress address)=>address.AddressFamily==AddressFamily.InterNetwork&&!IPAddress.IsLoopback(address)&&!address.Equals(IPAddress.Any)&&!address.Equals(IPAddress.None)&&!address.ToString().StartsWith("169.254.");
    }
}
