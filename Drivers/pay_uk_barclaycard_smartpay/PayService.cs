using Acrelec.Library.Logger;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UK_BARCLAYCARD_SMARTPAY.Communicator;
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UK_BARCLAYCARD_SMARTPAY.Model;
using System.ServiceProcess;
using System.Linq;
using System.Net.Sockets;
using System.Xml.Linq;
using System.Xml;
using System.Web;

namespace UK_BARCLAYCARD_SMARTPAY
{
    public class PayService : ICommunicatorCallbacks
    {
        public const string PAY_SERVICE_LOG = "Pay_Service";

        /// <summary>
        /// Object that is used for the communication with the Core Payment Driver
        /// </summary>
        private CoreCommunicator coreCommunicator;
          
        /// <summary>
        ///// Property that will give access to the callback methods
        /// </summary>
        private ICommunicatorCallbacks CommunicatorCallbacks { get; set; }

        /// <summary>
        /// Flag that will be used to prevent 2 or more callback methods simultaneous execution
        /// </summary>
        public bool IsCallbackMethodExecuting;

        /// <summary>
        /// Duration of payment in seconds
        /// </summary>
        private int paymentDuration;

        /// <summary>
        /// Flag based on which the result of the "Cancel" will be given
        /// </summary>
        private bool isPaymentCancelSuccessful;

        /// <summary>
        /// Flag based on which the result of the "Cancel" will be given
        /// </summary>
        private bool isPaymentExecuteCommandSuccessful;

        /// <summary>
        /// option that will control the result of the payment.
        /// 0 - always succesfull
        /// 1 - always failure
        /// </summary>
        private string paymentResult;

        /// <summary>
        /// The type of the credit card that was used for the payment
        /// </summary>
        private string paymentTenderMediaID;

        /// <summary>
        /// The full path to the ticket.
        /// The ticket will be created and updated after each successful payment
        /// </summary>
        private string ticketPath;

        /// <summary>
        /// Flag which will be used to identify if the cancel was triggered
        /// </summary>
        private bool wasCancelTigged;

        /// <summary>
        /// The driver input details
        /// </summary>
        private int kioskNumber;
        private string comPort;
        private string output =  @".\output\";
        private int port;
        private int country;
        private int currency;
        private string sourceId;
        private string serviceName;

        private bool solveServiceExist;
        private bool isSolveServiceRunning;

        private string[] fileContents;

        public PayService(CoreCommunicator coreCommunicator)
        {

            ticketPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "ticket");

            //init the core communicator
            this.coreCommunicator = coreCommunicator;

            // Hook the callback methods of the communicator to the ones of current class
            coreCommunicator.CommunicatorCallbacks = this;
        }

        /// <summary>
        /// Callback method that is triggered when the init message is received from the Core
        /// </summary>
        /// <param name="parameters"></param>
        public void InitRequest(object parameters)
        {
            Task.Run(() => { Init(parameters); });
        }

        /// <summary>
        /// Method will be executed in a separate thread that will execute and 
        /// Echo and Paring of the device
        /// </summary>
        /// <param name="parameters">
        /// Example : {
        ///            {
        ///                PaymentDuration = 10
        ///                PaymentResult = 2
        ///                TenderMedia = Visa
        ///             }
        ///           }
        /// </param>
        private void Init(object parameters)
        {
            Log.Info(PAY_SERVICE_LOG, "call Initialize");

            Log.Info(PAY_SERVICE_LOG, $"        Init(): parameters:\n{parameters.ToString()}.");

            //Check if another method is executing
            if (IsCallbackMethodExecuting)
            {
                coreCommunicator.SendMessage(CommunicatorMethods.Init, new { Status = 1 });

                Log.Info(PAY_SERVICE_LOG, "        Init(): another method is executing.");

                Log.Info(PAY_SERVICE_LOG, "endcall Initialize");

                return;
            }
            else
                IsCallbackMethodExecuting = true;



            //Get the needed parameters to make a connection
            if (!GetInitParameters(parameters.ToString()))
            {
                coreCommunicator.SendMessage(CommunicatorMethods.Init, new { Status = 1 });

                Log.Info(PAY_SERVICE_LOG, "        Init(): failed to deserialize the init parameters.");
               
            }
            else
            {
                // If paymentDuration received is less than 12 seconds we set at default value 12 so we can send progress messages
                if (paymentDuration < 12)
                    paymentDuration = 12;

                coreCommunicator.SendMessage(CommunicatorMethods.Init, new { Status = 0 });
                Log.Info(PAY_SERVICE_LOG, "        Init(): success.");
            }

            try
            {

                if (string.IsNullOrEmpty(comPort))
                {
                    Log.Error(PAY_SERVICE_LOG, $"        Init(): Invalid comPort value:  {comPort}.");
                    return;
                }

                // check SmartSwitch_Public.dfn is available
                // Checking the existence of the specified

                // check comport set correctly
                string comFilePath = @"C:\Barclaycard\SolveConnect\SmartSwitch_Public.dfn";
               

                if (File.Exists(comFilePath))
                {
                    Log.Info(PAY_SERVICE_LOG, $"        Init(): {comFilePath} exists.");
                    //extract the contents
                    fileContents = File.ReadAllLines(comFilePath);
                  
                }
                else
                {
                    Log.Error(PAY_SERVICE_LOG, $"        Init(): {comFilePath}  does not exist.");
                    return;
                }
                
                bool comPortflag =  false;

                Log.Info(PAY_SERVICE_LOG, $"        Init(): comPort value: {comPort}.");

                foreach (string line in fileContents)
                {

                    if (line.Contains(comPort))
                    {
                        comPortflag = true;
                    }

                }

                if (comPortflag ==  true)
                {

                    Log.Info($"        Init():  ComPort matches the Setting in SmartSwitch_Public.dfn : {comPort}.");
                }
                else
                {
                    Log.Error($"        Init(): Invalid ComPort: {comPort}.");
                    return;
                }


                if (port <=0)
                {
                    Log.Error(PAY_SERVICE_LOG, $"        Init(): Invalid port:  {port}.");
                    return;
                }
                if (kioskNumber <= 0)
                {
                    Log.Error(PAY_SERVICE_LOG, $"        Init():: Invalid kioskNumber: {kioskNumber}.");
                    return;
                }
                if (country <= 0)
                {
                    Log.Error(PAY_SERVICE_LOG, $"        Init(): Invalid country:  {country}.");
                    return;
                }
                if (currency <= 0)
                {
                    Log.Error(PAY_SERVICE_LOG, $"        Init(): Invalid currency:  {currency}.");
                    return;
                }
          
                if (string.IsNullOrEmpty(sourceId))
                {
                    Log.Error(PAY_SERVICE_LOG, $"        Init(): Invalid SourceId value:  {sourceId}.");
                    return;
                }

                if (string.IsNullOrEmpty(serviceName))
                {
                    Log.Error(PAY_SERVICE_LOG, $"        Init():: Invalid Service Name:  {serviceName}.");
                    return;
                }

                //check the Solveconnect Service exists
                solveServiceExist = ServiceExists(serviceName);

                if (solveServiceExist == true)
                {
                    Log.Info(PAY_SERVICE_LOG, $"        Init(): SolveConnect service exists.");

                    // so now check if Service is running
                    isSolveServiceRunning = ServiceIsRunning(serviceName);
                    if (isSolveServiceRunning != true)
                    {
                        Log.Error(PAY_SERVICE_LOG, $"        Init(): {serviceName} service is not running.");
                        return;
                    }
                    else
                    {
                        Log.Info(PAY_SERVICE_LOG, $"        Init(): {serviceName} service is running.");
                    }

                }
                else {
                    Log.Error(PAY_SERVICE_LOG, $"        Init(): SolveConnect service doesn't exist.");
                    return;
                }

                // pass the items to the API 
                using (var api = new BarclayCardSmartpayApi(currency, country, port, sourceId, kioskNumber))             
                {
                    Log.Info(PAY_SERVICE_LOG, "        Init(): Passing items to the API !");
                }
            }
            catch (Exception ex)
            {
                Log.Error(PAY_SERVICE_LOG, "        Init(): " + ex.Message);
                return;
            }
            finally
            {
                Log.Info(PAY_SERVICE_LOG, "        Init(): Init method finished.");
            }

            IsCallbackMethodExecuting = false;

            Log.Info(PAY_SERVICE_LOG, "endcall Initialize");
        }

        /// <summary>
        /// Callback method that is triggered when the test message is received from the Core
        /// </summary>
        /// <param name="parameters"></param>
        public void TestRequest(object parameters)
        {
            Task.Run(() => { Test(parameters); });
        }

        /// <summary>
        /// Method will be executed in a separate thread and will send Echo command and analyze the response 
        /// </summary>
        /// <param name="parameters"></param>
        public void Test(object parameters)
        {
            Log.Info(PAY_SERVICE_LOG, "call Test");

            //Check if another method is executing
            if (IsCallbackMethodExecuting)
            {
                coreCommunicator.SendMessage(CommunicatorMethods.Test, new { Status = 1 });

                Log.Info(PAY_SERVICE_LOG, "        Test(): another method is executing.");

                Log.Info(PAY_SERVICE_LOG, "endcall Test");

                return;
            }
            else
                IsCallbackMethodExecuting = true;

            coreCommunicator.SendMessage(CommunicatorMethods.Test, new { Status = 0 });
            Log.Info(PAY_SERVICE_LOG, "        Test(): success.");

            IsCallbackMethodExecuting = false;

            // check Serive still running 
            if (solveServiceExist == true)
            {
                Log.Info(PAY_SERVICE_LOG, $"        Test(): SolveConnect service exists.");

                // so now check if Service is running
                isSolveServiceRunning = ServiceIsRunning(serviceName);
                if (isSolveServiceRunning != true)
                {
                    Log.Error(PAY_SERVICE_LOG, $"        Test(): {serviceName} service is not running.");
                    return;
                }
                else
                {
                    Log.Info(PAY_SERVICE_LOG, $"        Test(): {serviceName} service is running.");
                }

            }
            else
            {
                Log.Error(PAY_SERVICE_LOG, $"        Test(): SolveConnect service doesn't exist.");
                return;
            }



            Log.Info(PAY_SERVICE_LOG, "endcall Test");
        }

        /// <summary>
        /// Callback method that is triggered when the pay message is received from the Core
        /// </summary>
        /// <param name="parameters"></param>
        public void PayRequest(object parameters)
        {
            Task.Run(() => { Pay(parameters); });
        }

        /// <summary>
        /// Method will be executed in a separate thread and will send Echo command and analyze the response 
        /// </summary>
        /// <param name="parameters"></param>
        public void Pay(object parameters)
        {
            Log.Info(PAY_SERVICE_LOG, "call Pay");

            //Check if another method is executing
            if (IsCallbackMethodExecuting)
            {
                coreCommunicator.SendMessage(CommunicatorMethods.Pay, new { Status = 297, Description = "Another method is executing." });

                Log.Info(PAY_SERVICE_LOG, "        Pay(): another method is executing.");
                Log.Info(PAY_SERVICE_LOG, "endcall Pay");

                return;
            }
            else
                IsCallbackMethodExecuting = true;
            try
            {
                //Get the pay request object that will be sent to the fiscal printer
                Log.Info(PAY_SERVICE_LOG, "        Pay(): deserialize the pay request parameters.");
                PayRequest payRequest = GetPayRequest(parameters.ToString());

                //Check if the pay deserialization was successful
                if (payRequest == null)
                {
                    coreCommunicator.SendMessage(CommunicatorMethods.Pay, new { Status = 331, Description = "Failed to deserialize the pay request parameters." });
                    Log.Info(PAY_SERVICE_LOG, "        Pay(): failed to deserialize the pay request parameters.");
                    return;
                }

                if (payRequest.Amount == 0)
                {
                    coreCommunicator.SendMessage(CommunicatorMethods.Pay, new { Status = -299, Description = "amount can't be zero.", PayDetails = new PayDetails() });
                    Log.Info(PAY_SERVICE_LOG, "        Pay(): amount can't be zero.");
                    return;
                }

                if (string.IsNullOrEmpty(payRequest.TransactionReference))
                {
                    coreCommunicator.SendMessage(CommunicatorMethods.Pay, new { Status = -298, Description = "transaction reference can't be empty.", PayDetails = new PayDetails() });
                    Log.Info(PAY_SERVICE_LOG, "        Pay(): transaction reference can't be empty.");
                    return;
                }

                PayDetailsExtended payDetails = new PayDetailsExtended();

                // check the transaction details:
                payDetails.PaidAmount = payRequest.Amount;
                payDetails.TransactionReference = payRequest.TransactionReference;

                Log.Info(PAY_SERVICE_LOG,       $"        Pay(): Amount = {payDetails.PaidAmount}.");
                Log.Info(PAY_SERVICE_LOG,       $"        Pay(): Transaction Reference  = {payDetails.TransactionReference}.");
                Log.Info(PAY_SERVICE_LOG,        "        Pay(): Payment method started...");

                using (var api = new BarclayCardSmartpayApi(currency, country, port, sourceId, kioskNumber))
                {
                        var payResult = api.Pay(payDetails.PaidAmount, payDetails.TransactionReference, out TransactionReceipts payReceipts, out string transNum);
                        Log.Info(PAY_SERVICE_LOG,       $"        Pay(): Pay Result: {payResult}");

                        if (payResult != DiagnosticErrMsg.OK)
                        {
                            //create error receipt
                            payDetails.HasClientReceipt = true;

                            if (payReceipts.CustomerReturnedReceipt == null)
                                PrintErrorTicket(payDetails, transNum);
                            else
                                 CreateTicket(payReceipts.CustomerReturnedReceipt, "CUSTOMER");

                            Log.Info(PAY_SERVICE_LOG, "        Pay(): payment failed.");
                                coreCommunicator.SendMessage(CommunicatorMethods.Pay, new { Status = 334, Description = "Failed payment", PayDetailsExtended = payDetails });
                        }
                        else
                        {
                            Log.Info(PAY_SERVICE_LOG, "        Pay(): payment succeeded.");

                             //persist the Merchant transaction
                              PersistTransaction(payReceipts.MerchantReturnedReceipt, "MERCHANT");

                            payDetails.HasClientReceipt = true;

                            payDetails.PaidAmount = payRequest.Amount;
                            payDetails.TransactionReference = payRequest.TransactionReference;

                            //get tender Id details
                            payDetails.TenderMediaId = GetTenderID(payReceipts.CustomerReturnedReceipt.ToString());
                            payDetails.TenderMediaDetails = payDetails.TenderMediaId;
                            Log.Info(PAY_SERVICE_LOG, $"        Pay(): payDetails.TenderMediaId: {payDetails.TenderMediaId}");

                        //create receipt
                        CreateTicket(payReceipts.CustomerReturnedReceipt, "CUSTOMER");
                      
                            Log.Info(PAY_SERVICE_LOG, "        Pay(): credit card payment succeeded.");
                            coreCommunicator.SendMessage(CommunicatorMethods.Pay, new { Status = 0, Description = "Successful payment", PayDetailsExtended = payDetails });

                        }

                         //treat answer type
                         if (wasCancelTigged && isPaymentCancelSuccessful)
                         {
                              Log.Info(PAY_SERVICE_LOG, "        Pay(): Payment was cancelled.");
                             coreCommunicator.SendMessage(CommunicatorMethods.Pay, new { Status = 335, Description = "Failed payment. Canceled by user.", PayDetailsExtended = payDetails });
                         }
                    }

                    return;
                }
                catch (Exception ex)
                {
                    Log.Info(PAY_SERVICE_LOG, string.Format("        {0}", ex.ToString()));
                }
                finally
                {
                    wasCancelTigged = false;
                    IsCallbackMethodExecuting = false;
                    Log.Info(PAY_SERVICE_LOG, "endcall Pay");
                }
            }

        /// <summary>
        /// Callback method that is triggered when the cancel message is received from the Core
        /// </summary>
        /// <param name="parameters"></param>
        public void CancelRequest(object parameters)
        {
            Task.Run(() => { Cancel(parameters); });
        }

        /// <summary>
        /// Callback method that is triggered when the cancel message is received from the Core on a separate thread
        /// </summary>
        /// <param name="parameters"></param>
        public void Cancel(object parameters)
        {
            Log.Info(PAY_SERVICE_LOG, "call Cancel");

            wasCancelTigged = true; 

            if (isPaymentCancelSuccessful)
            {
                coreCommunicator.SendMessage(CommunicatorMethods.Cancel, new { Status = 0 });
                Log.Info(PAY_SERVICE_LOG, "        successful cancellation.");
            }
            else
            {
                coreCommunicator.SendMessage(CommunicatorMethods.Cancel, new { Status = 1 });
                Log.Info(PAY_SERVICE_LOG, "        failed cancellation.");
            }

            Log.Info(PAY_SERVICE_LOG, "endcall Cancel");
        }

        /// <summary>
        /// Method that is executed if the ExecuteCommand event is raised by the communicator.
        /// </summary>
        /// <param name="parameters">Parameters that are send by the event</param>
        public void ExecuteCommandRequest(object parameters)
        {
            Task.Run(() => { ExecuteCommand(parameters); });
        }

        private void ExecuteCommand(object executeCommandRequestJson)
        {
            try
            {
                Log.Info(PAY_SERVICE_LOG, "call ExecuteCommand");

                //Check if another method is executing
                if (IsCallbackMethodExecuting)
                {
                    coreCommunicator.SendMessage(CommunicatorMethods.ExecuteCommand, new { Status = 297, Description = "Another method is executing", ExecuteCommandResponse = "Command not executed. Reason: busy" });
                    Log.Info(PAY_SERVICE_LOG, "        another method is executing.");

                    return;
                }
                else
                    IsCallbackMethodExecuting = true;

                //Deserialize execute command parameters
                ExecuteCommandRequest executeCommandRequest = GetExecuteCommandRequest(executeCommandRequestJson.ToString());

                //Check if the execute command deserialization was successful
                if (executeCommandRequest == null)
                {
                    coreCommunicator.SendMessage(CommunicatorMethods.ExecuteCommand, new { Status = 331, Description = "failed to deserialize the execute command request parameters", ExecuteCommandResponse = "Command not executed. Reason: params" });
                    Log.Info(PAY_SERVICE_LOG, "        failed to deserialize the execute command request parameters");
                    return;
                }

                Log.Info(PAY_SERVICE_LOG, $"     Command name to be executed: {executeCommandRequest.Command}");
                Log.Info(PAY_SERVICE_LOG, $"     Command body to be executed: {executeCommandRequest.CommandInfo}");

                //success
                if (isPaymentExecuteCommandSuccessful)
                {
                    Log.Info(PAY_SERVICE_LOG,  "     ExecuteCommand succeeded.");
                    coreCommunicator.SendMessage(CommunicatorMethods.ExecuteCommand, new { Status = 0, Description = "ExecuteCommand succeeded", ExecuteCommandResponse = "Command executed" });
                    Log.Info(PAY_SERVICE_LOG, $"     Command Executed: {executeCommandRequest.Command}, ReturnedText: Command executed");
                }
                //failure
                else
                {
                        Log.Info(PAY_SERVICE_LOG,  "     ExecuteCommand failed.");
                        coreCommunicator.SendMessage(CommunicatorMethods.ExecuteCommand, new { Status = 442, Description = "ExecuteCommand failed", ExecuteCommandResponse = "Command not executed. Reason: willingly" });

                        Log.Info(PAY_SERVICE_LOG, $"     Command Executed: {executeCommandRequest.Command}, Command not executed. Reason: willingly");
                }                
            }
            catch (Exception ex)
            {
                Log.Error(PAY_SERVICE_LOG, $"ExecuteCommand exception: {ex.ToString()}");
            }
            finally
            {
                Log.Info(PAY_SERVICE_LOG, "endcall ExecuteCommand");
                IsCallbackMethodExecuting = false;
            }
        }

        /// <summary>
        /// Deserialize the received json string and extract all the parameter used for the initialization
        /// </summary>
        /// <param name="jsonItems"></param>
        /// <returns></returns>
        private bool GetInitParameters(string initJson)
        {
            try
            {
                JObject jObject = JObject.Parse(initJson);

                if (jObject == null)
                    return false;

                if (jObject["PaymentDuration"] == null ||
                    jObject["PaymentResult"] == null ||
                    jObject["TenderMedia"] == null ||
                    jObject["IsPaymentExecuteCommandSuccessful"] == null ||
                    jObject["IsPaymentCancelSuccessful"] == null ||
                    jObject["ComPort"] == null ||
                    jObject["Port"] == null ||
                    jObject["KioskNumber"] == null ||
                    jObject["SourceId"] == null ||
                    jObject["Currency"] == null ||
                    jObject["Country"] == null ||
                    jObject["ServiceName"] == null
                    )
                return false;

                paymentDuration = Convert.ToInt32(jObject["PaymentDuration"].ToString());
                isPaymentCancelSuccessful = bool.Parse(jObject["IsPaymentCancelSuccessful"].ToString());
                isPaymentExecuteCommandSuccessful = bool.Parse(jObject["IsPaymentExecuteCommandSuccessful"].ToString());
                paymentResult = jObject["PaymentResult"].ToString();
                paymentTenderMediaID = jObject["TenderMedia"].ToString();
                port = Convert.ToInt32(jObject["Port"].ToString());
                comPort = jObject["ComPort"].ToString();
                kioskNumber = Convert.ToInt32(jObject["KioskNumber"].ToString());
                currency = Convert.ToInt32(jObject["Currency"].ToString());
                country = Convert.ToInt32(jObject["Country"].ToString());
                sourceId = jObject["SourceId"].ToString();
                serviceName = jObject["ServiceName"].ToString();

                return true;
            }
            catch (Exception ex)
            {
                Log.Info(PAY_SERVICE_LOG, string.Format("        {0}", ex.ToString()));
            }
            return false;
        }

        /// <summary>
        /// Deserialize the received json string into a PayRequest object
        /// </summary>
        /// <param name="jsonItems"></param>
        /// <returns></returns>
        private PayRequest GetPayRequest(string payRequestJSonString)
        {
            try
            {
                return JsonConvert.DeserializeObject<PayRequest>(payRequestJSonString);
            }
            catch (Exception ex)
            {
                Log.Info(PAY_SERVICE_LOG, string.Format("        {0}", ex.ToString()));
            }

            return null;
        }


        /// <summary>
        /// Deserialize the received json string into a ExecuteCommandRequest object
        /// </summary>
        /// <param name="jsonItems"></param>
        /// <returns></returns>
        private ExecuteCommandRequest GetExecuteCommandRequest(string executeCommandRequestJsonString)
        {
            try
            {
                ExecuteCommandRequest returnObject = JsonConvert.DeserializeObject<ExecuteCommandRequest>(executeCommandRequestJsonString.ToString());

                return returnObject;
            }
            catch (Exception ex)
            {
                Log.Error(PAY_SERVICE_LOG, $"GetExecuteCommandRequest exception: {ex.ToString()}");
            }
            return null;
        }

        private  void PrintErrorTicket(PayDetailsExtended payDetails, string transNum)
        {
            //print the payment ticket for an error
            //
            CreateTicket("\nPayment failure with\nyour card or issuer" +
                "\nNO payment has been taken." +
                "\n\nPlease try again with another card,\nor at a manned till.\n\n" +
                 "TOTAL: " + payDetails.PaidAmount + "\n" +
                 "Trans No: " + transNum + "\n" +
                 "Date: "+ DateTime.Now.ToString("dd/mm/yy hh:mm:ss") + "\n\n" +
                 "Please retain for your records\n\n" + "CUSTOMER COPY", "CUSTOMER_ERROR");

            payDetails.HasClientReceipt = true;
            payDetails.HasMerchantReceipt = true;
        }

        /// <summary>
        /// Persist the transaction as Text file
        /// with Customer and Merchant receiept
        /// </summary>
        /// <param name="result"></param>
        private  void PersistTransaction(string receipt, string ticketType)
        {
            try
            {
               // var config = AppConfiguration.Instance;
                var outputDirectory = Path.GetFullPath(output);
                var outputPath = Path.Combine(outputDirectory, $"{DateTime.Now:yyyyMMddHHmmss}_{ticketType}_ticket.txt");

                if (!Directory.Exists(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }

               // Log.Info($"Persisting {ticketType} to {outputPath}");

                //Write the new ticket
                File.WriteAllText(outputPath, receipt.ToString());
            }
            catch (Exception ex)
            {
                Log.Error("Persist Transaction exception.");
                Log.Error(ex);
            }
        }

        /// <summary>
        /// creates the customer or merchant ticket
        /// </summary>
        /// <param name="ticket"></param>
        /// <param name="ticketType"></param>
        private void CreateTicket(string ticket, string ticketType)
        {
            try
            {
                //Delete the old ticket
                if (File.Exists(ticketPath))
                    File.Delete(ticketPath);


                //Write the new ticket
                File.WriteAllText(ticketPath, ticket);

                Log.Info($"ticket Created: {ticketType}");

                //persist the transaction
                PersistTransaction(ticket, ticketType);

            }
            catch (Exception ex)
            {
                Log.Error($"Error {ticketType} persisting ticket.");
                Log.Error(ex);
            }
        }

        public static bool ServiceExists(string ServiceName)
        {
            return ServiceController.GetServices().Any(serviceController => serviceController.ServiceName.Equals(ServiceName));
        }

        public static bool ServiceIsRunning(string ServiceName)
        {
            ServiceController sc = new ServiceController
            {
                ServiceName = ServiceName
            };

            if (sc.Status == ServiceControllerStatus.Running)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private string GetTenderID(string ticket)
        {
            string card = string.Empty;

            var values = new[] { "VISA", "MASTERCARD", "ELECTRON", "MAESTRO", "MASTERCARD CREDIT", "AMEX", "UNION PAY",
                                 "VISA CONTACTLESS", "CONTACTLESS VISA", "VISA DEBIT", "VISA CREDIT",  "VISA ELECTRON",
                                 "VISA PURCHASING",  "MASTERCARD CONTACTLESS", "CONTACTLESS MASTERCARD", "DINERS",
                                 "INTERNATIONAL MAESTRO", "MAESTRO INTERNATIONAL",  "EXPRESSPAY", "AMERICAN EXPRESS",
                                 "DISCOVER", "UNION PAY CREDIT", "JCB CREDIT", "GIVEX" };

           

            foreach (string val in values)
            {
                if (ticket.Contains(val))
                {
                    card = val;
                }
            }

            return card;
        }

    }
}
