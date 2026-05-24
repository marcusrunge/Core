using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace MarcusRunge.Toolbox
{
    /// <summary>
    /// This class provides functionality to create self-signed certificates for development and testing purposes. It allows you to generate a certificate with a specified common name, RSA key size, and validity period. The generated certificate is exported in PKCS#12 format and can be loaded into the Windows certificate store or used directly in applications that require SSL/TLS certificates.
    /// </summary>
    /// <remarks>
    /// Uses RSA (default 2048 bit), SHA-256, and RSASignaturePadding.Pkcs1. Adds
    /// BasicConstraints, KeyUsage (DigitalSignature, KeyEncipherment), EnhancedKeyUsage (Server Authentication),
    /// SubjectAlternativeName and SubjectKeyIdentifier. Exports as PKCS#12 (PFX) with the specified password
    /// and loads the certificate with X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable.
    /// Validity start is UTC-Now minus five minutes; validity duration defaults to 5 years.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public class CertificateProvider
    {
        /// <summary>
        /// Creates a self-signed certificate for development and testing purposes.
        /// </summary>
        /// <param name="password">The password for the PKCS#12 export.</param>
        /// <param name="commonName">The common name for the certificate.</param>
        /// <param name="rsaKeySize">The size of the RSA key (default is 2048).</param>
        /// <param name="years">The number of years the certificate is valid (default is 5).</param>
        /// <returns>The created X509Certificate2 instance.</returns>
        public static X509Certificate2 CreateCertificate(string password, string commonName, int rsaKeySize = 2048, int years = 5)
        {
            // Create a new RSA key pair with the specified key size
            using var rsa = RSA.Create(rsaKeySize);

            // Create a certificate request with the specified common name, RSA key, hash algorithm, and signature padding
            var request = new CertificateRequest(
                $"CN={commonName}",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            // Add Basic Constraints extension (not a CA, no path length constraint, critical)
            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(
                    false,
                    false,
                    0,
                    true));

            // Add Key Usage extension (Digital Signature and Key Encipherment, critical)
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature |
                    X509KeyUsageFlags.KeyEncipherment,
                    true));

            // Add Enhanced Key Usage extension (Server Authentication, not critical)
            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    [
                        new Oid("1.3.6.1.5.5.7.3.1") // Server Authentication
                    ],
                    false));

            // Add Subject Alternative Name extension with the common name as a DNS name
            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName(commonName);
            request.CertificateExtensions.Add(sanBuilder.Build());

            // Add Subject Key Identifier extension (not critical)
            request.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(
                    request.PublicKey,
                    false));

            // Create a self-signed certificate with the specified validity period
            using var certificate = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddMinutes(-5),
                DateTimeOffset.UtcNow.AddYears(years));

            // Set the friendly name of the certificate to the common name
            certificate.FriendlyName = commonName;

            // Export the certificate as PKCS#12 (PFX) with the specified password and load it with the appropriate key storage flags
            return X509CertificateLoader.LoadPkcs12(
                certificate.Export(X509ContentType.Pfx, password),
                password,
                X509KeyStorageFlags.MachineKeySet |
                X509KeyStorageFlags.Exportable);
        }
    }
}