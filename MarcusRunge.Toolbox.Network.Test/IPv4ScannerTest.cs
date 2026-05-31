using MarcusRunge.Toolbox.Network.IPv4;
using System.Net;
using System.Net.Sockets;

namespace MarcusRunge.Toolbox.Network.Test
{
    /// <summary>
    /// Unit tests for <see cref="Scanner"/>.
    /// </summary>
    public class IPv4ScannerTest
    {
        /// <summary>
        /// Gets the host addresses and ensures only IPv4 addresses are returned.
        /// </summary>
        [Fact]
        public void GetHostAddresses_ReturnsOnlyIPv4Addresses()
        {
            // Act
            IPAddress[] addresses = Scanner.GetHostAddresses();

            // Assert
            Assert.NotNull(addresses);
            Assert.All(addresses, address => Assert.Equal(AddressFamily.InterNetwork, address.AddressFamily));
        }

        /// <summary>
        /// Gets the IP address range for a given IP address and subnet mask, and verifies that the calculated range matches the expected values.
        /// </summary>
        /// <param name="ipAddressText"></param>
        /// <param name="subnetMaskText"></param>
        /// <param name="expectedFromText"></param>
        /// <param name="expectedToText"></param>
        [Theory]
        [InlineData("10.1.2.3", "255.0.0.0", "10.0.0.0", "10.255.255.255")]
        [InlineData("172.16.2.3", "255.255.0.0", "172.16.0.0", "172.16.255.255")]
        [InlineData("192.168.1.42", "255.255.255.0", "192.168.1.0", "192.168.1.255")]
        public void GetIPAddressRange_WithExplicitSubnetMask_ReturnsExpectedRange(string ipAddressText, string subnetMaskText, string expectedFromText, string expectedToText)
        {
            // Arrange
            IPAddress ipAddress = IPAddress.Parse(ipAddressText);
            IPAddress subnetMask = IPAddress.Parse(subnetMaskText);

            byte[] expectedFrom = IPAddress.Parse(expectedFromText).GetAddressBytes();
            byte[] expectedTo = IPAddress.Parse(expectedToText).GetAddressBytes();

            // Act
            (byte[] from, byte[] to) = Scanner.GetIPAddressRange(ipAddress, subnetMask);

            // Assert
            Assert.Equal(expectedFrom, from);
            Assert.Equal(expectedTo, to);
        }

        /// <summary>
        /// Gets the IP address range when the IP address is an IPv6 address and throws an ArgumentException.
        /// </summary>
        [Fact]
        public void GetIPAddressRange_WhenIpAddressIsIPv6_ThrowsArgumentException()
        {
            // Arrange
            IPAddress ipAddress = IPAddress.IPv6Loopback;

            // Act
            void Act() => Scanner.GetIPAddressRange(ipAddress);

            // Assert
            ArgumentException exception = Assert.Throws<ArgumentException>(Act);
            Assert.Equal("ipAddress", exception.ParamName);
        }

        /// <summary>
        /// Gets the IP address range when the IP address is null and throws an ArgumentNullException.
        /// </summary>
        [Fact]
        public void GetIPAddressRange_WhenIpAddressIsNull_ThrowsArgumentNullException()
        {
            // Act
            static void Act() => Scanner.GetIPAddressRange(null!);

            // Assert
            Assert.Throws<ArgumentNullException>(Act);
        }

        /// <summary>
        /// Scans an IP range for a single address and verifies that the progress handler reports both 0% and 100% progress.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Fact]
        public async Task ScanIpRangeAsync_ForSingleAddress_ReportsZeroAndCompletionProgress()
        {
            // Arrange
            List<int> progressValues = [];

            // Act
            await Scanner.ScanIpRangeAsync([127, 0, 0, 1], [127, 0, 0, 1], _ => { }, progressHandler: progressValues.Add, timeoutMs: 1, maxDegreeOfParallelism: 1, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.Contains(0, progressValues);
            Assert.Contains(100, progressValues);
        }

        /// <summary>
        /// Scans an IP range when cancellation is requested and verifies that the method completes without throwing an exception.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Fact]
        public async Task ScanIpRangeAsync_WhenCancellationIsRequested_CompletesWithoutThrowing()
        {
            // Arrange
            using CancellationTokenSource cancellationTokenSource = new();
            cancellationTokenSource.Cancel();

            // Act
            Task Act() => Scanner.ScanIpRangeAsync(
                [127, 0, 0, 1],
                [127, 0, 0, 1],
                _ => { },
                timeoutMs: 1,
                maxDegreeOfParallelism: 1,
                cancellationToken: cancellationTokenSource.Token);

            // Assert
            await Act();
        }

        /// <summary>
        /// Scans an IP range when the "from" address has an invalid length and throws an ArgumentException.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Fact]
        public async Task ScanIpRangeAsync_WhenFromHasInvalidLength_ThrowsArgumentException()
        {
            // Act
            static Task Act() => Scanner.ScanIpRangeAsync([127, 0, 0], [127, 0, 0, 1], _ => { });

            // Assert
            ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(Act);
            Assert.Equal("from", exception.ParamName);
        }

        /// <summary>
        /// Scans an IP range when the "from" address is null and throws an ArgumentException.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Fact]
        public async Task ScanIpRangeAsync_WhenFromIsNull_ThrowsArgumentException()
        {
            // Act
            static Task Act() => Scanner.ScanIpRangeAsync(null!, [127, 0, 0, 1], _ => { });

            // Assert
            ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(Act);
            Assert.Equal("from", exception.ParamName);
        }

        /// <summary>
        /// Scans an IP range when the host found handler is null and throws an ArgumentNullException.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Fact]
        public async Task ScanIpRangeAsync_WhenHostFoundHandlerIsNull_ThrowsArgumentNullException()
        {
            // Act
            static Task Act() => Scanner.ScanIpRangeAsync(
                [127, 0, 0, 1],
                [127, 0, 0, 1],
                null!);

            // Assert
            ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(Act);
            Assert.Equal("hostFoundHandler", exception.ParamName);
        }

        /// <summary>
        /// Scans an IP range when ping fails and the exception handler is null, and verifies that no exceptions are thrown.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Fact]
        public async Task ScanIpRangeAsync_WhenPingFailsAndExceptionHandlerIsNull_DoesNotThrow()
        {
            // Act
            static Task Act() => Scanner.ScanIpRangeAsync(
                [127, 0, 0, 1],
                [127, 0, 0, 1],
                _ => { },
                exceptionHandler: null,
                timeoutMs: 1,
                maxDegreeOfParallelism: 1);

            // Assert
            await Act();
        }

        /// <summary>
        /// Scans an IP range when the start address is greater than the end address and throws an ArgumentException.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Fact]
        public async Task ScanIpRangeAsync_WhenStartAddressIsGreaterThanEndAddress_ThrowsArgumentException()
        {
            // Act
            static Task Act() => Scanner.ScanIpRangeAsync(
                [192, 168, 1, 2],
                [192, 168, 1, 1],
                _ => { });

            // Assert
            ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(Act);
            Assert.Null(exception.ParamName);
        }

        /// <summary>
        /// Scans an IP range when the "to" address has an invalid length and throws an ArgumentException.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Fact]
        public async Task ScanIpRangeAsync_WhenToHasInvalidLength_ThrowsArgumentException()
        {
            // Act
            static Task Act() => Scanner.ScanIpRangeAsync([127, 0, 0, 1], [127, 0, 0], _ => { });

            // Assert
            ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(Act);
            Assert.Equal("to", exception.ParamName);
        }

        /// <summary>
        /// Scans an IP range when the "to" address is null and throws an ArgumentException.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Fact]
        public async Task ScanIpRangeAsync_WhenToIsNull_ThrowsArgumentException()
        {
            // Act
            static Task Act() => Scanner.ScanIpRangeAsync([127, 0, 0, 1], null!, _ => { });

            // Assert
            ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(Act);
            Assert.Equal("to", exception.ParamName);
        }
    }
}