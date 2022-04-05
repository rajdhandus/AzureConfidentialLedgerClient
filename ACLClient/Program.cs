using Azure;
using Azure.Security.ConfidentialLedger;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Net.Http.Headers;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Identity;
using System.IO;
using System.Threading;

namespace ACLClient
{
    class Program
    {

        

        /*
         * The pfx file needs to be produced by running the following commands
         * 
         * openssl ecparam -out "privkey_name.pem" -name "secp384r1" -genkey # command to produce private key
         * openssl req -new -key "privkey_name.pem" -x509 -nodes -days 365 -out "cert.pem" -"sha384" -subj=/CN="ACL Client Cert" # command to produce the corresponding Public key
         * openssl pkcs12 -inkey .\privkey_name.pem -in .\cert.pem -export -out clientCertificate.pfx # command to produce the corresponding pfx file which will be used
         * 
         */

        private static string _pfxFileName = @"C:\Users\rapurush\clientCertificate.pfx"; // this should be changed to the target ledger

        private static string _ledgerId = "emily-ledger-march"; // this should be changed to the target ledger // use cert.pem file generated from above command while creating this ledger from portal in Security tab

        private static string _identityServiceEndpoint = "https://identity.confidential-ledger.core.azure.com"; // this is static for ACL 
        private static string _ledgerURI = $"https://{_ledgerId}.confidential-ledger.azure.com";
        private static string _tmpNetworkCertFileName = "networkcert.pem"; // will write this file to the current directory // can be changed as needed

        

        private static bool CertValidationCheck(HttpRequestMessage httpRequestMessage, X509Certificate2 cert, X509Chain x509Chain, SslPolicyErrors sslPolicyErrors)
        {
            
            X509Certificate2 ledgerTlsCert = new X509Certificate2(File.ReadAllBytes(_tmpNetworkCertFileName));

            // Create a certificate chain rooted with our TLS cert. 
            X509Chain certificateChain = new X509Chain();
            certificateChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            certificateChain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
            certificateChain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
            certificateChain.ChainPolicy.VerificationTime = DateTime.Now;
            certificateChain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan(0, 0, 0);
            certificateChain.ChainPolicy.ExtraStore.Add(ledgerTlsCert);
            certificateChain.Build(cert);

            var isCertSignedByTheTlsCert = certificateChain.ChainElements.Cast<X509ChainElement>()
                .Any(x => x.Certificate.Thumbprint == ledgerTlsCert.Thumbprint);

            return isCertSignedByTheTlsCert;
        }

        private static string FetchIdentityCertificate(string _identityServiceEndpoint, string _ledgerId)
        {
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

            return eccPem;
        }
        private static void RawHTTPSClientWrite(StringContent request, HttpClient client)
        {
            try
            {
                // explicitly setting Content-Type to prevent http 415 error // needs to be investigated
                request.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
                HttpResponseMessage dataplaneResponse = client.PostAsync(_ledgerURI + "/app/transactions?api-version=0.1-preview", request).GetAwaiter().GetResult();

                dataplaneResponse.EnsureSuccessStatusCode();

                string responseBody = dataplaneResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                string transactionId = dataplaneResponse.Headers.GetValues("x-ms-ccf-transaction-id").FirstOrDefault();
                string requestId = dataplaneResponse.Headers.GetValues("x-ms-request-id").FirstOrDefault();
                Console.WriteLine(transactionId);
                Console.WriteLine(requestId);
                Console.WriteLine(responseBody);
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine("\nException Caught!");
                Console.WriteLine("Message :{0} ", e.Message);
            }
        }

        private static void RawHTTPSClientGetLatest(HttpClient client, string subLedgerId)
        {
            try
            {
                HttpResponseMessage dataplaneResponse = client.GetAsync(_ledgerURI + $"/app/transactions/current?api-version=0.1-preview&subLedgerId={subLedgerId}").GetAwaiter().GetResult();

                dataplaneResponse.EnsureSuccessStatusCode();

                string responseBody = dataplaneResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                string transactionId = dataplaneResponse.Headers.GetValues("x-ms-ccf-transaction-id").FirstOrDefault();
                string requestId = dataplaneResponse.Headers.GetValues("x-ms-request-id").FirstOrDefault();
                Console.WriteLine(transactionId);
                Console.WriteLine(requestId);
                Console.WriteLine(responseBody);
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine("\nException Caught!");
                Console.WriteLine("Message :{0} ", e.Message);
            }
        }

        private static bool RawHTTPSClientGetDataByCommit(HttpClient client, string subLedgerId, string transactionId)
        {
            try
            {
                HttpResponseMessage dataplaneResponse = client.GetAsync(_ledgerURI + $"/app/transactions/{transactionId}?api-version=0.1-preview&subLedgerId={subLedgerId}").GetAwaiter().GetResult();

                dataplaneResponse.EnsureSuccessStatusCode();

                string responseBody = dataplaneResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                string requestId = dataplaneResponse.Headers.GetValues("x-ms-request-id").FirstOrDefault();
                Console.WriteLine(transactionId);
                Console.WriteLine(requestId);
                Console.WriteLine(responseBody);

                return responseBody.Contains("Ready"); // is the data loaded and Ready ? // if this is Loading - then it means that the client has to retry
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine("\nException Caught!");
                Console.WriteLine("Message :{0} ", e.Message);
                return false;
            }
        }

        private static HttpClient HandlerFactory(HttpClientHandler handler)
        {
            
            handler.ClientCertificateOptions = ClientCertificateOption.Manual;
            handler.SslProtocols = SslProtocols.Tls12;
            handler.ServerCertificateCustomValidationCallback = CertValidationCheck;

            X509Certificate2 certificate = new X509Certificate2(_pfxFileName, string.Empty);
            handler.ClientCertificates.Add(certificate);
            handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;
            handler.PreAuthenticate = true;
            handler.ClientCertificateOptions = ClientCertificateOption.Manual;
            HttpClient client = new HttpClient(handler);

            return client;
        }

        private static void SDKClient(StringContent request, HttpClientHandler handler)
        {
            try
            {
                var options = new ConfidentialLedgerClientOptions { Transport = new HttpClientTransport(handler) };
                var ledgerClient = new ConfidentialLedgerClient(new Uri(_ledgerURI), new DefaultAzureCredential(), options);
                RequestContent requestContent = RequestContent.Create(request);
                var responseForPost = ledgerClient.PostLedgerEntry(requestContent);
                Console.WriteLine(responseForPost.Content);
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine("\nException Caught!");
                Console.WriteLine("Message :{0} ", e.Message);
            }
        }

        static void Main(string[] args)
        {
            string identityCertString = FetchIdentityCertificate(_identityServiceEndpoint, _ledgerId);

            using (StreamWriter writer = System.IO.File.CreateText(_tmpNetworkCertFileName))
            {
                writer.WriteLine(identityCertString);
            }

            var jsonRequest = new { contents = "Hello world" };
            string jsonString = JsonSerializer.Serialize(jsonRequest);
            StringContent request = new StringContent(jsonString);


            var handler = new HttpClientHandler();
            HttpClient client = HandlerFactory(handler);
            RawHTTPSClientWrite(request, client); // write a new entry to the ledger // by default writes to subledger:0

            RawHTTPSClientGetLatest(client, "subledger:0"); // get the last entry in the ledger (subledger is 0)

            while (!RawHTTPSClientGetDataByCommit(client, "subledger:0", "2.181")) // if data is ready exit; or wait until its ready
            {
                Console.WriteLine("Data Not available yet! Will try again.");
                Thread.Sleep(TimeSpan.FromSeconds(5));
            }
            //SDKClient(request, handler); // this is throwing http 403 // we are looking into this!

            handler.Dispose();
            client.Dispose();
        }
    }
}
