using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MarcusRunge.Toolbox.Network.Test
{
    /// <summary>
    /// Unit tests for <see cref="IPv4.Subnet"/> and <see cref="IPv6.Subnet"/>.
    /// </summary>
    public class SubnetTest
    {
        /// <summary>
        /// Gets the ipv4 subnet mask when ip address is null throws argument null exception.
        /// </summary>
        [Fact]
        public void GetIpv4SubnetMask_WhenIpAddressIsNull_ThrowsArgumentNullException()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => IPv4.Subnet.GetSubnetMask(null!));

            Assert.Equal("ipAddress", exception.ParamName);
        }

        /// <summary>
        /// Gets the ipv4 subnet mask when ip address is ipv6 throws argument exception.
        /// </summary>
        [Fact]
        public void GetIpv4SubnetMask_WhenIpAddressIsIpv6_ThrowsArgumentException()
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() => IPv4.Subnet.GetSubnetMask(IPAddress.IPv6Loopback));

            Assert.Equal("ipAddress", exception.ParamName);
            Assert.Contains("IPv4", exception.Message);
        }

        /// <summary>
        /// Gets the ipv4 subnet mask when address is not assigned to local interface throws argument exception.
        /// </summary>
        [Fact]
        public void GetIpv4SubnetMask_WhenAddressIsNotAssignedToLocalInterface_ThrowsArgumentException()
        {
            IPAddress ipAddress = IPAddress.Parse("203.0.113.1");

            ArgumentException exception = Assert.Throws<ArgumentException>(() => IPv4.Subnet.GetSubnetMask(ipAddress));

            Assert.Equal("ipAddress", exception.ParamName);
            Assert.Contains(ipAddress.ToString(), exception.Message);
        }

        /// <summary>
        /// Gets the ipv4 subnet mask when address exists on local interface returns interface mask.
        /// </summary>
        [Fact]
        public void GetIpv4SubnetMask_WhenAddressExistsOnLocalInterface_ReturnsInterfaceMask()
        {
            UnicastIPAddressInformation? addressInformation = GetFirstUnicastAddress(AddressFamily.InterNetwork);
            Assert.NotNull(addressInformation);

            IPAddress subnetMask = IPv4.Subnet.GetSubnetMask(addressInformation.Address);

            Assert.Equal(addressInformation.IPv4Mask, subnetMask);
        }

        /// <summary>
        /// Gets the ipv6 prefix length when ip address is null throws argument null exception.
        /// </summary>
        [Fact]
        public void GetIpv6PrefixLength_WhenIpAddressIsNull_ThrowsArgumentNullException()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => IPv6.Subnet.GetIpv6PrefixLength(null!));

            Assert.Equal("ipAddress", exception.ParamName);
        }

        /// <summary>
        /// Gets the ipv6 prefix length when ip address is ipv4 throws argument exception.
        /// </summary>
        [Fact]
        public void GetIpv6PrefixLength_WhenIpAddressIsIpv4_ThrowsArgumentException()
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() => IPv6.Subnet.GetIpv6PrefixLength(IPAddress.Loopback));

            Assert.Equal("ipAddress", exception.ParamName);
            Assert.Contains("IPv6", exception.Message);
        }

        /// <summary>
        /// Gets the ipv6 prefix length when address is not assigned to local interface throws argument exception.
        /// </summary>
        [Fact]
        public void GetIpv6PrefixLength_WhenAddressIsNotAssignedToLocalInterface_ThrowsArgumentException()
        {
            IPAddress ipAddress = IPAddress.Parse("2001:db8::1");

            ArgumentException exception = Assert.Throws<ArgumentException>(() => IPv6.Subnet.GetIpv6PrefixLength(ipAddress));

            Assert.Equal("ipAddress", exception.ParamName);
            Assert.Contains(ipAddress.ToString(), exception.Message);
        }

        /// <summary>
        /// Gets the length of the ipv6 prefix length when address exists on local interface returns interface prefix.
        /// </summary>
        [Fact]
        public void GetIpv6PrefixLength_WhenAddressExistsOnLocalInterface_ReturnsInterfacePrefixLength()
        {
            UnicastIPAddressInformation? addressInformation = GetFirstUnicastAddress(AddressFamily.InterNetworkV6);
            Assert.NotNull(addressInformation);

            int prefixLength = IPv6.Subnet.GetIpv6PrefixLength(addressInformation.Address);

            Assert.Equal(addressInformation.PrefixLength, prefixLength);
        }

        private static UnicastIPAddressInformation? GetFirstUnicastAddress(AddressFamily addressFamily)
        {
            return NetworkInterface
                .GetAllNetworkInterfaces()
                .SelectMany(networkInterface => networkInterface.GetIPProperties().UnicastAddresses)
                .FirstOrDefault(address => address.Address.AddressFamily == addressFamily);
        }
    }
}