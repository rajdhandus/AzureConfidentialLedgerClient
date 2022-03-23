using Azure;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Identity;
using Azure.Security.ConfidentialLedger;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace ACLClient
{
    class Program
    {
        static void Main(string[] args)
        {
            string _identityServiceEndpoint = "https://identity.confidential-ledger.core.azure.com";
            string _ledgerId = "testcertauth"; // replace this with your ledger name
            string _ledgerURI = $"https://{_ledgerId}.confidential-ledger.azure.com";


            Uri identityServiceUri = new Uri(_identityServiceEndpoint);
            var identityClient = new ConfidentialLedgerIdentityServiceClient(identityServiceUri);

            // Get the ledger's  TLS certificate for our ledger. 
            string ledgerId = _ledgerId;
            Response response = identityClient.GetLedgerIdentity(ledgerId);

            // extract the ECC PEM value from the response. 
            var eccPem = JsonDocument.Parse(response.Content)
                .RootElement
                .GetProperty("ledgerTlsCertificate")
                .GetString();

            // construct an X509Certificate2 with the ECC PEM value. 
            X509Certificate2 ledgerTlsCert = new X509Certificate2(Encoding.UTF8.GetBytes(eccPem));

            // Create a certificate chain rooted with our TLS cert. 
            X509Chain certificateChain = new X509Chain();
            certificateChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            certificateChain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
            certificateChain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
            certificateChain.ChainPolicy.VerificationTime = DateTime.Now;
            certificateChain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan(0, 0, 0);
            certificateChain.ChainPolicy.ExtraStore.Add(ledgerTlsCert);

            // Define a validation function to ensure that the ledger certificate is trusted by the ledger identity TLS certificate. 
            bool CertValidationCheck(HttpRequestMessage httpRequestMessage, X509Certificate2 cert, X509Chain x509Chain, SslPolicyErrors sslPolicyErrors)
            {
                bool isChainValid = certificateChain.Build(cert);
                if (!isChainValid) return false;

                var isCertSignedByTheTlsCert = certificateChain.ChainElements.Cast<X509ChainElement>()
                    .Any(x => x.Certificate.Thumbprint == ledgerTlsCert.Thumbprint);
                return isCertSignedByTheTlsCert;
            }


            var handler = new HttpClientHandler();
            handler.ClientCertificateOptions = ClientCertificateOption.Manual;
            handler.SslProtocols = SslProtocols.Tls12;
            handler.ServerCertificateCustomValidationCallback = CertValidationCheck;

            /*
             * openssl ecparam -out "privkey_name.pem" -name "secp384r1" -genkey # command to produce private key
             * openssl req -new -key "privkey_name.pem" -x509 -nodes -days 365 -out "cert.pem" -"sha384" -subj=/CN="ACL Client Cert" # command to produce the corresponding Public key
             * 
             */

            // provide private key and public key from above commands;

            string privkeyFilePath = @"C:\Users\rapurush\privkey_name.pem";
            byte[] keyBuffer = File.ReadAllBytes(privkeyFilePath);

            string certFilePath = @"C:\Users\rapurush\cert.pem";
            byte[] certBuffer = File.ReadAllBytes(certFilePath);

            X509Certificate2 certificate = new X509Certificate2(certBuffer, string.Empty);

            handler.ClientCertificates.Add(certificate);

            var options = new ConfidentialLedgerClientOptions { Transport = new HttpClientTransport(handler) };

            var ledgerClient = new ConfidentialLedgerClient(new Uri(_ledgerURI), new DefaultAzureCredential(), options);
            RequestContent requestContent = RequestContent.Create(new { contents = "Hello world!" });
            var responseForPost = ledgerClient.PostLedgerEntry(requestContent);

        }
    }
}
