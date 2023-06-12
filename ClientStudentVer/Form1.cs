﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using System.IO;
using System.Security.Cryptography;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace ClientStudentVer
{
    public partial class Form1 : Form
    {
        TcpClient tcpClient;
        List<KeyValuePair<string, int>> serverIP_PortList = new List<KeyValuePair<string, int>>();
        NetworkStream stream;
        RSA PublicKey;
        // Cert-related folders and components
        static string CertSavedPath = @"..\\resources\\QuanNN.crt";
        static string originFile = @"..\\resources\\File.txt";
        static string encrFolder = @"..\\resources\";
        static string decrFolder = @"..\\resources\";
        // static string encryptedFile = @"File.enc";
        static string EncryptedSymmetricKeyPath = @"..\\resources\\Key.enc";
        string cert_thumbprint = "95266410248877b4db407a0449e6e18516cca8e8";  // QuanNN-cert
        X509Certificate2 cert;
        static byte[] ClientSessionKey, ClientIV;
        public Form1()
        {
            InitializeComponent();
            Buttons_NotClicked();
            EstablishTCPConnection();
        }
        private static void CreateSymmetricKey(RSA rsaPublicKey)
        {
            using (Aes aes = Aes.Create())
            {
                // Create instance of Aes for
                // symetric encryption of the data.
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                //aes.Key = Encoding.UTF8.
                using (ICryptoTransform transform = aes.CreateEncryptor())
                {
                    // Create symmetric key (or session key)
                    RSAPKCS1KeyExchangeFormatter keyFormatter = new RSAPKCS1KeyExchangeFormatter(rsaPublicKey);
                    ClientSessionKey = new byte[aes.Key.Length];
                    ClientSessionKey = aes.Key;
                    ClientIV = new byte[aes.IV.Length];
                    ClientIV = aes.IV;
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
                    using (FileStream outFs = new FileStream(EncryptedSymmetricKeyPath, FileMode.Create))
                    {
                        outFs.Write(LenK, 0, 4);
                        outFs.Write(LenIV, 0, 4);
                        outFs.Write(keyEncrypted, 0, lKey);
                        outFs.Write(aes.IV, 0, lIV);
                        
                        outFs.Close();
                    }
                }
            }
        }
        private void SendEncryptedFile(string encryptedFile)
        {
            // Send the key to server
            FileStream fs = new FileStream(encryptedFile, FileMode.Open);
            fs.CopyTo(stream);
            fs.Close();
            Print_log("Send " + encryptedFile + " to server.");
        }
        private static void EncryptFile(string inFile, RSA rsaPublicKey)
        {
            using (Aes aes = Aes.Create())
            {
                // Create instance of Aes for
                // symetric encryption of the data.
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Key = ClientSessionKey;
                aes.IV = ClientIV;
                using (ICryptoTransform transform = aes.CreateEncryptor())
                {
                    // Create symmetric key (or session key)
                    RSAPKCS1KeyExchangeFormatter keyFormatter = new RSAPKCS1KeyExchangeFormatter(rsaPublicKey);
                    
                    byte[] keyEncrypted =  keyFormatter.CreateKeyExchange(aes.Key, aes.GetType());

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

        // Decrypt a file using a private key.
        private static void DecryptFile(string inFile)
        {

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
                string decrFolder = @"..\resources\";
                // Construct the file name for the decrypted file.
                string outFile = decrFolder + inFile.Substring(0, inFile.LastIndexOf(".")) + ".txt";

                // Use FileStream objects to read the encrypted
                // file (inFs) and save the decrypted file (outFs).
                using (FileStream inFs = new FileStream(encrFolder + inFile, FileMode.Open))
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
                    //byte[] KeyDecrypted = ClientSessionKey; //rsaPrivateKey.Decrypt(KeyEncrypted, RSAEncryptionPadding.Pkcs1);
                    
                    // Decrypt the key.
                    using (ICryptoTransform transform = aes.CreateDecryptor(ClientSessionKey, ClientIV))
                    {

                        // Decrypt the cipher text from
                        // from the FileSteam of the encrypted
                        // file (inFs) into the FileStream
                        // for the decrypted file (outFs).
                        using (FileStream outFs = new FileStream(outFile, FileMode.Create))
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

        //     BEGINNING OF CONTROL METHOD  //////////////////
        private void usernameTextBox_Click(object sender, EventArgs e)
        {
            if (usernameTextBox.Text == "Tên tài khoản")
            {
                usernameTextBox.Text = "";
            }
        }

        private void usernameTextBox_Leave(object sender, EventArgs e)
        {
            if (usernameTextBox.Text == "")
            {
                usernameTextBox.Text = "Tên tài khoản";
            }
        }
        private void passTextBox_Leave(object sender, EventArgs e)
        {
            if (passTextBox.Text == "")
            {
                passTextBox.Text = "Mật khẩu";
            }
        }

        private void passTextBox_Click(object sender, EventArgs e)
        {
            if (passTextBox.Text == "Mật khẩu")
            {
                passTextBox.Text = "";
            }
        }
        private void SignInButton_Clicked()
        {
            SignUpButton.Enabled = false;
            SignUpButton.Visible = false;
            SignOutButton.Enabled = true;
            SignOutButton.Visible = true;
            UpdateButt.Enabled = true;
            UpdateButt.Visible = true;
            DeliLabel.Visible = true;
            NameTextBox.Visible = true;
            DepartTextBox.Visible = true;
            SexTextBox.Visible = true;
            label1.Visible = true;
            label7.Visible = true;
            label6.Visible = true;
            label4.Visible = true;
            SignInButton.Enabled = false;
        }
        private void SignOutButton_Clicked()
        {
            Buttons_NotClicked();
            SignInButton.Enabled = true;
            SignUpButton.Enabled = true;
            SignUpButton.Visible = true;
        }
        private void Buttons_NotClicked()
        {
            SignOutButton.Enabled = false;
            SignOutButton.Visible = false;
            UpdateButt.Enabled = false;
            UpdateButt.Visible = false;
            label1.Visible = false;
            label7.Visible = false;
            label6.Visible = false;
            label4.Visible = false;
            DeliLabel.Visible = false;
            NameTextBox.Visible = false;
            DepartTextBox.Visible = false;
            SexTextBox.Visible = false;
            FinishSignUpButt.Enabled = false;
            FinishSignUpButt.Visible = false;
        }
        private void SignInButton_Click(object sender, EventArgs e)
        {
            bool invalidflag = false;
            char[] invalidChars =
            { ' ', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
            '`', '~', '!', '@', '#', '$', '%', '^', '&', '*', '(', ')', '-', '_', '+', '=',
            '{', '[', '}', ']', '\\', '|', ':', ';', '"', '\'', '<', '>', '?', ',', '.', '/',
            'á', 'à', 'ả', 'ã', 'ạ','Á', 'À', 'Ả', 'Ã', 'Ạ','í', 'ì', 'ỉ', 'ĩ', 'ị','Í', 'Ì', 'Ỉ', 'Ĩ', 'Ị',
            'ó', 'ò', 'ỏ', 'õ', 'ọ', 'Ó', 'Ò', 'Ỏ', 'Õ', 'Ọ', 'ú', 'ù', 'ủ', 'ũ', 'ụ','Ú', 'Ù', 'Ủ', 'Ũ', 'Ụ',
            'é', 'è', 'ẻ', 'ẽ', 'ẹ','É', 'È', 'Ẻ', 'Ẽ', 'Ẹ','ă','ắ', 'ặ', 'ẳ', 'ẵ', 'ằ','Ă','Ắ', 'Ặ', 'Ẳ', 'Ẵ', 'Ằ',
            'â', 'ấ', 'ậ', 'Â', 'Ấ', 'Ậ','ế', 'ề', 'ê', 'ể', 'ễ', 'ệ', 'Ế', 'Ề', 'Ê', 'Ể', 'Ễ', 'Ệ',
            'ơ', 'ớ', 'ờ', 'ở', 'ỡ','ợ','Ơ', 'Ớ', 'Ờ', 'Ở', 'Ỡ', 'Ợ','ô', 'ố', 'ồ', 'ổ', 'ỗ', 'ộ', 'Ô', 'Ố', 'Ồ', 'Ổ', 'Ỗ', 'Ộ',
            'ư', 'ứ', 'ừ', 'ử', 'ữ', 'ự','Ư', 'Ứ', 'Ừ', 'Ử', 'Ữ', 'Ự', 'đ', 'Đ'};
            foreach (char ch in usernameTextBox.Text)
            {
                if (invalidChars.Contains(ch))
                {
                    invalidflag = true;
                    MessageBox.Show("Invalid characters existed in username!");
                    usernameTextBox.Clear();
                    break;
                }
            }
            if (invalidflag == false)
            {
                string b64EncodedUsernamePwd = Base64Encode(usernameTextBox.Text + '|' + passTextBox.Text);
                usernameTextBox.Text = Base64Decode(b64EncodedUsernamePwd);

                string reqHeader = "GET /" + " HTTP/1.1\r\n" // request line
                                  // request headers
                               + "Host: " + tcpClient.Client.RemoteEndPoint.ToString() + "\r\n"
                               + "Connection: keep-alive \r\n"
                               + "Upgrade-Insecure-Requests: 1\r\n"
                               + "User-Agent: C# client\r\n"
                               + "Authorization: Basic " + b64EncodedUsernamePwd + "\r\n"
                               + "Accept: text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7\r\n"
                               + "Accept-Encoding: gzip, deflate\r\n"
                               + "Accept-Language: en-US,en;q=0.9\r\n"
                               + "\r\n";
                File.WriteAllText(@"..\resources\GET.txt", reqHeader);
                EncryptFile(@"..\resources\GET.txt", PublicKey);
                SendEncryptedFile(@"..\resources\GET.enc");
                SignInButton_Clicked();
            }
        }

        private void SignUpButton_Click(object sender, EventArgs e)
        {
            SignInButton.Enabled = false;
            SignOutButton.Enabled = false;
            SignOutButton.Visible = false;
            UpdateButt.Visible = true;
            UpdateButt.Enabled = true;
            label1.Visible = true;
            label7.Visible = true;
            label6.Visible = true;
            label4.Visible = true;
            NameTextBox.Visible = true;
            DepartTextBox.Visible = true;
            SexTextBox.Visible = true;
            FinishSignUpButt.Enabled = true;
            FinishSignUpButt.Visible = true;
        }

        private void SignOutButton_Click(object sender, EventArgs e)
        {
            SignOutButton_Clicked();
        }

        private void FinishSignUpButt_Click(object sender, EventArgs e)
        {
            SignOutButton_Clicked();
        }


        //     END OF CONTROL METHOD  ///////////////////////////////////////////////////

        private void Print_log(string log)
        {
            if (LogTextBox.InvokeRequired)
            {
                LogTextBox.Invoke(new Action<string>(Print_log), log);
                return;
            }
            LogTextBox.AppendText(log + Environment.NewLine);
        }
        private void PrintResponse(string msg)
        {
            if (RespHeadTextBox.InvokeRequired)
            {
                RespHeadTextBox.Invoke(new Action<string>(PrintResponse), msg);
                return;
            }
            RespHeadTextBox.AppendText(msg + Environment.NewLine);
        }
        private void HandleServerCert()
        {
            // Load server cert (1 file .pfx for priv key and 1 file .crt for public key)
            cert = new X509Certificate2(CertSavedPath);

            // "Validate" the cert
            if (cert.Thumbprint.ToLower().ToString() == cert_thumbprint)
                Print_log("Right cert.");

            PublicKey = (RSA)cert.PublicKey.Key;    // Get public key
            CreateSymmetricKey (PublicKey);
            EncryptFile(@"..\resources\File.txt", PublicKey);
        }
        private void ReceiveCert()
        {
            byte[] certbuffer = new byte[1998];
            stream.Read(certbuffer, 0, certbuffer.Length);
            Print_log("Receive the cert.");

            // Save the server cert to local folder
            File.WriteAllBytes(CertSavedPath, certbuffer);
            stream.Flush();
        }

        private void ReceiveSave_File(string outFile)
        {
            // Receive Server File
            byte[] FileBuffer = new byte[2000];
            int bufferLen = stream.Read(FileBuffer, 0, FileBuffer.Length);
            Print_log("The file of server is received.");

            // Save the client file to local folder
            FileStream fs = new FileStream(@"..\resources\" + outFile, FileMode.Create);
            fs.Write(FileBuffer, 0, bufferLen);
            fs.Close();
            Print_log("Save the encrypted file to a folder.");
        }
        private void StartClient()
        {
            stream = tcpClient.GetStream();
            string response = "";
            ReceiveCert();
            HandleServerCert();

            SendEncryptedFile(EncryptedSymmetricKeyPath);    // Send encrypted key
            /*SendEncryptedFile(@"..\resources\File.enc");

            ReceiveSave_File("anhpnh.enc");
            DecryptFile("anhpnh.enc");
            Print_log("Decrypt file successfullly.");
            LoadFile(@"..\resources\" + "anhpnh.txt");*/
            while (true)
            {
                // receive cert from server
                //stream.Read(certbuffer, 0, certbuffer.Length);
                ReceiveSave_File("index.enc");
                DecryptFile("index.enc");
                Print_log("Decrypt file successfullly.");
                LoadFile(@"..\resources\" + "index.txt");
                // convert response from byte to string
                /*response = Encoding.ASCII.GetString(buffer, 0, b);
                MessageBox.Show(response);*/

                // Print the response to the sceen
                // print_message(response);
                // print_status(response);

                stream.Flush();
            }
        }
        private void EstablishTCPConnection()
        {
            try
            {
                string ServerIP = "127.0.0.1";
                int ServerPort = 8089;

                // Create tcp client
                tcpClient = new TcpClient();
                // connect to the server socket
                tcpClient.Connect(ServerIP, ServerPort);
                stream = tcpClient.GetStream();
                Print_log("Connected to " + ServerIP + ": " + ServerPort);

                /*KeyValuePair<string, int> endpoint = new KeyValuePair<string, int>(ServerIP, ServerPort);
                if (serverIP_PortList == null) // first time start the connection
                {
                    // Create tcp client
                    tcpClient = new TcpClient();

                    // connect to the server socket
                    serverIP_PortList.Add(new KeyValuePair<string, int>(ServerIP, ServerPort));
                    tcpClient.Connect(ServerIP, ServerPort);
                    stream = tcpClient.GetStream();
                    LogTextBox.AppendText("Connected to " + ServerIP + ": " + ServerPort);
                }
                else // Check whether connection is existed
                {
                    foreach (var element in serverIP_PortList)
                    {
                        if (endpoint.Key == element.Key && endpoint.Value == element.Value)
                        {
                            already_TCPconnected_flag = 1;
                            break;
                        }
                    }
                    if (already_TCPconnected_flag == 0)
                    {
                        // Create tcp client
                        tcpClient = new TcpClient();

                        // connect to the server socket
                        serverIP_PortList.Add(new KeyValuePair<string, int>(ServerIP, ServerPort));
                        tcpClient.Connect(ServerIP, ServerPort);
                        stream = tcpClient.GetStream();
                        LogTextBox.AppendText("Connected to " + ServerIP + ": " + ServerPort);
                    }

                }*/

                Task.Run(() => StartClient());

                // Replacement
                /*Thread ctThread = new Thread(StartClient);
                ctThread.Start();*/
            }
            catch (SocketException)
            {
                Print_log("Unable to connect to server's socket.");
            }
            catch (Exception ex)
            {
                Print_log(ex.ToString());
            }
            // CheckCertificate();
        }
        private void LoadFile (string inFile)
        {
            StreamReader sr = new StreamReader(inFile);
            PrintResponse(sr.ReadToEnd());
        }
        private string Base64Encode(string plainText)
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
