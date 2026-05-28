using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MarcusRunge.Toolbox.Network
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
        public static IPAddress GetIpv4SubnetMask(IPAddress ipAddress)
        {
            ArgumentNullException.ThrowIfNull(ipAddress);

            if (ipAddress.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ArgumentException("Address must be an IPv4 address.", nameof(ipAddress));
            }

            UnicastIPAddressInformation? addressInformation = FindUnicastAddress(ipAddress);

            if (addressInformation?.IPv4Mask is IPAddress mask)
            {
                return mask;
            }

            throw new ArgumentException($"Can't find subnet mask for IPv4 address '{ipAddress}'.", nameof(ipAddress));
        }

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

            UnicastIPAddressInformation? addressInformation = FindUnicastAddress(ipAddress);

            if (addressInformation is not null)
            {
                return addressInformation.PrefixLength;
            }

            throw new ArgumentException($"Can't find prefix length for IPv6 address '{ipAddress}'.", nameof(ipAddress));
        }

        private static UnicastIPAddressInformation? FindUnicastAddress(IPAddress ipAddress)
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