namespace MarcusRunge.Toolbox.Test
{
    using global::MarcusRunge.Toolbox.Security;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using Xunit;

    namespace MarcusRunge.Toolbox.Security.Test
    {
        /// <summary>
        /// Contains unit tests for the <see cref="CertificateProvider"/> class.
        /// </summary>
        public class CertificateProviderTest
        {
            /// <summary>
            /// Creates the certificate should return certificate.
            /// </summary>
            [Fact]
            public void CreateCertificate_ShouldReturnCertificate()
            {
                // Arrange
                const string password = "test-password";
                const string commonName = "localhost";

                // Act
                using var certificate =
                    CertificateProvider.CreateCertificate(password, commonName);

                // Assert
                Assert.NotNull(certificate);
            }

            /// <summary>
            /// Creates the name of the certificate should contain common.
            /// </summary>
            [Fact]
            public void CreateCertificate_ShouldContainCommonName()
            {
                // Arrange
                const string password = "test-password";
                const string commonName = "localhost";

                // Act
                using var certificate =
                    CertificateProvider.CreateCertificate(password, commonName);

                // Assert
                Assert.Contains($"CN={commonName}", certificate.Subject);
            }

            /// <summary>
            /// Creates the certificate should have private key.
            /// </summary>
            [Fact]
            public void CreateCertificate_ShouldHavePrivateKey()
            {
                // Arrange
                const string password = "test-password";

                // Act
                using var certificate =
                    CertificateProvider.CreateCertificate(password, "localhost");

                // Assert
                Assert.True(certificate.HasPrivateKey);
            }

            /// <summary>
            /// Creates the certificate should use requested key size.
            /// </summary>
            [Fact]
            public void CreateCertificate_ShouldUseRequestedKeySize()
            {
                // Arrange
                const string password = "test-password";
                const int keySize = 4096;

                // Act
                using var certificate =
                    CertificateProvider.CreateCertificate(
                        password,
                        "localhost",
                        keySize);

                using RSA? rsa = certificate.GetRSAPublicKey();

                // Assert
                Assert.NotNull(rsa);
                Assert.Equal(keySize, rsa!.KeySize);
            }

            /// <summary>
            /// Creates the certificate should be self signed.
            /// </summary>
            [Fact]
            public void CreateCertificate_ShouldBeSelfSigned()
            {
                // Arrange
                const string password = "test-password";

                // Act
                using var certificate =
                    CertificateProvider.CreateCertificate(password, "localhost");

                // Assert
                Assert.Equal(certificate.Subject, certificate.Issuer);
            }

            /// <summary>
            /// Creates the certificate should contain server authentication eku.
            /// </summary>
            [Fact]
            public void CreateCertificate_ShouldContainServerAuthenticationEku()
            {
                // Arrange
                const string password = "test-password";

                // Act
                using var certificate =
                    CertificateProvider.CreateCertificate(password, "localhost");

                // Assert
                var ekuExtension = certificate.Extensions
                    .OfType<X509EnhancedKeyUsageExtension>()
                    .FirstOrDefault();

                Assert.NotNull(ekuExtension);

                Assert.Contains(
                    ekuExtension!.EnhancedKeyUsages.Cast<Oid>(),
                    oid => oid.Value == "1.3.6.1.5.5.7.3.1");
            }

            /// <summary>
            /// Creates the certificate should contain digital signature and key encipherment key usage.
            /// </summary>
            [Fact]
            public void CreateCertificate_ShouldContainDigitalSignatureAndKeyEncipherment()
            {
                // Arrange
                const string password = "test-password";

                // Act
                using var certificate =
                    CertificateProvider.CreateCertificate(password, "localhost");

                // Assert
                var keyUsage = certificate.Extensions
                    .OfType<X509KeyUsageExtension>()
                    .FirstOrDefault();

                Assert.NotNull(keyUsage);

                Assert.True(
                    keyUsage!.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature));

                Assert.True(
                    keyUsage.KeyUsages.HasFlag(X509KeyUsageFlags.KeyEncipherment));
            }

            /// <summary>
            /// Creates the certificate should contain subject alternative name.
            /// </summary>
            [Fact]
            public void CreateCertificate_ShouldContainSubjectAlternativeName()
            {
                // Arrange
                const string password = "test-password";
                const string commonName = "localhost";

                // Act
                using var certificate =
                    CertificateProvider.CreateCertificate(password, commonName);

                // Assert
                var sanExtension = certificate.Extensions
                    .Cast<X509Extension>()
                    .FirstOrDefault(e => e.Oid?.Value == "2.5.29.17");

                Assert.NotNull(sanExtension);
            }

            /// <summary>
            /// Creates the certificate should respect validity period.
            /// </summary>
            [Fact]
            public void CreateCertificate_ShouldRespectValidityPeriod()
            {
                // Arrange
                const string password = "test-password";
                const int years = 2;

                // Act
                using var certificate =
                    CertificateProvider.CreateCertificate(
                        password,
                        "localhost",
                        2048,
                        years);

                // Assert
                var validity =
                    certificate.NotAfter - certificate.NotBefore;

                Assert.True(validity.TotalDays > 365 * years - 10);
            }
        }
    }
}
