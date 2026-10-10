using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace GolfSG.Infrastructure.Products;

public static class LocalDevelopmentCertificate
{
    public static bool IsAccepted(Uri? requestUri, X509Certificate2? certificate, SslPolicyErrors errors)
    {
        if (errors == SslPolicyErrors.None) return true;

        // Used only by DEBUG client setup. Keep exceptions restricted to local ASP.NET development certificates.
        return requestUri is { Scheme: "https", Host: "localhost" or "127.0.0.1" or "[::1]" or "10.0.2.2" }
            && certificate is not null
            && certificate.Subject == "CN=localhost"
            && certificate.Issuer == "CN=localhost"
            && certificate.NotBefore.ToUniversalTime() <= DateTime.UtcNow
            && certificate.NotAfter.ToUniversalTime() >= DateTime.UtcNow
            && certificate.Extensions.Any(e => e.Oid?.Value == "1.3.6.1.4.1.311.84.1.1")
            && (errors & SslPolicyErrors.RemoteCertificateNotAvailable) == 0;
    }
}
