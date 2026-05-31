using MarcusRunge.Toolbox.Network.IPv6;
using System.Net;
using System.Net.Sockets;
using System.Numerics;

namespace MarcusRunge.Toolbox.Network.Test
{
    /// <summary>
    /// Tests for the <see cref="Scanner"/> class.
    /// </summary>
    public class IPv6ScannerTest
    {
        /// <summary>
        /// Tests that <see cref="Scanner.GetHostAddresses"/> returns only IPv6 addresses.
        /// </summary>
        [Fact]
        public void GetHostAddresses_ReturnsOnlyIPv6Addresses()
        {
            IPAddress[] addresses = Scanner.GetHostAddresses();

            Assert.All(addresses, address =>
                Assert.Equal(AddressFamily.InterNetworkV6, address.AddressFamily));
        }

        /// <summary>
        /// Tests that <see cref="Scanner.GetIPAddressRange"/> returns the expected range for various inputs.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="prefixLength"></param>
        /// <param name="expectedFrom"></param>
        /// <param name="expectedTo"></param>
        [Theory]
        [InlineData(
            "2001:db8:abcd:1234:5678::1",
            64,
            "2001:db8:abcd:1234::",
            "2001:db8:abcd:1234:ffff:ffff:ffff:ffff")]
        [InlineData(
            "2001:db8:abcd:1234:5678::1",
            128,
            "2001:db8:abcd:1234:5678::1",
            "2001:db8:abcd:1234:5678::1")]
        [InlineData(
            "::1",
            0,
            "::",
            "ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff")]
        [InlineData(
            "ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff",
            128,
            "ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff",
            "ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff")]
        public void GetIPAddressRange_ReturnsExpectedRange(
            string input,
            int prefixLength,
            string expectedFrom,
            string expectedTo)
        {
            IPAddress ipAddress = IPAddress.Parse(input);

            var (from, to) = Scanner.GetIPAddressRange(ipAddress, prefixLength);

            Assert.Equal(IPAddress.Parse(expectedFrom).GetAddressBytes(), from);
            Assert.Equal(IPAddress.Parse(expectedTo).GetAddressBytes(), to);
        }

        /// <summary>
        /// Tests that <see cref="Scanner.GetIPAddressRange"/> throws appropriate exceptions for invalid inputs.
        /// </summary>
        [Fact]
        public void GetIPAddressRange_WithNullAddress_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                Scanner.GetIPAddressRange(null!, 64));
        }

        /// <summary>
        /// Tests that <see cref="Scanner.GetIPAddressRange"/> throws an <see cref="ArgumentException"/> when given an IPv4 address, since it only supports IPv6 addresses.
        /// </summary>
        [Fact]
        public void GetIPAddressRange_WithIPv4Address_ThrowsArgumentException()
        {
            IPAddress ipv4Address = IPAddress.Parse("192.168.0.1");

            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                Scanner.GetIPAddressRange(ipv4Address, 24));

            Assert.Equal("ipAddress", exception.ParamName);
        }

        /// <summary>
        /// Tests that <see cref="Scanner.GetIPAddressRange"/> throws an <see cref="ArgumentOutOfRangeException"/> when given an invalid prefix length (less than 0 or greater than 128).
        /// </summary>
        /// <param name="prefixLength"></param>
        [Theory]
        [InlineData(-1)]
        [InlineData(129)]
        public void GetIPAddressRange_WithInvalidPrefixLength_ThrowsArgumentOutOfRangeException(
            int prefixLength)
        {
            IPAddress ipAddress = IPAddress.Parse("2001:db8::1");

            ArgumentOutOfRangeException exception =
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    Scanner.GetIPAddressRange(ipAddress, prefixLength));

            Assert.Equal("prefixLength", exception.ParamName);
        }

        /// <summary>
        /// Tests that <see cref="Scanner.ScanIpRangeAsync(byte[], byte[], Action{string}, Action{int}?, Action{Exception}?, int, int, BigInteger?, CancellationToken)"/> throws appropriate exceptions for invalid inputs, such as null byte arrays, invalid byte array lengths, null handlers, invalid timeout and parallelism values, and when the start address is greater than the end address.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task ScanIpRangeAsync_WithNullFromBytes_ThrowsArgumentException()
        {
            byte[] to = IPAddress.Parse("::1").GetAddressBytes();

            ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                Scanner.ScanIpRangeAsync(null!, to, _ => { }, cancellationToken: TestContext.Current.CancellationToken));

            Assert.Equal("from", exception.ParamName);
        }

        /// <summary>
        /// Tests that <see cref="Scanner.ScanIpRangeAsync(byte[], byte[], Action{string}, Action{int}?, Action{Exception}?, int, int, BigInteger?, CancellationToken)"/> throws an <see cref="ArgumentException"/> when the "to" byte array is null, since it is required to define the end of the IP range to scan.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Fact]
        public async Task ScanIpRangeAsync_WithNullToBytes_ThrowsArgumentException()
        {
            byte[] from = IPAddress.Parse("::1").GetAddressBytes();

            ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                Scanner.ScanIpRangeAsync(from, null!, _ => { }, cancellationToken: TestContext.Current.CancellationToken));

            Assert.Equal("to", exception.ParamName);
        }

        /// <summary>
        /// Tests that <see cref="Scanner.ScanIpRangeAsync(byte[], byte[], Action{string}, Action{int}?, Action{Exception}?, int, int, BigInteger?, CancellationToken)"/> throws an <see cref="ArgumentException"/> when the "from" byte array has an invalid length (not equal to 16 bytes for IPv6), since it cannot represent a valid IPv6 address.
        /// </summary>
        /// <param name="length"></param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Theory]
        [InlineData(0)]
        [InlineData(15)]
        [InlineData(17)]
        public async Task ScanIpRangeAsync_WithInvalidFromByteLength_ThrowsArgumentException(
            int length)
        {
            byte[] from = new byte[length];
            byte[] to = IPAddress.Parse("::1").GetAddressBytes();

            ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                Scanner.ScanIpRangeAsync(from, to, _ => { }, cancellationToken: TestContext.Current.CancellationToken));

            Assert.Equal("from", exception.ParamName);
        }

        /// <summary>
        /// Tests that <see cref="Scanner.ScanIpRangeAsync(byte[], byte[], Action{string}, Action{int}?, Action{Exception}?, int, int, BigInteger?, CancellationToken)"/> throws an <see cref="ArgumentException"/> when the "to" byte array has an invalid length (not equal to 16 bytes for IPv6), since it cannot represent a valid IPv6 address.
        /// </summary>
        /// <param name="length"></param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Theory]
        [InlineData(0)]
        [InlineData(15)]
        [InlineData(17)]
        public async Task ScanIpRangeAsync_WithInvalidToByteLength_ThrowsArgumentException(
            int length)
        {
            byte[] from = IPAddress.Parse("::1").GetAddressBytes();
            byte[] to = new byte[length];

            ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                Scanner.ScanIpRangeAsync(from, to, _ => { }, cancellationToken: TestContext.Current.CancellationToken));

            Assert.Equal("to", exception.ParamName);
        }

        /// <summary>
        /// Tests that <see cref="Scanner.ScanIpRangeAsync(byte[], byte[], Action{string}, Action{int}?, Action{Exception}?, int, int, BigInteger?, CancellationToken)"/> throws an <see cref="ArgumentNullException"/> when the "hostFoundHandler" is null, since it is required to handle found hosts during the scanning process.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Fact]
        public async Task ScanIpRangeAsync_WithNullHostFoundHandler_ThrowsArgumentNullException()
        {
            byte[] from = IPAddress.Parse("::1").GetAddressBytes();
            byte[] to = IPAddress.Parse("::1").GetAddressBytes();

            ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
                Scanner.ScanIpRangeAsync(from, to, null!, cancellationToken: TestContext.Current.CancellationToken));

            Assert.Equal("hostFoundHandler", exception.ParamName);
        }

        /// <summary>
        /// Tests that <see cref="Scanner.ScanIpRangeAsync(byte[], byte[], Action{string}, Action{int}?, Action{Exception}?, int, int, BigInteger?, CancellationToken)"/> throws an <see cref="ArgumentOutOfRangeException"/> when the "timeoutMs" parameter is set to an invalid value (less than or equal to 0), since a positive timeout value is required for the scanning operation to function correctly.
        /// </summary>
        /// <param name="timeoutMs"></param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task ScanIpRangeAsync_WithInvalidTimeout_ThrowsArgumentOutOfRangeException(
            int timeoutMs)
        {
            byte[] from = IPAddress.Parse("::1").GetAddressBytes();
            byte[] to = IPAddress.Parse("::1").GetAddressBytes();

            ArgumentOutOfRangeException exception =
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                    Scanner.ScanIpRangeAsync(from, to, _ => { }, timeoutMs: timeoutMs, cancellationToken: TestContext.Current.CancellationToken));

            Assert.Equal("timeoutMs", exception.ParamName);
        }

        /// <summary>
        /// Tests that <see cref="Scanner.ScanIpRangeAsync(byte[], byte[], Action{string}, Action{int}?, Action{Exception}?, int, int, BigInteger?, CancellationToken)"/> throws an <see cref="ArgumentOutOfRangeException"/> when the "maxDegreeOfParallelism" parameter is set to an invalid value (less than or equal to 0), since a positive value is required to specify the degree of parallelism for the scanning operation.
        /// </summary>
        /// <param name="maxDegreeOfParallelism"></param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task ScanIpRangeAsync_WithInvalidMaxDegreeOfParallelism_ThrowsArgumentOutOfRangeException(
            int maxDegreeOfParallelism)
        {
            byte[] from = IPAddress.Parse("::1").GetAddressBytes();
            byte[] to = IPAddress.Parse("::1").GetAddressBytes();

            ArgumentOutOfRangeException exception =
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                    Scanner.ScanIpRangeAsync(from, to, _ => { }, maxDegreeOfParallelism: maxDegreeOfParallelism, cancellationToken: TestContext.Current.CancellationToken));

            Assert.Equal("maxDegreeOfParallelism", exception.ParamName);
        }

        /// <summary>
        /// Tests that <see cref="Scanner.ScanIpRangeAsync(byte[], byte[], Action{string}, Action{int}?, Action{Exception}?, int, int, BigInteger?, CancellationToken)"/> throws an <see cref="ArgumentException"/> when the "from" address is greater than the "to" address, since it would result in an invalid IP range for scanning.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Fact]
        public async Task ScanIpRangeAsync_WithStartGreaterThanEnd_ThrowsArgumentException()
        {
            byte[] from = IPAddress.Parse("::2").GetAddressBytes();
            byte[] to = IPAddress.Parse("::1").GetAddressBytes();

            ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                Scanner.ScanIpRangeAsync(from, to, _ => { }, cancellationToken: TestContext.Current.CancellationToken));

            Assert.Contains("Start address", exception.Message);
        }

        /// <summary>
        /// Tests that <see cref="Scanner.ScanIpRangeAsync(byte[], byte[], Action{string}, Action{int}?, Action{Exception}?, int, int, BigInteger?, CancellationToken)"/> throws an <see cref="ArgumentOutOfRangeException"/> when the "maxAddressesToScan" parameter is set to an invalid value (less than or equal to 0), since a positive value is required to specify the maximum number of addresses to scan in order to prevent excessive scanning operations.
        /// </summary>
        /// <param name="maxAddressesToScan"></param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task ScanIpRangeAsync_WithInvalidMaxAddressesToScan_ThrowsArgumentOutOfRangeException(
            int maxAddressesToScan)
        {
            byte[] from = IPAddress.Parse("::1").GetAddressBytes();
            byte[] to = IPAddress.Parse("::1").GetAddressBytes();

            ArgumentOutOfRangeException exception =
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                    Scanner.ScanIpRangeAsync(from, to, _ => { }, maxAddressesToScan: new BigInteger(maxAddressesToScan), cancellationToken: TestContext.Current.CancellationToken));

            Assert.Equal("maxAddressesToScan", exception.ParamName);
        }

        /// <summary>
        /// Tests that <see cref="Scanner.ScanIpRangeAsync(byte[], byte[], Action{string}, Action{int}?, Action{Exception}?, int, int, BigInteger?, CancellationToken)"/> throws an <see cref="InvalidOperationException"/> when the specified IP range exceeds the configured safety limit for scanning, since it would result in an excessively large number of addresses to scan and could potentially cause performance issues or unintended consequences.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Fact]
        public async Task ScanIpRangeAsync_WhenRangeExceedsSafetyLimit_ThrowsInvalidOperationException()
        {
            byte[] from = IPAddress.Parse("::1").GetAddressBytes();
            byte[] to = IPAddress.Parse("::2").GetAddressBytes();

            InvalidOperationException exception =
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    Scanner.ScanIpRangeAsync(from, to, _ => { }, maxAddressesToScan: BigInteger.One, cancellationToken: TestContext.Current.CancellationToken));

            Assert.Contains("exceeds the configured safety limit", exception.Message);
        }

        /// <summary>
        /// Tests that <see cref="Scanner.ScanIpRangeAsync(byte[], byte[], Action{string}, Action{int}?, Action{Exception}?, int, int, BigInteger?, CancellationToken)"/> throws an <see cref="ArgumentException"/> when the "from" IP address is an IPv4 address, since the method is designed to work with IPv6 addresses and would not be able to handle an IPv4 address correctly.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Fact]
        public async Task ScanIpRangeAsync_WithIPAddressOverloadAndIPv4From_ThrowsArgumentException()
        {
            IPAddress from = IPAddress.Parse("192.168.0.1");
            IPAddress to = IPAddress.Parse("::1");

            ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                Scanner.ScanIpRangeAsync(from, to, _ => { }, cancellationToken: TestContext.Current.CancellationToken));

            Assert.Equal("from", exception.ParamName);
        }

        /// <summary>
        /// Tests that <see cref="Scanner.ScanIpRangeAsync(byte[], byte[], Action{string}, Action{int}?, Action{Exception}?, int, int, BigInteger?, CancellationToken)"/> throws an <see cref="ArgumentException"/> when the "to" IP address is an IPv4 address, since the method is designed to work with IPv6 addresses and would not be able to handle an IPv4 address correctly.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Fact]
        public async Task ScanIpRangeAsync_WithIPAddressOverloadAndIPv4To_ThrowsArgumentException()
        {
            IPAddress from = IPAddress.Parse("::1");
            IPAddress to = IPAddress.Parse("192.168.0.1");

            ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                Scanner.ScanIpRangeAsync(from, to, _ => { }, cancellationToken: TestContext.Current.CancellationToken));

            Assert.Equal("to", exception.ParamName);
        }

        /// <summary>
        /// Tests that <see cref="Scanner.ScanIpRangeAsync(byte[], byte[], Action{string}, Action{int}?, Action{Exception}?, int, int, BigInteger?, CancellationToken)"/> throws an <see cref="InvalidOperationException"/> when the specified IP range defined by an IP address and prefix length exceeds the configured safety limit for scanning, since it would result in an excessively large number of addresses to scan and could potentially cause performance issues or unintended consequences.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Fact]
        public async Task ScanIpRangeAsync_WithPrefixOverloadAndLargeRange_ThrowsInvalidOperationException()
        {
            IPAddress ipAddress = IPAddress.Parse("2001:db8::1");

            InvalidOperationException exception =
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    Scanner.ScanIpRangeAsync(ipAddress, 64, _ => { }, maxAddressesToScan: new BigInteger(100), cancellationToken: TestContext.Current.CancellationToken));

            Assert.Contains("exceeds the configured safety limit", exception.Message);
        }

        /// <summary>
        /// Tests that <see cref="Scanner.ScanIpRangeAsync(byte[], byte[], Action{string}, Action{int}?, Action{Exception}?, int, int, BigInteger?, CancellationToken)"/> reports progress correctly by invoking the provided progress handler with appropriate progress values (0% at the start and 100% at the end) when scanning a single IP address, since it allows for tracking the progress of the scanning operation and provides feedback to the user or calling code about the scanning status.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Fact]
        public async Task ScanIpRangeAsync_WithSingleAddress_ReportsInitialAndFinalProgress()
        {
            byte[] from = IPAddress.Parse("::1").GetAddressBytes();
            byte[] to = IPAddress.Parse("::1").GetAddressBytes();
            List<int> progressValues = [];

            await Scanner.ScanIpRangeAsync(from, to, _ => { }, progressHandler: progressValues.Add, exceptionHandler: _ => { }, timeoutMs: 1, maxDegreeOfParallelism: 1, maxAddressesToScan: BigInteger.One, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Contains(0, progressValues);
            Assert.Contains(100, progressValues);
        }

        /// <summary>
        /// Tests that <see cref="Scanner.ScanIpRangeAsync(byte[], byte[], Action{string}, Action{int}?, Action{Exception}?, int, int, BigInteger?, CancellationToken)"/> throws an <see cref="OperationCanceledException"/> when the provided cancellation token is already canceled before the scanning operation begins, since it allows for proper handling of cancellation scenarios and ensures that the scanning operation can be gracefully terminated when requested by the caller or user.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>

        [Fact]
        public async Task ScanIpRangeAsync_WithCancelledToken_ThrowsOperationCanceledException()
        {
            byte[] from = IPAddress.Parse("::1").GetAddressBytes();
            byte[] to = IPAddress.Parse("::1").GetAddressBytes();

            using CancellationTokenSource cancellationTokenSource = new();
            await cancellationTokenSource.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                Scanner.ScanIpRangeAsync(
                    from,
                    to,
                    _ => { },
                    exceptionHandler: _ => { },
                    timeoutMs: 1,
                    maxDegreeOfParallelism: 1,
                    maxAddressesToScan: BigInteger.One,
                    cancellationToken: cancellationTokenSource.Token));
        }
    }
}