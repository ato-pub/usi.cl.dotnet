using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.IdentityModel.Tokens;
using System.Net;
using System.Reflection;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Security;
using Abr.AuskeyManager.KeyStore;
using System.IdentityModel.Protocols.WSTrust;
using System.Linq;
using System.ServiceModel.Configuration;
using USISampleCode.UsiCreateServiceReference;

namespace USISampleCode
{
    static class ServiceChannel
    {
        // Name of the /configuration/system.servicemodel/client/endpoint configuration used in
        // the App.config to configure the ChannelFactory for the USI service.
        const string EndpointConfigurationName = "WS2007FederationHttpBinding_IUSIService";

        //Obviously these two variables should be in a config file. Included here so it's easier to see how this code works.
        private const string ClientCertificateKeystoreLocation = "keystore-usi.xml"; // @"C:\Developer\Userdata\keystore.xml";
        const string Alias = "ABRD:27809366375_USIMachine"; // Old one: "ABRD:12300000059_TestDevice03";

        //*** This should be stored in encrypted form. See notes in GetPasswordString().
        const string Password = "Password1!";

        private static ChannelFactory<IUSIService> _channelFactory;

        public static IUSIService OpenWithM2M()
        {
            SecurityToken token = GetStsToken(tokenLifeTimeMinutes: 60);

            var clientSection = (ClientSection) ConfigurationManager.GetSection("system.serviceModel/client");
            var endpointElement = clientSection.Endpoints.Cast<ChannelEndpointElement>().First(endpoint =>
                "UsiCreateServiceReference.IUSIService".Equals(endpoint.Contract, StringComparison.InvariantCultureIgnoreCase));
            if (endpointElement == null)
                throw new Exception("No endpoint matching service contract was found");

            var channelFactory = new ChannelFactory<IUSIService>(endpointElement.Name);
            channelFactory.Open();

            return channelFactory.CreateChannelWithIssuedToken(token);
        }

        private static SecurityToken GetStsToken(int tokenLifeTimeMinutes)
        {
            WS2007HttpBinding binding = new WS2007HttpBinding();
            binding.Security.Message.ClientCredentialType = MessageCredentialType.Certificate;
            binding.Security.Mode = SecurityMode.TransportWithMessageCredential;
            binding.Security.Message.EstablishSecurityContext = false;
            binding.Security.Message.AlgorithmSuite = SecurityAlgorithmSuite.Basic256Sha256;
            string endPoint = "S007SecurityTokenServiceEndpointV3";

            var factory = new WSTrustChannelFactory(endPoint);
            factory.Credentials.ClientCertificate.Certificate = GetClientCertificateFromKeystore();

            // Instantiate and invoke the client to get the security token
            factory.Credentials.SupportInteractive = false;

            var appliesTo = ConfigurationManager.AppSettings["appliesTo"];
            var rst = new RequestSecurityToken
            {
                Claims = { new RequestClaim("http://vanguard.ebusiness.gov.au/2008/06/identity/claims/abn", false),
                    new RequestClaim("http://vanguard.ebusiness.gov.au/2008/06/identity/claims/credentialtype", false) },
                AppliesTo = new EndpointReference(appliesTo),
                Lifetime = new Lifetime(DateTime.UtcNow, DateTime.UtcNow.AddMinutes(tokenLifeTimeMinutes)),
                RequestType = RequestTypes.Issue,
                KeyType = KeyTypes.Symmetric,
                TokenType = "http://docs.oasis-open.org/wss/oasis-wss-saml-token-profile-1.1#SAMLV1.1"
            };

            // Instantiate and invoke the client to get the security token
            var client = (WSTrustChannel)factory.CreateChannel();

            SecurityToken response = client.Issue(rst);

            return response;
        }

        internal static X509Certificate2 GetClientCertificateFromKeystore()
        {
            //Please replace [YourCompanyName] tag with the valid organisation Name
            AbrProperties.SetSoftwareInfo("[OrganisationName]", Assembly.GetEntryAssembly().GetName().Name, 
                Assembly.GetEntryAssembly().GetName().Version.ToString(), DateTime.Now.ToString(CultureInfo.InvariantCulture));

            var keyStore = new AbrKeyStore(File.OpenRead(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ClientCertificateKeystoreLocation)));

            using (var pwd = GetPasswordString())
            {
                var abrCredential = keyStore.GetCredential(Alias);
                 
                if (!abrCredential.IsReadyForRenewal())
                {
                    X509Certificate2 clientCertificate = abrCredential.PrivateKey(pwd, X509KeyStorageFlags.MachineKeySet);
                    return clientCertificate;
                }
                else
                {
                    //throw new Exception("Renew certificate");
                    X509Certificate2 clientCertificate = abrCredential.PrivateKey(pwd, X509KeyStorageFlags.MachineKeySet);
                    return clientCertificate;
                }                
            }
        }

        private static SecureString GetPasswordString()
        {
            //NOTE: This code is for demonstration purposes only.
            //      Production code should obtain the password SecureString instance from an encrypted source,
            //      and it should never be held in a plain String object.
            //      Read MSDN remarks about why to avoid the password being stored in a plain String object: 
            //      http://msdn.microsoft.com/en-us/library/system.security.securestring%28v=vs.110%29.aspx
            var pwd = new SecureString();
            foreach (var c in Password)
                pwd.AppendChar(c);

            return pwd;
        }

        public static void Close()
        {
            if (_channelFactory != null)
                _channelFactory.Close();

            _channelFactory = null;
        }
    }
}


