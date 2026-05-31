using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MarcusRunge.Toolbox.Network.IPv4
{
    /// <summary>
    /// Contains methods for scanning IPv4 address ranges, such as pinging addresses to check if they are active.
    /// </summary>
    public static class Scanner
    {
        /// <summary>
        /// Gets the host addresses of the local machine, filtering for IPv4 addresses only.
        /// </summary>
        /// <returns>An array of IPv4 addresses.</returns>
        public static IPAddress[] GetHostAddresses() => [.. Dns.GetHostAddresses(Dns.GetHostName()).Where(x => x.AddressFamily == AddressFamily.InterNetwork)];

        /// <summary>
        /// Gets the range of IP addresses for the given IP address and subnet mask. The method calculates the starting and ending IP addresses in the range by performing bitwise operations on the host address and subnet mask. The starting address is obtained by performing a bitwise AND operation between the host address and subnet mask, while the ending address is obtained by performing a bitwise OR operation between the host address and the inverted subnet mask. This allows for scanning or enumerating all IP addresses within the calculated range.
        /// </summary>
        /// <param name="ipAddress"></param>
        /// <param name="subnetMask"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static (byte[] from, byte[] to) GetIPAddressRange(IPAddress ipAddress, IPAddress subnetMask)
        {
            // Validate that the ipAddress and subnetMask parameters are not null.
            ArgumentNullException.ThrowIfNull(ipAddress);
            ArgumentNullException.ThrowIfNull(subnetMask);
            // Validate that both the ipAddress and subnetMask are IPv4 addresses.
            if (ipAddress.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ArgumentException("Only IPv4 addresses are supported.", nameof(ipAddress));
            }
            // Validate that the subnetMask is an IPv4 address.
            if (subnetMask.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ArgumentException("Only IPv4 subnet masks are supported.", nameof(subnetMask));
            }
            // Get the byte arrays for the host address and subnet mask.
            byte[] hostAddress = ipAddress.GetAddressBytes();
            byte[] subnetAddress = subnetMask.GetAddressBytes();
            // Initialize byte arrays for the starting and ending addresses of the range.
            byte[] startAddress = new byte[4];
            byte[] endAddress = new byte[4];
            // Calculate the starting and ending addresses by performing bitwise operations on the host address and subnet mask.
            for (int i = 0; i < 4; i++)
            {
                startAddress[i] = (byte)(hostAddress[i] & subnetAddress[i]);
                endAddress[i] = (byte)(hostAddress[i] | (subnetAddress[i] ^ 0xFF));
            }

            return (startAddress, endAddress);
        }

        /// <summary>
        /// Gets the range of IP addresses for the given IP address based on its subnet mask. The subnet mask is determined using the Subnet.GetSubnetMask method, which calculates the subnet mask based on the class of the IP address. The method returns a tuple containing the starting and ending IP addresses in byte array format. This allows for scanning or enumerating all IP addresses within the calculated range.
        /// </summary>
        /// <param name="ipAddress"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static (byte[] from, byte[] to) GetIPAddressRange(IPAddress ipAddress)
        {
            // Validate that the ipAddress parameter is not null and is an IPv4 address.
            ArgumentNullException.ThrowIfNull(ipAddress);

            if (ipAddress.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ArgumentException("Only IPv4 addresses are supported.", nameof(ipAddress));
            }
            // Get the subnet mask for the given IP address using the Subnet.GetSubnetMask method.
            IPAddress subnetMask = IPv4.Subnet.GetSubnetMask(ipAddress);

            return GetIPAddressRange(ipAddress, subnetMask);
        }

        /// <summary>
        /// Scans the ip range asynchronous.
        /// </summary>
        /// <param name="from">From.</param>
        /// <param name="to">To.</param>
        /// <param name="hostFoundHandler">The host found handler.</param>
        /// <param name="progressHandler">The progress handler.</param>
        /// <param name="exceptionHandler">The exception handler.</param>
        /// <param name="timeoutMs">The timeout ms.</param>
        /// <param name="maxDegreeOfParallelism">The maximum degree of parallelism.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <exception cref="ArgumentException">
        /// Start address must contain exactly 4 bytes. - from
        /// or
        /// End address must contain exactly 4 bytes. - to
        /// or
        /// Start address must not be greater than end address.
        /// </exception>
        /// <exception cref="ArgumentNullException"></exception>
        public static async Task ScanIpRangeAsync(
            byte[] from,
            byte[] to,
            Action<string> hostFoundHandler,
            Action<int>? progressHandler = null,
            Action<Exception>? exceptionHandler = null,
            int timeoutMs = 1000,
            int maxDegreeOfParallelism = 128,
            CancellationToken cancellationToken = default
            )
        {
            // Validate that the 'from' address is not null and has exactly 4 bytes.
            if (from == null || from.Length != 4)
                throw new ArgumentException("Start address must contain exactly 4 bytes.", nameof(from));
            // Validate that the 'to' address is not null and has exactly 4 bytes.
            if (to == null || to.Length != 4)
                throw new ArgumentException("End address must contain exactly 4 bytes.", nameof(to));
            // Validate that the host found handler is not null.
            ArgumentNullException.ThrowIfNull(hostFoundHandler);

            uint start = ToUInt32(from);
            uint end = ToUInt32(to);
            // Validate that the start address is not greater than the end address.
            if (start > end)
                throw new ArgumentException("Start address must not be greater than end address.");
            // calculate the total number of addresses to scan and initialize the completed count.
            ulong total = (ulong)end - start + 1UL;
            ulong completed = 0;

            int nextProgress = 10;
            // Invoke the progress handler with 0% progress at the start of the scan.
            progressHandler?.Invoke(0);
            // Set up the parallel options for the scan, including the cancellation token and maximum degree of parallelism.
            var options = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = maxDegreeOfParallelism
            };

            try
            {
                // Use Parallel.ForEachAsync to scan the IP range in parallel, pinging each address and invoking the appropriate handlers based on the results.
                await Parallel.ForEachAsync(
                    EnumerateRange(start, end),
                    options,
                    async (ipValue, token) =>
                    {
                        try
                        {
                            // Check for cancellation before starting the ping operation.
                            token.ThrowIfCancellationRequested();
                            // Convert the current uint value to an IPAddress object.
                            IPAddress ipAddress = ToIPAddress(ipValue);
                            // Create a new Ping instance and send a ping request to the current IP address with the specified timeout.
                            using var ping = new Ping();
                            // Await the ping reply and check if the status indicates success. If so, invoke the host found handler with the IP address as a string.
                            PingReply reply = await ping.SendPingAsync(ipAddress, timeoutMs);
                            // If the ping reply indicates success, invoke the host found handler with the IP address as a string.
                            if (reply.Status == IPStatus.Success)
                            {
                                hostFoundHandler(ipAddress.ToString());
                            }
                        }
                        catch (OperationCanceledException) when (token.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            exceptionHandler?.Invoke(ex);
                        }
                        finally
                        {
                            ulong done = (ulong)Interlocked.Increment(ref completed);
                            ReportProgress(done, total, progressHandler, ref nextProgress);
                        }
                    });
            }
            catch (OperationCanceledException)
            {
                // Scan was cancelled.
            }
        }

        // Enumerates the range of uint values from start to end, inclusive.
        private static IEnumerable<uint> EnumerateRange(uint start, uint end)
        {
            // Yield each uint value in the specified range, checking for overflow when reaching uint.MaxValue.
            for (uint current = start; current <= end; current++)
            {
                yield return current;

                if (current == uint.MaxValue)
                    yield break;
            }
        }

        // Reports the progress of the scan by invoking the progress handler when certain percentage thresholds are crossed.
        private static void ReportProgress(ulong completed, ulong total, Action<int>? progressHandler, ref int nextProgress)
        {
            if (progressHandler == null || total == 0)
                return;

            int percent = (int)(completed * 100UL / total);

            // Loop to check if the current progress percentage has reached or exceeded the next target percentage, and if so, invoke the progress handler and update the next target percentage.
            while (true)
            {
                int currentTarget = Volatile.Read(ref nextProgress);

                if (percent < currentTarget || currentTarget > 100)
                    break;

                if (Interlocked.CompareExchange(
                        ref nextProgress,
                        currentTarget + 10,
                        currentTarget) == currentTarget)
                {
                    progressHandler(currentTarget);
                }
            }
        }

        // Converts a uint representing an IPv4 address back to an IPAddress object.
        private static IPAddress ToIPAddress(uint value) => new([(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value]);

        // Converts a 4-byte array to a uint representing the IPv4 address.
        private static uint ToUInt32(byte[] address) => ((uint)address[0] << 24) | ((uint)address[1] << 16) | ((uint)address[2] << 8) | address[3];
    }
}