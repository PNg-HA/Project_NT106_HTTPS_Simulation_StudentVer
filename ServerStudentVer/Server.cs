using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
namespace ServerStudentVer
{
    public partial class Server : Form
    {
        TcpListener listener;
        List<TcpClient> clients;
        static Dictionary<string, byte[]> client_sessionKeys ;   // string: Client-IPEndPoint, byte: AES key
        static Dictionary<string, byte[]> client_sessionIVs;     // string: Client-IPEndPoint, byte: AES IV
        static Dictionary<string, string> client_Path;
        // Cert-related folders and components
        static string CertPath = "..\\resources\\QuanNN.crt";
        static string PrivateKeyCertPath = "..\\resources\\key.pfx";
        static string originFile = @"D:\Move\Resource\File.txt";
        static string encrFolder = @"..\\resources\";
        static string decrFolder = @"..\\resources\\";
        static string encryptedFile = @"File.enc";
        static string EncryptedSymmectricKeyPath = @"..\resources\Key.enc";
        static string decryptedFile = @"..\resources\File.txt";
        // Load 2 certs (1 file .pfx for priv key and 1 file .crt for public key)
        X509Certificate2 cert;
        X509Certificate2 cert2;
        public Server()
        {
            clients = new List<TcpClient>();
            client_sessionKeys = new Dictionary<string, byte[]>();
            client_sessionIVs = new Dictionary<string, byte[]>();
            client_Path = new Dictionary<string, string>();
            listener = new TcpListener(IPAddress.Any, int.Parse("8089"));
            InitializeComponent();
            EstablishTCPConnections();
            cert = new X509Certificate2(PrivateKeyCertPath, "", X509KeyStorageFlags.Exportable);
            cert2 = new X509Certificate2(CertPath);
        }
        private static void EncryptFile(TcpClient client, string inFile, RSA rsaPublicKey)
        {
            // Client file path
            string ClientEndPoint = client_Path[client.Client.RemoteEndPoint.ToString()];
            using (Aes aes = Aes.Create())
            {
                // Create instance of Aes for
                // symetric encryption of the data.
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Key = client_sessionKeys[client.Client.RemoteEndPoint.ToString()]; // Encoding.UTF8.GetBytes(keyAES);
                aes.IV = client_sessionIVs[client.Client.RemoteEndPoint.ToString()];
                using (ICryptoTransform transform = aes.CreateEncryptor())
                {
                    // Create symmetric key (or session key)
                    RSAPKCS1KeyExchangeFormatter keyFormatter = new RSAPKCS1KeyExchangeFormatter(rsaPublicKey);

                    byte[] keyEncrypted = keyFormatter.CreateKeyExchange(aes.Key, aes.GetType());

                    // Create byte arrays to contain
                    // the length values of the key and IV.
                    byte[] LenK = new byte[4];
                    byte[] LenIV = new byte[4];

                    int lKey = keyEncrypted.Length;
                    LenK = BitConverter.GetBytes(lKey);
                    int lIV = aes.IV.Length;
                    LenIV = BitConverter.GetBytes(lIV);

                    // Write the following to the FileStream
                    // for the encrypted file (outFs):
                    // - length of the key
                    // - length of the IV
                    // - ecrypted key
                    // - the IV
                    // - the encrypted cipher content

                    int startFileName = inFile.LastIndexOf("\\") + 1;
                    // Change the file's extension to ".enc"
                    string outFile = encrFolder + inFile.Substring(startFileName, inFile.LastIndexOf(".") - startFileName) + ".enc";
                    Directory.CreateDirectory(encrFolder);

                    using (FileStream outFs = new FileStream(outFile, FileMode.Create))
                    {

                        outFs.Write(LenK, 0, 4);
                        outFs.Write(LenIV, 0, 4);
                        outFs.Write(keyEncrypted, 0, lKey);
                        outFs.Write(aes.IV, 0, lIV);

                        // Now write the cipher text using
                        // a CryptoStream for encrypting.
                        using (CryptoStream outStreamEncrypted = new CryptoStream(outFs, transform, CryptoStreamMode.Write))
                        {

                            // By encrypting a chunk at
                            // a time, you can save memory
                            // and accommodate large files.
                            int count = 0;
                            // blockSizeBytes can be any arbitrary size.
                            int blockSizeBytes = aes.BlockSize / 8;
                            byte[] data = new byte[blockSizeBytes];
                            int bytesRead = 0;

                            using (FileStream inFs = new FileStream(inFile, FileMode.Open))
                            {
                                do
                                {
                                    count = inFs.Read(data, 0, blockSizeBytes);
                                    outStreamEncrypted.Write(data, 0, count);
                                    bytesRead += count;
                                }
                                while (count > 0);
                                inFs.Close();
                            }
                            outStreamEncrypted.FlushFinalBlock();
                            outStreamEncrypted.Close();
                        }
                        outFs.Close();
                    }
                }
            }
        }
        private static void DecryptFile(TcpClient client, string inFile, RSA rsaPrivateKey)
        {
            // Create the client path
            string ClientEndPoint = client_Path[client.Client.RemoteEndPoint.ToString()];

            // Create instance of Aes for
            // symetric decryption of the data.
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                // Create byte arrays to get the length of
                // the encrypted key and IV.
                // These values were stored as 4 bytes each
                // at the beginning of the encrypted package.
                byte[] LenK = new byte[4];
                byte[] LenIV = new byte[4];

                // Unused: Construct the file name for the decrypted file. 
                string outFile = decrFolder + inFile.Substring(0, inFile.LastIndexOf(".")) + ".txt";

                // Use FileStream objects to read the encrypted
                // file (inFs) and save the decrypted file (outFs).
                using (FileStream inFs = new FileStream(@"..\resources\" + ClientEndPoint +"_File.enc", FileMode.Open))
                {

                    inFs.Seek(0, SeekOrigin.Begin);
                    inFs.Seek(0, SeekOrigin.Begin);
                    inFs.Read(LenK, 0, 3);
                    inFs.Seek(4, SeekOrigin.Begin);
                    inFs.Read(LenIV, 0, 3);

                    // Convert the lengths to integer values.
                    int lenK = BitConverter.ToInt32(LenK, 0);
                    int lenIV = BitConverter.ToInt32(LenIV, 0);

                    // Determine the start position of
                    // the cipher text (startC)
                    // and its length(lenC).
                    int startC = lenK + lenIV + 8;
                    int lenC = (int)inFs.Length - startC;

                    // Create the byte arrays for
                    // the encrypted Aes key,
                    // the IV, and the cipher text.
                    byte[] KeyEncrypted = new byte[lenK];
                    byte[] IV = new byte[lenIV];

                    // Extract the key and IV
                    // starting from index 8
                    // after the length values.
                    inFs.Seek(8, SeekOrigin.Begin);
                    inFs.Read(KeyEncrypted, 0, lenK);
                    inFs.Seek(8 + lenK, SeekOrigin.Begin);
                    inFs.Read(IV, 0, lenIV);
                    Directory.CreateDirectory(decrFolder);

                    
                    // Use RSA
                    // to decrypt the Aes key.
                    byte[] KeyDecrypted =  rsaPrivateKey.Decrypt(KeyEncrypted, RSAEncryptionPadding.Pkcs1);

                    if (Encoding.UTF8.GetString(KeyDecrypted) == Encoding.UTF8.GetString(client_sessionKeys[client.Client.RemoteEndPoint.ToString()]))
                    {
                        // Decrypt the key.
                        using (ICryptoTransform transform = aes.CreateDecryptor(client_sessionKeys[client.Client.RemoteEndPoint.ToString()], client_sessionIVs[client.Client.RemoteEndPoint.ToString()]))   //KeyDecrypted, IV))
                        {

                            // Decrypt the cipher text from
                            // from the FileSteam of the encrypted
                            // file (inFs) into the FileStream
                            // for the decrypted file (outFs).
                            using (FileStream outFs = new FileStream(@"..\resources\"+ ClientEndPoint + "_File.txt", FileMode.Create))
                            {

                                int count = 0;

                                int blockSizeBytes = aes.BlockSize / 8;
                                byte[] data = new byte[blockSizeBytes];

                                // By decrypting a chunk a time,
                                // you can save memory and
                                // accommodate large files.

                                // Start at the beginning
                                // of the cipher text.
                                inFs.Seek(startC, SeekOrigin.Begin);
                                using (CryptoStream outStreamDecrypted = new CryptoStream(outFs, transform, CryptoStreamMode.Write))
                                {
                                    do
                                    {
                                        count = inFs.Read(data, 0, blockSizeBytes);
                                        outStreamDecrypted.Write(data, 0, count);
                                    }
                                    while (count > 0);

                                    outStreamDecrypted.FlushFinalBlock();
                                    outStreamDecrypted.Close();
                                }
                                outFs.Close();
                            }
                            inFs.Close();
                        }
                    }
                    
                }
            }
        }
        private void AcceptClients()
        {
            while (true)
            {
                try
                {
                    TcpClient client = listener.AcceptTcpClient();
                    AddMessageToLog(client.Client.RemoteEndPoint + ": connected.");
                    clients.Add(client);
                    Task.Run(() => HandleClient(client));
                }
                catch (Exception ex)
                {
                    AddMessageToLog("Error accepting client: " + ex.Message);
                }
            }
        }
        private void SendEncryptedFile(string encryptedFile, NetworkStream stream)
        {
            // Send the key to server
            FileStream fs = new FileStream(encryptedFile, FileMode.Open);
            fs.CopyTo(stream);
            fs.Close();
            AddMessageToLog("Send " + encryptedFile + " to client.");
        }
        private void HandleClient(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            SendCert(client);

            // Add Client Path to the Client_Path list
            IPEndPoint iPEndPoint = (IPEndPoint)client.Client.RemoteEndPoint;
            string ClientEndPoint = iPEndPoint.Address.ToString() + '.' + iPEndPoint.Port.ToString();
            client_Path.Add(client.Client.RemoteEndPoint.ToString(), ClientEndPoint);

            // Receive Client Key
            byte[] KeyBuffer = new byte[1024];
            int bufferLen = stream.Read(KeyBuffer, 0, KeyBuffer.Length);
            AddMessageToLog("The key of " + client.Client.RemoteEndPoint.ToString() + "is received.");

            // Save the client key to client_IPEndPoint's folder
            FileStream fs = new FileStream(@"..\resources\"+ ClientEndPoint+"_Key.enc", FileMode.Create);
            fs.Write(KeyBuffer, 0, bufferLen);
            fs.Close();
            AddMessageToLog("Save the encrypted key to a folder.");

            HandleClientEncryptedKey(client, cert.GetRSAPrivateKey());

            // Receive Client File
            byte[] FileBuffer = new byte[2000];
            /*bufferLen = stream.Read(FileBuffer, 0, FileBuffer.Length);
            AddMessageToLog("The file of " + client.Client.RemoteEndPoint.ToString() + "is received.");

            // Save the client file to local folder
            fs = new FileStream(@"..\resources\" + ClientEndPoint + "_File.enc", FileMode.Create);
            fs.Write(FileBuffer, 0, bufferLen);
            fs.Close();
            AddMessageToLog("Save the encrypted file to a folder.");

            DecryptFile (client, encryptedFile, cert.GetRSAPrivateKey());

            // Create anhpnh.enc
            var publicKey = (RSA)cert2.PublicKey.Key;    // Get public key
            EncryptFile(client, @"..\resources\anhpnh.txt", publicKey);

            // Send anhpnh.enc
            SendEncryptedFile(@"..\resources\anhpnh.enc", stream);*/
            while (true)
            {
                try
                {
                    // Receive Client File
                    bufferLen = stream.Read(FileBuffer, 0, FileBuffer.Length);
                    AddMessageToLog("The file of " + client.Client.RemoteEndPoint.ToString() + "is received.");

                    // Save the client file to local folder
                    fs = new FileStream(@"..\resources\" + ClientEndPoint + "_File.enc", FileMode.Create);
                    fs.Write(FileBuffer, 0, bufferLen);
                    fs.Close();
                    AddMessageToLog("Save the encrypted file to a folder.");

                    DecryptFile(client, encryptedFile, cert.GetRSAPrivateKey());

                    // Create anhpnh.enc
                    var publicKey = (RSA)cert2.PublicKey.Key;    // Get public key
                    EncryptFile(client, @"..\resources\anhpnh.txt", publicKey);

                    // Send anhpnh.enc
                    SendEncryptedFile(@"..\resources\anhpnh.enc", stream);
                }
                catch (Exception ex)
                {
                    AddMessageToLog("Error handling client: " + ex.Message);
                    clients.Remove(client);
                    break;
                }
            }
        }
        private void HandleClientEncryptedKey(TcpClient client, RSA rsaPrivateKey)
        {
            // Create the client path
            string ClientEndPoint = client_Path[client.Client.RemoteEndPoint.ToString()];

            // Create instance of Aes for
            // symetric decryption of the data.
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                
                // Create byte arrays to get the length of
                // the encrypted key and IV.
                // These values were stored as 4 bytes each
                // at the beginning of the encrypted package.
                byte[] LenK = new byte[4];
                byte[] LenIV = new byte[4];

                // Use FileStream objects to read the encrypted
                // semetric key (inFs) and save the decrypted file (outFs).
                
                using (FileStream inFs = new FileStream(@"..\resources\" + ClientEndPoint + "_Key.enc", FileMode.Open))
                {
                    
                    inFs.Seek(0, SeekOrigin.Begin);
                    inFs.Seek(0, SeekOrigin.Begin);
                    inFs.Read(LenK, 0, 3);
                    inFs.Seek(4, SeekOrigin.Begin);
                    inFs.Read(LenIV, 0, 3);

                    // Convert the lengths to integer values.
                    int lenK = BitConverter.ToInt32(LenK, 0);
                    int lenIV = BitConverter.ToInt32(LenIV, 0);

                    // Determine the start position of
                    // the cipher text (startC)
                    // and its length(lenC).
                    int startC = lenK + lenIV + 8;
                    int lenC = (int)inFs.Length - startC;

                    // Create the byte arrays for
                    // the encrypted Aes key,
                    // the IV, and the cipher text.
                    byte[] KeyEncrypted = new byte[lenK];
                    byte[] IV = new byte[lenIV];

                    // Extract the key and IV
                    // starting from index 8
                    // after the length values.
                    inFs.Seek(8, SeekOrigin.Begin);
                    inFs.Read(KeyEncrypted, 0, lenK);
                    inFs.Seek(8 + lenK, SeekOrigin.Begin);
                    inFs.Read(IV, 0, lenIV);
                    Directory.CreateDirectory(decrFolder);

                    // Use RSA
                    // to decrypt the Aes key.
                    byte[] KeyDecrypted = rsaPrivateKey.Decrypt(KeyEncrypted, RSAEncryptionPadding.Pkcs1);

                    byte[] ClientSessionKey = new byte[aes.Key.Length];
                    ClientSessionKey = KeyDecrypted;
                    byte[] ClientSessionIV = new byte[IV.Length];
                    ClientSessionIV = IV;

                    client_sessionKeys.Add(client.Client.RemoteEndPoint.ToString(), ClientSessionKey);
                    client_sessionIVs.Add(client.Client.RemoteEndPoint.ToString(), ClientSessionIV);
                    if (KeyDecrypted == client_sessionKeys[client.Client.RemoteEndPoint.ToString()])
                        AddMessageToLog("Decrypt right client key");
                    //MessageBox.Show(Encoding.UTF8.GetString(client_sessionKeys[client.Client.RemoteEndPoint.ToString()]));
                    // tbx_log.AppendText(client_sessionKeys[client.Client.RemoteEndPoint.ToString()].ToString());
                    inFs.Close();
                }
                AddMessageToLog(client.Client.RemoteEndPoint.ToString() + "'s decrypted key is added to the list.");
            }
        }
        private void ReceiveClientKey()
        {
             
        }
        private void SendCert(TcpClient client)
        {
            
            StreamReader sr = new StreamReader(CertPath); // create a stream reader file from OpenFileDialog

            // Send the signature to server
            Byte[] CertByte = Encoding.ASCII.GetBytes(sr.ReadToEnd());
            NetworkStream stream = client.GetStream();
            stream.Write(CertByte, 0, CertByte.Length);
            stream.Flush();
            AddMessageToLog("Send cert to " + client.Client.RemoteEndPoint.ToString());
        }
        private void HandleMessage(string message, TcpClient client)
        {
            AddMessageToLog(client.Client.RemoteEndPoint + ":\r\n" + message);
            string command = message.Substring(0, message.IndexOf("\r\n"));
            string[] cfields = command.Split(' ');
            string request_method = cfields[0];
            string resource = cfields[1];
            string version = cfields[2];


            if (request_method == "GET")
            {
                SendMessage(GETMethod(resource), client);
            }
            else if (request_method == "POST")
            {
                SendMessage(POSTMethod(resource, message), client);
            }
            else if (request_method == "DELETE")
            {
                SendMessage(DELETEMethod(resource), client);
            }
        }
        private string GETMethod(string res)
        {
            string filePath = "../resources" + res;
            try
            {
                StreamReader rd = new StreamReader(filePath);
                string payload = rd.ReadToEnd();
                DateTime timestamp = DateTime.Now;
                string info = "Server: C# server\r\n"
                            + "Content-Type: text/html/json; charset=UTF-8\r\n"
                            + "Date: " + timestamp.ToString() + "\r\n"
                            + "Connection: keep-alive\r\n"
                            + "Content-Language: en\r\n";

                string header = "HTTP/1.1 200 OK\r\n" + info + "\r\n";
                return header + payload;
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while reading the file: " + ex.Message);
                return "HTTP/1.1 404 Not Found\r\n"
                        + "Content-Type: text/html; charset=UTF-8\r\n"
                        + "Date: " + DateTime.Now.ToString() + "\r\n";
            }
        }
        private string POSTMethod(string res, string message)
        {
            string filePath = "../resources" + res;
            try
            {
                string body = message.Split(new string[] { "\r\n\r\n" }, StringSplitOptions.None)[1].Trim();

                string a = body.Remove(body.Length - 1, 1).Remove(0, 1).Trim();

                string key = "\"" + a.Split(new string[] { ": " }, StringSplitOptions.None)[0] + "\"";
                string value = "\"" + a.Split(new string[] { ": " }, StringSplitOptions.None)[1] + "\"";

                string data = File.ReadAllText(filePath);

                string data_temp = data.Remove(data.Length - 1, 1).Remove(0, 1).Trim();

                string[] data_pair = data_temp.Split(new string[] { ",\r\n" }, StringSplitOptions.None);

                foreach (string pair in data_pair)
                {
                    string temp_key = pair.Split(new string[] { ": " }, StringSplitOptions.None)[0].Trim();
                    string temp_value = pair.Split(new string[] { ": " }, StringSplitOptions.None)[1].Trim();
                    if (temp_key == key)
                    {
                        data = data.Replace(temp_value, value);
                    }
                }
                File.WriteAllText(filePath, data);

                DateTime timestamp = DateTime.Now;
                string info = "Server: C# server\r\n"
                            + "Content-Type: text/html/json; charset=UTF-8\r\n"
                            + "Date: " + timestamp.ToString() + "\r\n"
                            + "Connection: keep-alive\r\n"
                            + "Content-Language: en\r\n";

                string header = "HTTP/1.1 200 OK\r\n" + info + "\r\n";
                return header;
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while reading the file: " + ex.Message);
                return "HTTP/1.1 404 Not Found\r\n"
                        + "Content-Type: text/html; charset=UTF-8\r\n"
                        + "Date: " + DateTime.Now.ToString() + "\r\n";
            }
        }

        private string DELETEMethod(string res)
        {
            string filePath = "../resources" + res;
            try
            {
                File.Delete(filePath);
                string payload = "<html><body><h1>File deleted.</h1></body></html>";
                DateTime timestamp = DateTime.Now;
                string info = "Server: C# server\r\n"
                            + "Content-Type: text/html/json; charset=UTF-8\r\n"
                            + "Date: " + timestamp.ToString() + "\r\n"
                            + "Connection: keep-alive\r\n"
                            + "Content-Language: en\r\n";

                string header = "HTTP/1.1 200 OK\r\n" + info + "\r\n";
                return header + payload;
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while reading the file: " + ex.Message);
                return "HTTP/1.1 404 Not Found\r\n"
                        + "Content-Type: text/html; charset=UTF-8\r\n"
                        + "Date: " + DateTime.Now.ToString() + "\r\n";
            }
        }
        private void SendMessage(string message, TcpClient client)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(message + "\r\n");
            NetworkStream stream = client.GetStream();
            stream.Write(bytes, 0, bytes.Length);
        }

        private void AddMessageToLog(string message)
        {
            if (tbx_log.InvokeRequired)
            {
                tbx_log.Invoke(new Action<string>(AddMessageToLog), message);
                return;
            }
            tbx_log.AppendText(message + Environment.NewLine);
        }

        private void EstablishTCPConnections()
        {
            try
            {
                listener.Start();
                AddMessageToLog("Server started.");
                Task.Run(() => AcceptClients());
            }
            catch (Exception ex)
            {
                AddMessageToLog("Error starting server: " + ex.Message);
            }
        }
        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }
        public static string Base64Decode(string base64EncodedData)
        {
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }
    }
}
