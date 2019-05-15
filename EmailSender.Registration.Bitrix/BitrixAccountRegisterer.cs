using AngleSharp.Html.Parser;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace EmailSender.Registration.Bitrix
{
    public class BitrixAccountRegisterer
    {
        private IWebProxy _proxy;
        private GetNadaClient _getNadaClient;

        HttpClient _httpClient;

        public BitrixAccountRegisterer(IWebProxy proxy, GetNadaClient nadaClient)
        {
            _proxy = proxy;
            _getNadaClient = nadaClient;

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "ru-RU,ru");
        }

        public async Task<RegistrationResult> RegisterAsync()
        { 
            /* Решение капчи */
            const int MAX_CREATED_TASKS = 3;
            const int TASK_TIMOUT_STEP = 5000;

            string captchaToken = null;

            for (int createdTasksCount = 0; createdTasksCount < MAX_CREATED_TASKS; createdTasksCount++)
            {
                
                //Получение задачи Антигейт
                string antiGateTaskId = await SendCaptchaToAntiCaptchaAsync();

                if (antiGateTaskId == null)
                {
                    await Task.Delay(TASK_TIMOUT_STEP);
                    continue;
                }
                    

                const int TIMEOUT_MAX = 50000;
                const int TIMEOUT_STEP = 5000;

                //Попытка получить решение задачи (токен)
                for (int timeOutTotal = 0; timeOutTotal < TIMEOUT_MAX; timeOutTotal += TIMEOUT_STEP)
                {
                    captchaToken = await GetCaptchaTokenAsync(antiGateTaskId);

                    if (captchaToken != null)
                        break;
                    else
                        await Task.Delay(TIMEOUT_STEP);
                }

                if (captchaToken != null)
                    break;
            }

            if (captchaToken == null)
                return null;

            /* Формирование данны для регистрации */

            string email = await _getNadaClient.GetRandomEmailAddressAsync();
            int email_agree_news = 0;
            int email_agree_webinar = 0;
            string sessid = await GetBitrixSessidAsync();
            string SITE_ID = "cr";

            string data =
                "login=" + email + "&" +
                "coupon=&&&" +
                "email[agree_news]=" + email_agree_news + "&" +
                "email[agree_webinar]=" + email_agree_webinar + "&" +
                "captchaToken=" + captchaToken + "&" +
                "sessid=" + sessid + "&" +
                "SITE_ID=" + SITE_ID;

            /* Запрос на регистрацию в Битрикс */

            Uri uri = new Uri("https://auth2.bitrix24.net/bitrix/services/main/ajax.php?action=b24network.create.register");
            StringContent content = new StringContent(data, Encoding.UTF8, "application/x-www-form-urlencoded");

            var responce = await _httpClient.PostAsync(uri, content);

            string responseString = await responce.Content.ReadAsStringAsync();

            JObject jsonResponce = JObject.Parse(responseString);

            if (jsonResponce["status"].Value<string>() == "error")
                return null;

            /* Парсинг сообщения */

            Message[] messages = await _getNadaClient.GetMessagesAsync(email, DateTime.Today);

            for(int i = 0; i < messages.Length; i++)
            {
                if(messages[i].From == "Битрикс24")
                {
                    string bitrixEntrance;
                    string confirmationLink;
                    string password;

                    ParseBitrixMessage(messages[i], out bitrixEntrance, out confirmationLink, out password);

                    RegistrationResult registrationResult = new RegistrationResult(bitrixEntrance, email, password);

                    const int TIMEOUT_MAX = 25000;
                    const int TIMEOUT_STEP = 5000;
                    int timeout_Total = 0;

                    bool isLoginConfirm;

                    do
                    {
                        isLoginConfirm = await ConfirmBitrixEmailAsync(confirmationLink);
                        if(!isLoginConfirm)
                        {
                            await Task.Delay(TIMEOUT_STEP);
                            timeout_Total += TIMEOUT_STEP;

                            if (timeout_Total > TIMEOUT_MAX)
                                return null;
                        }
                    } while (!isLoginConfirm);
                    
                    return registrationResult;
                }
            }

            return null;
        }

        private async Task<string> SendCaptchaToAntiCaptchaAsync()
        {
            NetworkCredential proxyCredential = (NetworkCredential)((WebProxy)_proxy).Credentials;

            string data = "{" +
                "\"clientKey\":\"689da824473446ba9152583268d850b7\"," +
                "\"task\": {" +
                    "\"type\": \"NoCaptchaTask\"," +
                    "\"websiteURL\":\"https://auth2.bitrix24.net/create/\"," +
                    "\"websiteKey\":\"6LcYvYQUAAAAAMBxYciy9jcsztgq3TQ6qKrLYLpl\"," +
                    "\"proxyType\":\"http\"," +
                    "\"proxyAddress\":\"" + ((WebProxy)_proxy).Address.Host + "\"," +
                    "\"proxyPort\":" + ((WebProxy)_proxy).Address.Port + "," +
                    "\"proxyLogin\":\"" + proxyCredential.UserName + "\"," +
                    "\"proxyPassword\":\"" + proxyCredential.Password + "\"," +
                    "\"userAgent\":\"Mozilla/5.0 (Macintosh; Intel Mac OS X 10_11_6) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/52.0.2743.116 Safari/537.36\"" +
                "}" +
            "}";

            try
            {
                    Uri uri = new Uri("https://api.anti-captcha.com/createTask");
                    StringContent content = new StringContent(data, Encoding.UTF8, "application/json");

                    var responce = await _httpClient.PostAsync(uri, content);

                    string responseString = await responce.Content.ReadAsStringAsync();

                    JObject jsonResponce = JObject.Parse(responseString);

                    if (jsonResponce["errorId"].Value<int>() == 0)
                        return jsonResponce["taskId"].ToString();
                    else
                        return null;
            }
            catch (HttpRequestException)
            {
                return null;
            }
        }

        private async Task<string> GetCaptchaTokenAsync(string taskId)
        {
            string data = "{" +
                "\"clientKey\":\"689da824473446ba9152583268d850b7\"," +
                "\"taskId\":" + taskId +
            "}";

            try
            {
                Uri uri = new Uri("https://api.anti-captcha.com/getTaskResult");
                StringContent content = new StringContent(data, Encoding.UTF8, "application/json");

                var responce = await _httpClient.PostAsync(uri, content);

                string responseString = await responce.Content.ReadAsStringAsync();

                JObject jsonResponce = JObject.Parse(responseString);

                JToken errorJToken = new JObject();

                if (!jsonResponce.TryGetValue("status", out errorJToken) ||
                    jsonResponce["status"].ToString() != "ready")
                    return null;
                else
                    return jsonResponce["solution"]["gRecaptchaResponse"].ToString();

            }
            catch (HttpRequestException)
            {
                return null;
            }
        }

        private async Task<string> GetBitrixSessidAsync()
        {
            try
            {
                    Uri uri = new Uri("https://auth2.bitrix24.net/create/");

                    HttpResponseMessage response = await _httpClient.GetAsync(uri);

                    string responseString = await response.Content.ReadAsStringAsync();

                    List<char> sessidList = new List<char>();

                    int sessidStartIndex = responseString.IndexOf("ssid':'") + "ssid':'".Length;

                    char currentSymbol = responseString[sessidStartIndex];

                    for (int i = sessidStartIndex + 1; currentSymbol != '\''; i++)
                    {
                        sessidList.Add(currentSymbol);
                        currentSymbol = responseString[i];
                    }

                    return new string(sessidList.ToArray());
            }

            catch(HttpRequestException)
            {
                return null;
            }
        }

        private async Task<bool> ConfirmBitrixEmailAsync(string confirmationLink)
        {
            try
            {
                Uri uri = new Uri(confirmationLink);

                HttpResponseMessage response = await _httpClient.GetAsync(uri);

                return true;
            }
            catch(HttpRequestException)
            {
                return false;
            }
        }

        private void ParseBitrixMessage(Message message, out string bitrixEntrance, out string confirmationLink, out string password)
        {
            List<char> passwordList = new List<char>();

            int passwordStartIndex = message.TextBody.IndexOf("Пароль: ") + "Пароль: ".Length;

            char currentSymbol = message.TextBody[passwordStartIndex];

            for (int k = passwordStartIndex + 1; currentSymbol != ' '; k++)
            {
                passwordList.Add(currentSymbol);
                currentSymbol = message.TextBody[k];
            }

            password = new string(passwordList.ToArray());

            HtmlParser parser = new HtmlParser();
            var doc = parser.ParseDocument(message.HtmlBody);

            bitrixEntrance = doc.QuerySelector("a:nth-child(1)").Attributes["href"].Value;

            confirmationLink = doc.QuerySelector("a:nth-child(3)").Attributes["href"].Value;
        }
    }
}