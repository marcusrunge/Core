using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Numerics;

namespace MarcusRunge.Toolbox.Network.IPv6
{
    /// <summary>
    /// Contains methods for scanning IPv6 address ranges, such as pinging addresses to check if they are active.
    /// </summary>
    public static class Scanner
    {
        // IPv6 addresses are always 16 bytes long.
        private const int IPv6AddressLength = 16;

        // A lock object to synchronize access to the BigInteger counter. This is necessary because BigInteger is not thread-safe, and we need to ensure that increments to the counter are atomic.
        private static readonly Lock SyncRoot = new();

        /// <summary>
        /// Gets the host addresses of the local machine, filtering for IPv6 addresses only.
        /// </summary>
        /// <returns>An array of IPv6 addresses.</returns>
        public static IPAddress[] GetHostAddresses() =>
            [.. Dns.GetHostAddresses(Dns.GetHostName())
            .Where(x => x.AddressFamily == AddressFamily.InterNetworkV6)];

        /// <summary>
        /// Gets the IPv6 address range for the given IPv6 address and prefix length.
        /// </summary>
        /// <param name="ipAddress">The IPv6 address.</param>
        /// <param name="prefixLength">The IPv6 prefix length, from 0 to 128.</param>
        /// <returns>A tuple containing the start and end address as byte arrays.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static (byte[] from, byte[] to) GetIPAddressRange(IPAddress ipAddress, int prefixLength)
        {
            // Validate input parameters.
            ArgumentNullException.ThrowIfNull(ipAddress);
            // Ensure the address is IPv6.
            if (ipAddress.AddressFamily != AddressFamily.InterNetworkV6)
                throw new ArgumentException("Only IPv6 addresses are supported.", nameof(ipAddress));
            // Validate prefix length.
            if (prefixLength < 0 || prefixLength > 128)
                throw new ArgumentOutOfRangeException(nameof(prefixLength), "IPv6 prefix length must be between 0 and 128.");
            // Convert the IP address to a BigInteger for easier manipulation.
            byte[] addressBytes = ipAddress.GetAddressBytes();
            // Convert to BigInteger (treating the byte array as unsigned).
            BigInteger addressValue = ToBigInteger(addressBytes);
            BigInteger mask = CreatePrefixMask(prefixLength);
            // Calculate the start and end values of the range.
            BigInteger startValue = addressValue & mask;
            BigInteger endValue = startValue | (MaxIPv6Value() ^ mask);

            return (ToIPv6Bytes(startValue), ToIPv6Bytes(endValue));
        }

        /// <summary>
        /// Scans an IPv6 range asynchronously.
        /// </summary>
        /// <param name="from">Start IPv6 address as 16-byte array.</param>
        /// <param name="to">End IPv6 address as 16-byte array.</param>
        /// <param name="hostFoundHandler">Invoked when a host replies successfully.</param>
        /// <param name="progressHandler">Optional progress handler in percent.</param>
        /// <param name="exceptionHandler">Optional exception handler.</param>
        /// <param name="timeoutMs">Ping timeout in milliseconds.</param>
        /// <param name="maxDegreeOfParallelism">Maximum number of parallel ping operations.</param>
        /// <param name="maxAddressesToScan">
        /// Optional safety limit. IPv6 ranges can be astronomically large.
        /// Default is 1,000,000 addresses.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public static async Task ScanIpRangeAsync(
            byte[] from,
            byte[] to,
            Action<string> hostFoundHandler,
            Action<int>? progressHandler = null,
            Action<Exception>? exceptionHandler = null,
            int timeoutMs = 1000,
            int maxDegreeOfParallelism = 128,
            BigInteger? maxAddressesToScan = null,
            CancellationToken cancellationToken = default)
        {
            // Validate input parameters.
            ValidateIPv6AddressBytes(from, nameof(from));
            ValidateIPv6AddressBytes(to, nameof(to));
            // Ensure hostFoundHandler is provided.
            ArgumentNullException.ThrowIfNull(hostFoundHandler);
            // Validate timeout and parallelism parameters.
            if (timeoutMs <= 0)
                throw new ArgumentOutOfRangeException(nameof(timeoutMs), "Timeout must be greater than zero.");
            // Allow -1 for infinite parallelism, but disallow other non-positive values.
            if (maxDegreeOfParallelism <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism), "Maximum degree of parallelism must be greater than zero.");
            // Convert the from and to addresses to BigInteger for easier range calculations.
            BigInteger start = ToBigInteger(from);
            BigInteger end = ToBigInteger(to);
            // Validate that the start address is not greater than the end address.
            if (start > end)
                throw new ArgumentException("Start address must not be greater than end address.");
            // Calculate the total number of addresses in the range.
            BigInteger total = end - start + BigInteger.One;
            // Apply the safety limit to prevent scanning excessively large ranges.
            BigInteger limit = maxAddressesToScan ?? new BigInteger(1_000_000);
            // Ensure the limit is a positive number.
            if (limit <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxAddressesToScan), "Maximum addresses to scan must be greater than zero.");
            // If the total number of addresses exceeds the limit, throw an exception to prevent accidental scans of huge ranges.
            if (total > limit)
            {
                throw new InvalidOperationException(
                    $"IPv6 range contains {total:N0} addresses, which exceeds the configured safety limit of {limit:N0}. " +
                    "Use a smaller IPv6 prefix/range or explicitly increase maxAddressesToScan.");
            }
            // Use a thread-safe counter to track progress across parallel tasks.
            BigInteger completed = BigInteger.Zero;
            // Initialize the next progress threshold to 10% increments.
            int nextProgress = 10;
            // Report initial progress as 0%.
            progressHandler?.Invoke(0);
            // Configure parallel options with the provided cancellation token and degree of parallelism.
            var options = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = maxDegreeOfParallelism
            };

            try
            {
                // Enumerate the range of addresses and ping each one in parallel.
                await Parallel.ForEachAsync(
                    EnumerateRange(start, end),
                    options,
                    async (ipValue, token) =>
                    {
                        try
                        {
                            // Check for cancellation before starting the ping operation.
                            token.ThrowIfCancellationRequested();
                            // Convert the current BigInteger value back to an IPAddress instance.
                            IPAddress ipAddress = ToIPAddress(ipValue);
                            // Create a new Ping instance for this operation. Ping is not thread-safe, so we create a new instance for each task.
                            using var ping = new Ping();
                            // Send the ping asynchronously with the specified timeout.
                            PingReply reply = await ping.SendPingAsync(ipAddress, timeoutMs);
                            // If the ping was successful, invoke the host found handler with the IP address as a string.
                            if (reply.Status == IPStatus.Success)
                            {
                                hostFoundHandler(ipAddress.ToString());
                            }
                        }
                        // Catch OperationCanceledException separately to allow it to propagate and stop the scan when cancellation is requested.
                        catch (OperationCanceledException) when (token.IsCancellationRequested)
                        {
                            throw;
                        }
                        // Catch specific exceptions related to pinging and socket operations, and invoke the exception handler if provided.
                        catch (PingException ex)
                        {
                            exceptionHandler?.Invoke(ex);
                        }
                        // Catch SocketException which can occur if the network is unreachable or other socket-related issues arise.
                        catch (SocketException ex)
                        {
                            exceptionHandler?.Invoke(ex);
                        }
                        // Catch any other unexpected exceptions to prevent the entire scan from crashing, and invoke the exception handler if provided.
                        catch (Exception ex)
                        {
                            exceptionHandler?.Invoke(ex);
                        }
                        // In the finally block, we increment the completed counter and report progress. This ensures that progress is updated even if exceptions occur.
                        finally
                        {
                            BigInteger done = Increment(ref completed);
                            ReportProgress(done, total, progressHandler, ref nextProgress);
                        }
                    });
                // After the parallel loop completes, ensure that progress is reported as 100% if it hasn't already been reported.
                progressHandler?.Invoke(100);
            }
            catch (OperationCanceledException)
            {
                // Scan was cancelled.
                throw;
            }
        }

        /// <summary>
        /// Scans an IPv6 range asynchronously using IPAddress instances.
        /// This overload preserves readability at the call site.
        /// </summary>
        public static Task ScanIpRangeAsync(
            IPAddress from,
            IPAddress to,
            Action<string> hostFoundHandler,
            Action<int>? progressHandler = null,
            Action<Exception>? exceptionHandler = null,
            int timeoutMs = 1000,
            int maxDegreeOfParallelism = 128,
            BigInteger? maxAddressesToScan = null,
            CancellationToken cancellationToken = default)
        {
            // Validate that the 'from' and 'to' parameters are not null.
            ArgumentNullException.ThrowIfNull(from);
            ArgumentNullException.ThrowIfNull(to);
            // Validate that the 'from' address is an IPv6 address.
            if (from.AddressFamily != AddressFamily.InterNetworkV6)
                throw new ArgumentException("Only IPv6 addresses are supported.", nameof(from));
            // Validate that the 'to' address is also IPv6.
            if (to.AddressFamily != AddressFamily.InterNetworkV6)
                throw new ArgumentException("Only IPv6 addresses are supported.", nameof(to));
            // Delegate to the main scanning method using the byte array representations of the IP addresses.
            return ScanIpRangeAsync(
                from.GetAddressBytes(),
                to.GetAddressBytes(),
                hostFoundHandler,
                progressHandler,
                exceptionHandler,
                timeoutMs,
                maxDegreeOfParallelism,
                maxAddressesToScan,
                cancellationToken);
        }

        /// <summary>
        /// Scans an IPv6 prefix asynchronously.
        /// </summary>
        public static Task ScanIpRangeAsync(
            IPAddress ipAddress,
            int prefixLength,
            Action<string> hostFoundHandler,
            Action<int>? progressHandler = null,
            Action<Exception>? exceptionHandler = null,
            int timeoutMs = 1000,
            int maxDegreeOfParallelism = 128,
            BigInteger? maxAddressesToScan = null,
            CancellationToken cancellationToken = default)
        {
            // Get the start and end addresses for the given IPv6 address and prefix length.
            var (from, to) = GetIPAddressRange(ipAddress, prefixLength);
            // Delegate to the main scanning method using the calculated range.
            return ScanIpRangeAsync(
                from,
                to,
                hostFoundHandler,
                progressHandler,
                exceptionHandler,
                timeoutMs,
                maxDegreeOfParallelism,
                maxAddressesToScan,
                cancellationToken);
        }

        // This method creates a subnet mask for the given prefix length. It calculates the mask by creating a host mask with the appropriate number of bits set to 1 and then XORing it with the maximum IPv6 value to get the network mask.
        private static BigInteger CreatePrefixMask(int prefixLength)
        {
            // Creates a subnet mask for the given prefix length. For example, a prefix length of 64 would create a mask with the first 64 bits set to 1 and the remaining 64 bits set to 0.
            if (prefixLength == 0)
                return BigInteger.Zero;
            // The maximum value for an IPv6 address is 2^128 - 1. We create a host mask with the last (128 - prefixLength) bits set to 1, and then XOR it with the max value to get the network mask.
            BigInteger max = MaxIPv6Value();
            BigInteger hostBits = 128 - prefixLength;
            BigInteger hostMask = (BigInteger.One << (int)hostBits) - BigInteger.One;
            // XORing the host mask with the max value gives us the network mask, which has the first prefixLength bits set to 1 and the rest set to 0.
            return max ^ hostMask;
        }

        // This method generates all BigInteger values from start to end inclusive. It is used to enumerate the IPv6 address range. The use of yield return allows us to generate the values on-the-fly without needing to store them all in memory at once, which is important given the potentially large size of IPv6 ranges.
        private static IEnumerable<BigInteger> EnumerateRange(BigInteger start, BigInteger end)
        {
            // This method generates all BigInteger values from start to end inclusive. It is used to enumerate the IPv6 address range.
            for (BigInteger current = start; current <= end; current++)
            {
                // Use yield return to generate values on-the-fly without storing them all in memory at once. This is important given the potentially large size of IPv6 ranges.
                yield return current;
            }
        }

        // This method safely increments a BigInteger value in a thread-safe manner using a lock. Since BigInteger is not thread-safe, we need to ensure that only one thread can modify it at a time. The method takes a reference to the BigInteger value, increments it by one, and returns the new value.
        private static BigInteger Increment(ref BigInteger value)
        {
            // This method safely increments a BigInteger value in a thread-safe manner using a lock. Since BigInteger is not thread-safe, we need to ensure that only one thread can modify it at a time.
            lock (SyncRoot)
            {
                // Increment the value by one and return the new value. This is used to track the number of completed ping operations across multiple threads.
                value += BigInteger.One;
                return value;
            }
        }

        // This method returns the maximum possible value for an IPv6 address, which is 2^128 - 1. This is used in calculations to determine the end of an IPv6 range and to create subnet masks.
        private static BigInteger MaxIPv6Value() => (BigInteger.One << 128) - BigInteger.One;

        // This method calculates the current progress percentage and invokes the progress handler if the next progress threshold has been reached. It uses Interlocked operations to safely update the next progress threshold across multiple threads. The method checks if the progress handler is null or if the total is zero or negative, in which case it returns early. It then calculates the current progress percentage and uses a loop with Interlocked.CompareExchange to check if the current progress has reached the next threshold. If it has, it updates the next threshold to the next 10% increment and invokes the progress handler with the current percentage.
        private static void ReportProgress(
            BigInteger completed,
            BigInteger total,
            Action<int>? progressHandler,
            ref int nextProgress)
        {
            // This method calculates the current progress percentage and invokes the progress handler if the next progress threshold has been reached. It uses Interlocked operations to safely update the next progress threshold across multiple threads.
            if (progressHandler == null || total <= 0)
                return;
            // Calculate the current progress percentage. We multiply by 100 before dividing to get a percentage value.
            int percent = (int)((completed * 100) / total);
            // Loop to check if we've reached the next progress threshold. We use a loop with Interlocked.CompareExchange to safely update the nextProgress variable across multiple threads without using locks.
            while (true)
            {
                // Read the current nextProgress threshold in a thread-safe manner. We use Volatile.Read to ensure we get the latest value across threads.
                int currentTarget = Volatile.Read(ref nextProgress);
                // If the current progress percentage is less than the next target threshold, or if the target threshold is greater than 100%, we break out of the loop without reporting progress.
                if (percent < currentTarget || currentTarget > 100)
                    break;
                // Attempt to update the nextProgress threshold to the next 10% increment. If another thread has already updated it, we read the new value and check again.
                if (Interlocked.CompareExchange(
                        ref nextProgress,
                        currentTarget + 10,
                        currentTarget) == currentTarget)
                {
                    // Successfully updated the next progress threshold, so we can now invoke the progress handler with the current percentage.
                    progressHandler(currentTarget);
                }
            }
        }

        // This method converts a 16-byte array representing an IPv6 address into a BigInteger. The byte array is treated as an unsigned integer in big-endian format. We first validate that the byte array is the correct length for an IPv6 address. Then we create a new byte array with an extra byte for the sign (set to 0 for positive) and reverse the order of the bytes to convert from big-endian to little-endian format, which is what BigInteger expects. Finally, we create a new BigInteger from the little-endian byte array.
        private static BigInteger ToBigInteger(byte[] address)
        {
            // Converts a 16-byte IPv6 address to a BigInteger. The byte array is treated as an unsigned integer in big-endian format.
            ValidateIPv6AddressBytes(address, nameof(address));
            // Create a new byte array with an extra byte for the sign (set to 0 for positive) and reverse the order to convert from big-endian to little-endian.
            byte[] unsignedLittleEndian = new byte[IPv6AddressLength + 1];
            // Copy the bytes in reverse order to convert from big-endian to little-endian.
            for (int i = 0; i < IPv6AddressLength; i++)
            {
                unsignedLittleEndian[i] = address[IPv6AddressLength - 1 - i];
            }
            // The last byte (most significant byte) is set to 0 to ensure the BigInteger is treated as positive (unsigned).
            return new BigInteger(unsignedLittleEndian);
        }

        // This method converts a BigInteger back to an IPAddress instance. It first converts the BigInteger to a 16-byte array representing the IPv6 address in big-endian format. The ToIPv6Bytes method handles the conversion and validation of the BigInteger value to ensure it fits within the IPv6 address range. Finally, we create a new IPAddress instance from the byte array.
        private static IPAddress ToIPAddress(BigInteger value) => new(ToIPv6Bytes(value));

        // This method converts a BigInteger back to a 16-byte array representing an IPv6 address. The BigInteger is treated as an unsigned integer and converted to big-endian format. We first validate that the value is within the valid range for IPv6 addresses. Then we create a new byte array for the result and get the little-endian byte array from the BigInteger. We copy the bytes in reverse order to convert from little-endian to big-endian format, taking only the first 16 bytes since IPv6 addresses are 16 bytes long.
        private static byte[] ToIPv6Bytes(BigInteger value)
        {
            // Converts a BigInteger back to a 16-byte array representing an IPv6 address. The BigInteger is treated as an unsigned integer and converted to big-endian format.
            if (value < BigInteger.Zero || value > MaxIPv6Value())
                throw new ArgumentOutOfRangeException(nameof(value), "Value is outside the IPv6 address range.");
            // Get the little-endian byte array from the BigInteger and reverse it to get big-endian format for the IPv6 address.
            byte[] result = new byte[IPv6AddressLength];
            byte[] littleEndian = value.ToByteArray();

            // Copy the bytes in reverse order to convert from little-endian to big-endian. We only take the first 16 bytes, as IPv6 addresses are 16 bytes long.
            for (int i = 0; i < Math.Min(littleEndian.Length, IPv6AddressLength); i++)
            {
                result[IPv6AddressLength - 1 - i] = littleEndian[i];
            }
            // If the little-endian byte array is shorter than 16 bytes, the remaining bytes in the result will be left as 0, which is correct for representing smaller values.
            return result;
        }

        // This method validates that the provided byte array is a valid IPv6 address representation, which must be exactly 16 bytes long. If the byte array is null or does not have a length of 16, an ArgumentException is thrown with a message indicating the issue and the name of the parameter that caused it.
        private static void ValidateIPv6AddressBytes(byte[] address, string parameterName)
        {
            // Validates that the provided byte array is a valid IPv6 address representation (16 bytes).
            if (address == null || address.Length != IPv6AddressLength)
                throw new ArgumentException("IPv6 address must contain exactly 16 bytes.", parameterName);
        }
    }
}