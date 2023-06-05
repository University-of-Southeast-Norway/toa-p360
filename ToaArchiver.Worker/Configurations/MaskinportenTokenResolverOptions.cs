using System.Security;

namespace ToaArchiver.Worker.Configurations
{
    public class MaskinportenTokenResolverOptions
    {
        public MaskinportenCertificateOptions Certificate { get; set; }
        public string Audience { get; set; }
        public string TokenEndpoint { get; set; }
        public string Issuer { get; set; }
    }
}
