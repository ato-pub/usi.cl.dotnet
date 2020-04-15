using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using System.Text;
using USISampleCode.UsiCreateServiceReference;

namespace USISampleCode
{
    class Program
    {
        static void Main(string[] args)
        {
            // Set callback handler to validate a server certificate.
            ServicePointManager.ServerCertificateValidationCallback += new RemoteCertificateValidationCallback(CustomSSLCertificateValidation);

            string option = null;
            string output = null;

            while (true)
            {
               
                if (option == null)
                {
                    option = ShowUsage();
                }
                else if (option.ToUpper() == "/C")
                {
                    output = PerformCreateAndVerifyUSI();
                    Console.WriteLine(output);
                    option = ShowUsage();
                }
                else if (option.ToUpper() == "/B")
                {
                    output = PerformBulkUpload();
                    Console.WriteLine(output);
                    option = ShowUsage();
                }
                else if (option.ToUpper() == "/R")
                {
                    Console.WriteLine("Enter the receipt number:");
                    string receiptNumber = Console.ReadLine();

                    if (!String.IsNullOrEmpty(receiptNumber))
                    {
                        output = PerformBulkUploadRetrieve(receiptNumber);
                        Console.WriteLine(output);
                        option = ShowUsage();
                    }
                    else
                    {
                        option = ShowUsage();
                    }
                }
                else if (option.ToUpper() == "/V")
                {
                    output = PerformBulkVerify();
                    Console.WriteLine(output);
                    option = ShowUsage();
                }
                else if (option.ToUpper() == "/UC")
                {
                    output = UpdateContactDetails();
                    Console.WriteLine(output);
                    option = ShowUsage();
                }
                else if (option.ToUpper() == "/G")
                {
                    output = GetNonDvsDocumentTypes();
                    Console.WriteLine(output);
                    option = ShowUsage();
                }
                else if (option.ToUpper() == "/E")
                {
                    break;
                }
                else
                {
                    break;
                }
            }
        }

        private static string UpdateContactDetails()
        {
            IUSIService client = null;
            string result = null;
            StringBuilder sb = new StringBuilder();
            try
            {
                Console.WriteLine("Please enter a USI to update (you must have permission):");

                string usi = Console.ReadLine();

                if (string.IsNullOrEmpty(usi))
                {
                    result = "Could not read the USI";
                    return result;
                }

                usi = usi.Trim();

                if (usi.Length != 10)
                {
                    result = "USI should be 10 characters long";
                    return result;
                }

                //create a request to get nonDvsDocumentTypes
                var request = RequestFactory.CreateUpdateContactDetailsRequest("0002", usi);

                // Open a channel to USI service.
                client = ServiceChannel.OpenWithM2M();
                
                UpdateUSIContactDetailsResponse response;

                try
                {
                    response = client.UpdateUSIContactDetails(request);
                }
                catch (FaultException<ErrorInfo> ex)
                {
                    sb.AppendLine("Get Non Dvs Documents returned a FaultException");
                    sb.AppendLine(string.Format("Detail: {0}", ex.Detail.Message));
                    result = sb.ToString();
                    return result;
                }

                if (response.UpdateUSIContactDetailsResponse1.Result == UpdateUSIContactDetailsResponseTypeResult.Failure)
                {
                    var message = " " + String.Join(". ",response.UpdateUSIContactDetailsResponse1.Errors.Select(e => e.Message));
                    Console.WriteLine(string.Format("Update contact details Failed. Reason: {0}",message));
                    return message.ToString();

                }

                Console.WriteLine("Successfully updated contact details");
                
            }
            finally
            {
                if (client != null)
                    ((ICommunicationObject)client).Close();

                ServiceChannel.Close();
            }

            return result;
        }

        private static string GetNonDvsDocumentTypes()
        {
            IUSIService client = null;
            string result = null;
            StringBuilder sb = new StringBuilder();
            try
            {
                //create a request to get nonDvsDocumentTypes
                var request = RequestFactory.CreateGetNonDvsDocumentRequest("0002");

                GetNonDvsDocumentTypesResponse response;

                try
                {
                    // Open a channel to the USI service.
                    response = ServiceChannel.OpenWithM2M().GetNonDvsDocumentTypes(request);
                }
                catch (FaultException<ErrorInfo> ex)
                {                 
                    sb.Append("Get Non Dvs Documents returned a FaultException").AppendLine(string.Format("Detail: {0}", ex.Detail.Message));
                    result = sb.ToString();
                    return result;
                }

                var responseStrings = response.GetNonDvsDocumentTypesResponse1.NonDvsDocumentTypes;
                Console.WriteLine("The following non dvs document types were returned;" + Environment.NewLine);
                foreach (var nonDvsDocumentTypeType in responseStrings)
                {
                    sb.AppendLine(string.Format("Id:{0} Type:{1} Sort Order:{2}", nonDvsDocumentTypeType.Id, nonDvsDocumentTypeType.DocumentType, nonDvsDocumentTypeType.SortOrder));
                }
                
            }
            finally
            {
                if (client != null)
                    ((ICommunicationObject)client).Close();

                ServiceChannel.Close();
            }
            result = sb.ToString();
            return result;
        }

        static bool CustomSSLCertificateValidation(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors error)
        {
#if DEBUG
            //IMPORTANT - This should not be used in production code.
            //The purpose of this is to allow the use of a mismatching SSL certificate, 
            //which is not expected to be required in third party or production.
            //This setting may create a "Man in the Middle" vulnerability.
            return false == error.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable);
#else
            //This is the safe version of the above code.
            return error == SslPolicyErrors.None;
#endif

        } 

        static string DecodeBulkUploadRetrievalResponse(ApplicationResponseType response)
        {
            string message = String.Empty;

            // Determine overall success or failure of the call.
            switch (response.Result)
            {
                case ApplicationResponseTypeResult.Success:
                    message = String.Format("Application {0} succeeded with USI {1}.", response.ApplicationId, response.USI);
                    break;

                case ApplicationResponseTypeResult.MatchFound:
                    message = String.Format("Application {0} already exists.", response.ApplicationId);
                    break;

                case ApplicationResponseTypeResult.Failure:
                    message = String.Format("Application {0} failed.", response.ApplicationId);
                    break;
            }

            if (response.USI != null)
            {
                message += String.Format(" USI={0}.", response.USI);
            }
            else
            {
                message += " USI is null.";
            }

            message += String.Format(" IdentityDocumentVerified={0}.", response.IdentityDocumentVerified);

            // Get the detailed messages if the call failed.
            if (response.Errors != null && response.Errors.Length > 0)
                message += " " + String.Join(". ", response.Errors.Select(e => e.Message));

            return message;
        }

        private static string DecodeBulkVerifyResponse(VerificationResponseType response)
        {
            string message = String.Format("RecordId={0}\nUSI={1}\nUSIStatus={2}\n", response.RecordId, response.USI, response.USIStatus);

            for (int i = 0; i < response.Items.Count(); i++)
            {
                message += String.Format("{0}={1}\n", response.ItemsElementName[i], response.Items[i]);
            }

            message += String.Format("DateOfBirth={0}\n", response.DateOfBirth);

            return message;
        }

        static string PerformBulkUpload()
        {
            IUSIService client = null;
            string result = null;
            StringBuilder sb = new StringBuilder();
            try
            {
                // Create an array of applications.
                var request = RequestFactory.CreateBulkUploadRequest(new ApplicationType[] 
                {
                    // 09xxxxxxxx is a valid but non-existent phone number. http://australia.gov.au/about-australia/our-country/telephone-country-and-area-codes
                    // usi.sample.code@gmail.com is a registered but unused email address. Gmail ignores a plus sign and anything following it.
                    RequestFactory.CreateApplication("Johnny", "Smithy", new DateTime(1980, 01, 02), PersonalDetailsTypeGender.M, "usi.sample.code+bulk11f@gmail.com", "0900000004", "5 Johnny Street", "4013", StateListType.QLD, "NORTHGATE", RequestFactory.BirthCert()),
                    RequestFactory.CreateApplication("Lucy", "Kockhe", new DateTime(1985, 03, 02), PersonalDetailsTypeGender.F, "usi.sample.code+bulk31f@gmail.com", "0900000004", "5 Luce Street", "4013", StateListType.QLD, "NORTHGATE", RequestFactory.Citizenship()),
                    RequestFactory.CreateApplication("Nichloas", "Koke", new DateTime(1990, 07, 02), PersonalDetailsTypeGender.M, "usi.sample.code+bulk41f@gmail.com", "0900000004", "5 Nice Street", "4013", StateListType.QLD, "NORTHGATE", RequestFactory.Descent()),
                    RequestFactory.CreateApplication("Bobby", "Lashley", new DateTime(1977, 09, 02), PersonalDetailsTypeGender.M, "usi.sample.code+bulk101f@gmail.com", "0900000004", "5 France Street", "4013", StateListType.QLD, "NORTHGATE", RequestFactory.DriversLicence()),
                    RequestFactory.CreateApplication("Clooney", "Amal", new DateTime(1981, 02, 02), PersonalDetailsTypeGender.F, "usi.sample.code+bulk201f@gmail.com", "0900000004", "5 Liz Street", "4013", StateListType.QLD, "NORTHGATE", RequestFactory.Medicare("Lisa Smith7f")),
                    RequestFactory.CreateApplication("Greg", "Clooney", new DateTime(1981, 09, 02), PersonalDetailsTypeGender.F, "usi.sample.code+bulk301m@gmail.com", "0900000004", "5 Ae Street", "4013", StateListType.QLD, "NORTHGATE", RequestFactory.Passport()),
                    RequestFactory.CreateApplication("Tom", "Cruise", new DateTime(1991, 04, 02), PersonalDetailsTypeGender.F, "usi.sample.code+bulk401m@gmail.com", "0900000004", "5 May Street", "4013", StateListType.QLD, "NORTHGATE", RequestFactory.Visa())
                });

                // Open a channel to USI service.
                client = ServiceChannel.OpenWithM2M();

                // Make the USI service call.
                BulkUploadResponse response;
                try
                {
                    response = client.BulkUpload(request);
                }
                catch (FaultException<ErrorInfo> ex)
                {
                    sb.AppendLine("BulkUpload returned a FaultException");
                    sb.AppendLine(string.Format("Detail: {0}", ex.Detail.Message));
                    result = sb.ToString();

                    return result;
                }

                result = String.Format("Succeeded with receipt number {0}", response.BulkUploadResponse1.ReceiptNumber);
            }
            finally
            {
                if (client != null)
                    ((ICommunicationObject)client).Close();

                ServiceChannel.Close();
            }
            return result;
        }

        static string PerformBulkUploadRetrieve(string receiptNumber)
        {
            IUSIService client = null;
            string result = null;
            StringBuilder sb = new StringBuilder();
            try
            {
                // Open a channel to USI service.
                client = ServiceChannel.OpenWithM2M();

                // Create a BulkUploadRetrieveRequest using the supplied receipt number.
                var request = new BulkUploadRetrieveType() { ReceiptNumber = receiptNumber };
                var wrappedRequest = new BulkUploadRetrieveRequest(request);

                // Make the USI service call.
                BulkUploadRetrieveResponse response;
                try
                {
                    response = client.BulkUploadRetrieve(wrappedRequest);
                }
                catch (FaultException<ErrorInfo> ex)
                {
                    sb.AppendLine("BulkUploadRetrieve returned a FaultException");
                    sb.AppendLine(string.Format("Detail: {0}", ex.Detail.Message));
                    result = sb.ToString();
                    return result;
                }

                // Decode the response messages and display.
                var appResponseStrings = response.BulkUploadRetrieveResponse1.Applications.Select(DecodeBulkUploadRetrievalResponse);
                string linebreak = String.Format("{0}{0}", Environment.NewLine);
                string messages = String.Join(linebreak, appResponseStrings);

                result = messages;
            }
            finally
            {
                if (client != null)
                    ((ICommunicationObject)client).Close();

                ServiceChannel.Close();
            }
            return result;
        }

        private static VerificationType[] Get500()
        {
            List<VerificationType> items = new List<VerificationType>();

            for (int i = 0; i < 500; i++)
            {
                items.Add(RequestFactory.CreateVerification(i + 1, "DUX9A3FJR6", "Johnfgfgfjdjdjdjdjdjdjdjdjdjdjdjdjdjdjd", "Smitdefdsjdjdjdjdjdjdjdjdjdjdjdjdjdjdjd",
                                                            new DateTime(1980, 01, 21)));
            }

            return items.ToArray();
        }

        static string PerformBulkVerify()
        {
            IUSIService client = null;
            string result = null;
            StringBuilder sb = new StringBuilder();
            try
            {
                // Create an array of applications.
                var request = RequestFactory.CreateBulkVerifyRequest(new VerificationType[] 
                {
                    RequestFactory.CreateVerification(1, "C2P5P4UBHP", "Nicholas", "Koke", new DateTime(1990, 07, 02)),
                    RequestFactory.CreateVerification(2, "QS5Q8XWSUJ", "Annie", "Angle", new DateTime(1981, 09, 02)), 
                    RequestFactory.CreateVerification(3, "9AKTUJMMAZ", "Lucy", "Smithcd", new DateTime(1985, 03, 03)), 
                    RequestFactory.CreateVerification(4, "BJRVU7U59N", "Nick", "Smithdd", new DateTime(1990, 07, 14)), 
                    RequestFactory.CreateVerification(5, "VL8CYKH3ND", "Adam", "Smithed", new DateTime(1977, 09, 07)), 
                    RequestFactory.CreateVerification(6, "6N69KBFUDZ", "Paul", "Smithfd", new DateTime(1982, 12, 06)), 
                    RequestFactory.CreateVerification(7, "N7UEE7FWKV", "Lisa", "Smithgd", new DateTime(1981, 02, 19)), 
                    RequestFactory.CreateVerification(8, "A88H9D64CS", "Anne", "Smithhd", new DateTime(1981, 09, 22)), 
                    RequestFactory.CreateVerification(9, "GBYDD3ZLVN", "Mary", "Smithid", new DateTime(1991, 04, 26))
                });

                // or use this to create a large request.
                //var request = RequestFactory.CreateBulkVerifyRequest(Get500());

                // Open a channel to USI service.
                client = ServiceChannel.OpenWithM2M();

                BulkVerifyUSIResponse response;
                try
                {
                    response = client.BulkVerifyUSI(request);
                }
                catch (FaultException<ErrorInfo> ex)
                {
                    sb.AppendLine("BulkVerifyUSI returned a FaultException");
                    sb.AppendLine(string.Format("Detail: {0}", ex.Detail.Message));
                    result = sb.ToString();
                    return result;
                }

                var responseStrings = response.BulkVerifyUSIResponse1.VerificationResponses.Select(DecodeBulkVerifyResponse);
                string linebreak = String.Format("{0}{0}", Environment.NewLine);
                string messages = String.Join(linebreak, responseStrings);

                result = messages;
            }
            finally
            {
                if (client != null)
                    ((ICommunicationObject)client).Close();

                ServiceChannel.Close();
            }
            return result;
        }

        private static string PerformCreateUsi()
        {
            IUSIService client = null;
           
                // Create an application.
                // See explanation at PerformBulkUpload, above.
                var createRequest = RequestFactory.CreateUSIRequest(RequestFactory.CreateApplication(
                    "Janeee", "Smithhh", new DateTime(1980, 01, 06), PersonalDetailsTypeGender.F, "usi.sample.code+single1136962@gmail.com", "0900000006", 
                    "6 James Street", "2601", StateListType.ACT, "Canberra", RequestFactory.BirthCert()));

                // Open a channel to USI service.
                client = ServiceChannel.OpenWithM2M();

                // Make the USI service call.
                CreateUSIResponse createResponse;
                try
                {
                    createResponse = client.CreateUSI(createRequest);
                }
                catch (FaultException<ErrorInfo> ex)
                {
                    Console.WriteLine("CreateUSI returned a FaultException");
                    Console.WriteLine("Detail: {0}", ex.Detail.Message);
                    return null;
                }
                finally
                {
                    if (client != null)
                        ((ICommunicationObject)client).Close();

                    ServiceChannel.Close();
                }
                // Note: if the response is "MatchFound", it means the application was rejected based on being a duplicate (try changing the personal details above)
                Console.WriteLine("Application submitted with result: {0} Usi: {1}", createResponse.CreateUSIResponse1.Application.Result, createResponse.CreateUSIResponse1.Application.USI);

                // Write out any errors that may have occurred during the request.
                if (createResponse.CreateUSIResponse1.Application.Errors != null && createResponse.CreateUSIResponse1.Application.Errors.Length > 0)
                    Console.WriteLine(string.Join(Environment.NewLine, createResponse.CreateUSIResponse1.Application.Errors.Select(e => e.Message)));

                return createResponse.CreateUSIResponse1.Application.USI;
            
        }

        static string PerformCreateAndVerifyUSI()
        {
            IUSIService client = null;
            string result = null;
            StringBuilder sb = new StringBuilder();
            try
            {
                // Create an application.
                // See explanation at PerformBulkUpload, above.
                var createRequest = RequestFactory.CreateUSIRequest(RequestFactory.CreateApplication(
                    "App", "Rohas", new DateTime(1977, 06, 06), PersonalDetailsTypeGender.F, "usi.sample.code+single18102020@gmail.com", "0400000013",
                    "62 Butter Street", "2600", StateListType.ACT, "Canberra", RequestFactory.BirthCert()));

                // Open a channel to USI service.
                client = ServiceChannel.OpenWithM2M();

                // Make the USI service call.
                CreateUSIResponse createResponse;
                try
                {
                    createResponse = client.CreateUSI(createRequest);
                }
                catch (FaultException<ErrorInfo[]> ex)
                {
                    sb.AppendLine("CreateUSI returned a FaultException");
                    sb.AppendLine(string.Format("Detail: {0} {1}Code: {2}", ex.Detail.First().Message, Environment.NewLine, ex.Detail.First().Code));
                    result = sb.ToString();

                    return result;
                }
                catch (FaultException<ErrorInfo> ex)
                {
                    sb.AppendLine("CreateUSI returned a FaultException");
                    sb.AppendLine(string.Format("Detail: {0}", ex.Detail.Message));
                    result = sb.ToString();
                    return result;
                }
                // Note: if the response is "MatchFound", it means the application was rejected based on being a duplicate (try changing the personal details above)
                Console.WriteLine("Application submitted with result: {0}", createResponse.CreateUSIResponse1.Application.Result);

                // Write out any errors that may have occurred during the request.
                if (createResponse.CreateUSIResponse1.Application.Errors != null && createResponse.CreateUSIResponse1.Application.Errors.Length > 0)
                    Console.WriteLine(string.Join(Environment.NewLine, createResponse.CreateUSIResponse1.Application.Errors.Select(e => e.Message)));

                if (createResponse.CreateUSIResponse1.Application.Result != ApplicationResponseTypeResult.Failure)
                {

                    // Create a VerifyUSIRequest to verify the previous call.
                    var verifyRequest = new VerifyUSIType
                    {
                        OrgCode = createRequest.CreateUSI.OrgCode,
                        USI = createResponse.CreateUSIResponse1.Application.USI,
                        Items = createRequest.CreateUSI.Application.PersonalDetails.Items,
                        ItemsElementName = createRequest.CreateUSI.Application.PersonalDetails.ItemsElementName.Select(itemsChoice => Translate(itemsChoice)).ToArray(),
                        DateOfBirth = createRequest.CreateUSI.Application.PersonalDetails.DateOfBirth
                    };

                    var wrappedVerifyRequest = new VerifyUSIRequest(verifyRequest);

                    // Make the USI service call.
                    VerifyUSIResponse verifyResponse;
                    try
                    {
                        verifyResponse = client.VerifyUSI(wrappedVerifyRequest);
                    }
                    catch (FaultException<ErrorInfo> ex)
                    {
                        sb.AppendLine("VerifyUSIResponse returned a FaultException");
                        sb.AppendLine(string.Format("Detail: {0}", ex.Detail.Message));
                        result = sb.ToString();

                        return result;
                    }
                    result = string.Format("USI {0} verified with status {1}", verifyRequest.USI, verifyResponse.VerifyUSIResponse1.USIStatus.ToString());
                }
                else
                {
                    result = "Cannot make Verify call for an unsuccessful USI creation.";
                }
            }
            finally
            {
                if (client != null)
                    ((ICommunicationObject)client).Close();

                ServiceChannel.Close();
            }

            return result;
        }

        static string ShowUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("{0} <option>", Assembly.GetExecutingAssembly().GetName().Name);
            Console.WriteLine("  /c                - Calls CreateUSI and VerifyUSI");
            Console.WriteLine("  /b                - Calls BulkUpload returning a ReceiptNumber");
            Console.WriteLine("  /r ReceiptNumber  - Calls BulkUploadRetreive using ReceiptNumber");
            Console.WriteLine("  /v                - Calls BulkVerify");
            Console.WriteLine("  /uc               - Calls Update Contact Details");
            Console.WriteLine("  /g                - Calls GetNonDvsDocuments");
            Console.WriteLine("  /e                - Exit");
            Console.WriteLine("Enter your choice and press enter.");
            string input = Console.ReadLine();
            return input;
        }

        static ItemsChoiceType3 Translate(ItemsChoiceType choice)
        {
            return (ItemsChoiceType3)Enum.Parse(typeof(ItemsChoiceType3), choice.ToString());
        }

        static void UseLatestTLS()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls12;
        }
    }
}

