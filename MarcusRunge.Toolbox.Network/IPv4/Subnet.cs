using MarcusRunge.Toolbox.Network.Helper;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MarcusRunge.Toolbox.Network.IPv4
{
    /// <summary>
    /// Contains methods for working with subnets, such as retrieving the subnet mask for IPv4 addresses and the prefix length for IPv6 addresses.
    /// </summary>
    public static class Subnet
    {
        /// <summary>
        /// Gets the IPv4 subnet mask from an IPv4 address.
        /// </summary>
        /// <param name="ipAddress">The IPv4 address.</param>
        /// <returns>The subnet mask as <see cref="IPAddress"/>.</returns>
        public static IPAddress GetSubnetMask(IPAddress ipAddress)
        {
            ArgumentNullException.ThrowIfNull(ipAddress);

            if (ipAddress.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ArgumentException("Address must be an IPv4 address.", nameof(ipAddress));
            }

            UnicastIPAddressInformation? addressInformation = UnicastHelper.FindUnicastAddress(ipAddress);

            return addressInformation?.IPv4Mask is IPAddress mask
                ? mask
                : throw new ArgumentException($"Can't find subnet mask for IPv4 address '{ipAddress}'.", nameof(ipAddress));
        }
    }
}