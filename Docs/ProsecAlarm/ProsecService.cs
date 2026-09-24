 public class IProsecService
 {
     /// <summary>
     /// Panel bilgilerini listeler
     /// </summary>  
     /// <param name="userid"><inheritdoc cref="Kullanıcı adı" path="/summary"/></param>
     /// <param name="uDevId"><inheritdoc cref="Gprs ise imei No , Ethernet ise Mac Adresi" path="/summary"/></param>
     /// <returns> </returns>
     public string spcGetPanels(int userid, string uDevId)
     {
         string result = string.Empty;
         var httpWebRequest = (HttpWebRequest)WebRequest.Create("http://panel.prosec.com.tr:3300/datasnap/rest/TServerMethods1/SpcGetPanels");
         httpWebRequest.ContentType = "application/json";
         httpWebRequest.Method = "POST";

         using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
         {
             string json = "{\"userid\":\"" + Encrypt(userid.ToString()) + "\"," +
                           "\"uDevId\":\"" + Encrypt(uDevId) + "\"}";
             streamWriter.Write(json);
         }
         var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
         using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
         {
             result = streamReader.ReadToEnd();
         }
         return result;
     }
     /// <summary>
     /// Alarm sistemini kurmanızı ve ya devre dışı bırakmanızı sağlar
     /// </summary>
     /// <param name="userid"><inheritdoc cref="Kullanıcı adı" path="/summary"/></param>
     /// <param name="uDevId"><inheritdoc cref="Gprs ise imei No , Ethernet ise Mac Adresi" path="/summary"/></param>
     /// <param name="uCommand"><inheritdoc cref="STAYARM,DISARM,ARM " path="/summary"/></param>
     /// <param name="uPanelPass"><inheritdoc cref="Panel Şifresi 4 ile 8 hane arasında olmalı" path="/summary"/></param>
     /// <returns> </returns>
     public string spcArmDisArm(int userid, string uDevId, string uCommand, int uPanelPass)
     {
         string result = string.Empty;
         var httpWebRequest = (HttpWebRequest)WebRequest.Create("http://panel.prosec.com.tr:3300/datasnap/rest/TServerMethods1/SpcArmDisArm");
         httpWebRequest.ContentType = "application/json";
         httpWebRequest.Method = "POST";

         using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
         {
             string json = "{\"userid\":\"" + Encrypt(userid.ToString()) + "\"," +
                           "\"uDevId\":\"" + Encrypt(uDevId) + "\"," +
                           "\"uCommand\":\"" + Encrypt(uCommand) + "\"," +
                           "\"uPanelPass\":\"" + Encrypt(uPanelPass.ToString()) + "\"}";
             streamWriter.Write(json);
         }
         var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
         using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
         {
             result = streamReader.ReadToEnd();
         }
         return result;
     }
     /// <summary>
     /// Zone Durumlarını Listeler
     /// </summary>
     /// <param name="userid"><inheritdoc cref="Kullanıcı adı" path="/summary"/></param>
     /// <param name="uDevId"><inheritdoc cref="Gprs ise imei No , Ethernet ise Mac Adresi" path="/summary"/></param>
     /// <returns> </returns>
     public string spcGetPanelZonesByPassStatus(int userid, string uDevId)
     {
         string result = string.Empty;
         var httpWebRequest = (HttpWebRequest)WebRequest.Create("http://panel.prosec.com.tr:3300/datasnap/rest/TServerMethods1/spcGetPanelZonesByPassStatus");
         httpWebRequest.ContentType = "application/json";
         httpWebRequest.Method = "POST";

         using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
         {
             string json = "{\"userid\":\"" + Encrypt(userid.ToString()) + "\"," +
                           "\"uDevId\":\"" + Encrypt(uDevId) + "\"}";
             streamWriter.Write(json);
         }
         var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
         using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
         {
             result = streamReader.ReadToEnd();
         }
         return result;
     }
     /// <summary>
     /// Seçilen Çıkışı Açmayı Sağlar
     /// </summary>
     /// <param name="userid"><inheritdoc cref="Kullanıcı adı" path="/summary"/></param>
     /// <param name="uDevId"><inheritdoc cref="Gprs ise imei No , Ethernet ise Mac Adresi" path="/summary"/></param>
     /// <param name="zOneNo"><inheritdoc cref="1 no lu zone a bypass yapılmak istendiği zaman 0 olarak gönderilmelidir. (ZoneNo-1)" path="/summary"/></param>
     /// <param name="zOnePos"><inheritdoc cref="0 Bypassı kaldırır, 1 Bypass yapılmasını sağlar" path="/summary"/></param>
     /// <returns> </returns>
     public string spcSetPanelZonesByPassStatus(int userid, string uDevId, int zOneNo, int zOnePos)
     {
         string result = string.Empty;
         var httpWebRequest = (HttpWebRequest)WebRequest.Create("http://panel.prosec.com.tr:3300/datasnap/rest/TServerMethods1/SpcSetPanelZonesByPassStatus");
         httpWebRequest.ContentType = "application/json";
         httpWebRequest.Method = "POST";

         using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
         {
             string json = "{\"userid\":\"" + Encrypt(userid.ToString()) + "\"," +
                           "\"uDevId\":\"" + Encrypt(uDevId) + "\"," +
                           "\"ZoneNo\":\"" + Encrypt(zOneNo.ToString()) + "\"," +
                           "\"ZonePos\":\"" + Encrypt(zOnePos.ToString()) + "\"}";
             streamWriter.Write(json);
         }
         var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
         using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
         {
             result = streamReader.ReadToEnd();
         }
         return result;
     }
     /// <summary>
     /// Çıkışları Listeleri
     /// </summary>
     /// <param name="userid"><inheritdoc cref="Kullanıcı adı" path="/summary"/></param>
     /// <param name="uDevId"><inheritdoc cref="Gprs ise imei No , Ethernet ise Mac Adresi" path="/summary"/></param>
     /// <returns> </returns>
     public string spcGetPanelOutputs(int userid, string uDevId)
     {
         string result = string.Empty;
         var httpWebRequest = (HttpWebRequest)WebRequest.Create("http://panel.prosec.com.tr:3300/datasnap/rest/TServerMethods1/SpcGetPanelOutputs");
         httpWebRequest.ContentType = "application/json";
         httpWebRequest.Method = "POST";

         using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
         {
             string json = "{\"userid\":\"" + Encrypt(userid.ToString()) + "\"," +
                           "\"uDevId\":\"" + Encrypt(uDevId) + "\"}";
             streamWriter.Write(json);
         }
         var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
         using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
         {
             result = streamReader.ReadToEnd();
         }
         return result;
     }
     /// <summary>
     /// Seçilen Çıkışı Açmayı Sağlar
     /// </summary>
     /// <param name="userid"><inheritdoc cref="Kullanıcı adı" path="/summary"/></param>
     /// <param name="uDevId"><inheritdoc cref="Gprs ise imei No , Ethernet ise Mac Adresi" path="/summary"/></param>
     /// <param name="outNo"><inheritdoc cref="Çıkış numarasını belirtir. 0 dan başlar fakat 0 siren dir panel den 0 çıkış no su siren olarak tanımlanmışsa komut kabul edilmez" path="/summary"/></param>
     /// <param name="outPos"><inheritdoc cref="0 Çıkışı Kapatır, 1 çıkışı açar" path="/summary"/></param>
     /// <returns> </returns>
     public string spcSetPanelOutputs(int userid, string uDevId, int outNo, int outPos)
     {
         string result = string.Empty;
         var httpWebRequest = (HttpWebRequest)WebRequest.Create("http://panel.prosec.com.tr:3300/datasnap/rest/TServerMethods1/SpcSetPanelOutputs");
         httpWebRequest.ContentType = "application/json";
         httpWebRequest.Method = "POST";

         using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
         {
             string json = "{\"userid\":\"" + Encrypt(userid.ToString()) + "\"," +
                           "\"uDevId\":\"" + Encrypt(uDevId) + "\"," +
                           "\"outNo\":\"" + Encrypt(outNo.ToString()) + "\"," +
                           "\"outPos\":\"" + Encrypt(outPos.ToString()) + "\"}";
             streamWriter.Write(json);
         }
         var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
         using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
         {
             result = streamReader.ReadToEnd();
         }
         return result;
     }

     #region Cryptography       
     public string Encrypt(string toEncrypt)
     {
         toEncrypt = toEncrypt.Replace(":", "");
         byte[] keyArray = new byte[] { 0x13, 0xFA, 0x45, 0xC5, 0x4A, 0x55, 0x16, 0x7B, 0x93, 0xFE, 0x3C, 0x63, 0x68, 0xE0, 0xAf, 0x65, 0xC1, 0x34, 0x9b, 0xB1, 0x00, 0xFF, 0x99, 0x32 };
         byte[] initVector = new byte[] { 0xc7, 0x2d, 0xed, 0xf8, 0xf4, 0xf7, 0x8c, 0xfb };
         byte[] byteBuff = Encoding.UTF8.GetBytes(toEncrypt.PadRight(16, ' '));

         TripleDESCryptoServiceProvider desCryptoProvider = new TripleDESCryptoServiceProvider();
         desCryptoProvider.Key = keyArray;
         desCryptoProvider.Mode = CipherMode.CBC;
         desCryptoProvider.IV = initVector;
         desCryptoProvider.Padding = PaddingMode.None;

         var encoded = desCryptoProvider.CreateEncryptor().TransformFinalBlock(byteBuff, 0, byteBuff.Length);
         var encodedText = BitConverter.ToString(encoded);
         encodedText = encodedText.Replace("-", "");
         return encodedText;
     }
     public string Decrypt(string encodedText)
     {
         byte[] keyArray = new byte[] { 0x13, 0xFA, 0x45, 0xC5, 0x4A, 0x55, 0x16, 0x7B, 0x93, 0xFE, 0x3C, 0x63, 0x68, 0xE0, 0xAf, 0x65, 0xC1, 0x34, 0x9b, 0xB1, 0x00, 0xFF, 0x99, 0x32 };
         byte[] initVector = new byte[] { 0xc7, 0x2d, 0xed, 0xf8, 0xf4, 0xf7, 0x8c, 0xfb };
         byte[] byteBuff = StringToByteArray(encodedText);

         TripleDESCryptoServiceProvider desCryptoProvider = new TripleDESCryptoServiceProvider();

         desCryptoProvider.Key = keyArray;
         desCryptoProvider.Mode = CipherMode.CBC;
         desCryptoProvider.IV = initVector;
         desCryptoProvider.Padding = PaddingMode.None;

         string plaintext = Encoding.UTF8.GetString(desCryptoProvider.CreateDecryptor().TransformFinalBlock(byteBuff, 0, byteBuff.Length));
         plaintext = plaintext.Trim();

         return plaintext;
     }
     public byte[] StringToByteArray(string encodedText)
     {
         return Enumerable.Range(0, encodedText.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(encodedText.Substring(x, 2), 16))
                             .ToArray();
     }
     #endregion
 }