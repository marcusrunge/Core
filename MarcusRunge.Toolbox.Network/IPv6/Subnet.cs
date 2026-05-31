using MarcusRunge.Toolbox.Network.Helper;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MarcusRunge.Toolbox.Network.IPv6
{
    /// <summary>
    /// Contains methods for working with subnets, such as retrieving the subnet mask for IPv4 addresses and the prefix length for IPv6 addresses.
    /// </summary>
    public static class Subnet
    {
        /// <summary>
        /// Gets the IPv6 prefix length from an IPv6 address.
        /// </summary>
        /// <param name="ipAddress">The IPv6 address.</param>
        /// <returns>The prefix length.</returns>
        public static int GetIpv6PrefixLength(IPAddress ipAddress)
        {
            ArgumentNullException.ThrowIfNull(ipAddress);

            if (ipAddress.AddressFamily != AddressFamily.InterNetworkV6)
            {
                throw new ArgumentException("Address must be an IPv6 address.", nameof(ipAddress));
            }

            UnicastIPAddressInformation? addressInformation = UnicastHelper.FindUnicastAddress(ipAddress);

            return addressInformation is not null
                ? addressInformation.PrefixLength
                : throw new ArgumentException($"Can't find prefix length for IPv6 address '{ipAddress}'.", nameof(ipAddress));
        }
    }
}