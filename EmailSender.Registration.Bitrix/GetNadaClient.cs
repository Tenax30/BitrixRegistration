using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace EmailSender.Registration.Bitrix
{
    public class GetNadaClient
    {
        HttpClient _httpClient;

        public GetNadaClient()
        {
            _httpClient = new HttpClient();
        }

        public async Task<string> GetRandomEmailAddressAsync()
        {
            do
            {
                /* Генерация нового почтового адреса */

                string email = GenerateNewEmailAdress();

                /* Проверка адреса */


                Uri uri = new Uri("https://getnada.com/api/v1/inboxes/" + email);

                HttpResponseMessage response = await _httpClient.GetAsync(uri);

                string responseString = await response.Content.ReadAsStringAsync();

                JObject jsonResponce = JObject.Parse(responseString);

                JToken errorJToken = new JObject();

                if (!jsonResponce.TryGetValue("error", out errorJToken))
                    return email;


            } while (true);

        }

        public async Task<Message[]> GetMessagesAsync(string address, DateTime from)
        {
            /* Проверка новых писем */

            bool newMessages;

            const int TIMEOUT_STEP = 2000;
            const int TIMEOUT_MAX = 20000;
            int timeout_Total = 0;

            do
            {
                newMessages = await CheckNewMessagesAsync(address, from);

                if (newMessages == false)
                {
                    await Task.Delay(TIMEOUT_STEP);
                    timeout_Total += TIMEOUT_STEP;
                    if (timeout_Total > TIMEOUT_MAX)
                        throw new NewMessagesTimeoutException();
                }

            } while (newMessages == false);

            /* Получение писем */

            List<Message> messages = new List<Message>();

            Uri uri = new Uri("https://getnada.com/api/v1/inboxes/" + address);

            HttpResponseMessage response = await _httpClient.GetAsync(uri);

            string responseString = await response.Content.ReadAsStringAsync();

            JObject jsonResponce = JObject.Parse(responseString);

            JArray jArrayMessages = (JArray)jsonResponce["msgs"];

            for (int i = 0; i < jArrayMessages.Count; i++)
            {
                string uid = jArrayMessages[i]["uid"].ToString();

                Message message = await GetCurrentMessageAsync(uid);
                messages.Add(message);
            }

            return messages.ToArray();
        }

        private string GenerateNewEmailAdress()
        {
            DateTime nowDateTime = DateTime.Now;

            DateTime _2k10DateTime = new DateTime(2010, 1, 1);

            TimeSpan interval = nowDateTime.Subtract(_2k10DateTime);

            long intervalMilliseconds = (long)(interval.Milliseconds + interval.Seconds * 1000 +
                interval.Minutes * 60000 + interval.Hours * 3.6e+6 + interval.Days * 8.64e+7);

            Random rnd = new Random();

            return "q" + intervalMilliseconds + rnd.Next((int)10e+6) + "@getnada.com";
        }

        private async Task<bool> CheckNewMessagesAsync(string address, DateTime from)
        {

            long unixTime = ((DateTimeOffset)from).ToUnixTimeSeconds();

            Uri uri = new Uri("https://getnada.com/api/v1/u/" + address + "/" + unixTime);

            HttpResponseMessage response = await _httpClient.GetAsync(uri);

            string responseString = await response.Content.ReadAsStringAsync();

            JObject jsonResponce = JObject.Parse(responseString);

            if (jsonResponce["new"].Value<bool>() == true)
                return true;
            else
                return false;
        }

        private async Task<Message> GetCurrentMessageAsync(string uid)
        {
            Uri uri = new Uri("https://getnada.com/api/v1/messages/" + uid);

            HttpResponseMessage response = await _httpClient.GetAsync(uri);

            string responseString = await response.Content.ReadAsStringAsync();

            JObject jsonResponce = JObject.Parse(responseString);

            Message message = new Message
            {
                From = jsonResponce["f"].ToString(),
                Subject = jsonResponce["s"].ToString(),
                TextBody = jsonResponce["text"].ToString(),
                HtmlBody = jsonResponce["html"].ToString()
            };

            return message;
        }
    }
}