namespace MarcusRunge.Toolbox.Test
{
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using Xunit;

    namespace MarcusRunge.Toolbox.Test
    {
        public class CertificateProviderTest
        {
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
