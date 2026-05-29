using System.Net;
using System.Net.NetworkInformation;

namespace MarcusRunge.Toolbox.Network.Helper
{
    internal static class UnicastHelper
    {
        internal static UnicastIPAddressInformation? FindUnicastAddress(IPAddress ipAddress)
        {
            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                IPInterfaceProperties properties = networkInterface.GetIPProperties();

                foreach (UnicastIPAddressInformation addressInformation in properties.UnicastAddresses)
                {
                    if (ipAddress.Equals(addressInformation.Address))
                    {
                        return addressInformation;
                    }
                }
            }

            return null;
        }
    }
}