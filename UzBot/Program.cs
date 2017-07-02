using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Threading;

namespace UzBot
{
    class Program
    {
        static int failCount = 0;
        static string token = "";
        static void Main(string[] args)
        {
            token = ConfigurationManager.AppSettings["token"];
            while(failCount <= 4)
            {
                var wed = new DateTime(2016, 8, 17, 20, 21, 00).AddHours(-3).Subtract(new DateTime(1970, 1, 1)).TotalSeconds.ToString();
                var thue = new DateTime(2016, 8, 18, 20, 21, 00).AddHours(-3).Subtract(new DateTime(1970, 1, 1)).TotalSeconds.ToString();
                try
                {
                    checkForTickets(wed, false);
                    checkForTickets(thue, true);
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }

                Thread.Sleep(TimeSpan.FromMinutes(1));
            }

            Console.ReadLine();
        }

        static void checkForTickets(string depTime, bool checkForSinglePlace)
        {

            var station_id_from = "2218020";
            var station_id_till = "2208001";
            var train = "084Л";
            var baseAddress = new Uri("http://booking.uz.gov.ua");
            var cookieContainer = new CookieContainer();
            using (HttpClientHandler handler = new HttpClientHandler() { CookieContainer = cookieContainer })
            using (HttpClient client = new HttpClient(handler))
            {
                client.BaseAddress = new Uri("http://booking.uz.gov.ua");

                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("station_id_from", station_id_from),
                    new KeyValuePair<string, string>("station_id_till", station_id_till),
                    new KeyValuePair<string, string>("date_dep", depTime),
                    new KeyValuePair<string, string>("train", train),
                    new KeyValuePair<string, string>("time_dep_till", ""),
                    new KeyValuePair<string, string>("another_ec", "0"),
                    new KeyValuePair<string, string>("model", "0"),
                    new KeyValuePair<string, string>("coach_type", "П"),
                    new KeyValuePair<string, string>("round_trip", "0"),
                    new KeyValuePair<string, string>("search", ""),

                });
                client.DefaultRequestHeaders.Add("GV-Ajax", "1");
                client.DefaultRequestHeaders.Add("GV-Referer", "http://booking.uz.gov.ua/en/");
                client.DefaultRequestHeaders.Add("GV-Screen", "1920x1080");
                client.DefaultRequestHeaders.Add("GV-Token", token);
                client.DefaultRequestHeaders.Add("GV-Unique-Host", "1");
                client.DefaultRequestHeaders.Add("Origin", "http://booking.uz.gov.ua");
                client.DefaultRequestHeaders.Add("Host", "booking.uz.gov.ua");
                client.DefaultRequestHeaders.Add("Referer", "http://booking.uz.gov.ua/en/");
                client.DefaultRequestHeaders.Add("Accept", "*/*");
                client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
                client.DefaultRequestHeaders.Add("Accept-Language", "uk,en-US;q=0.8,en;q=0.6,ru;q=0.4");
                client.DefaultRequestHeaders.Add("Connection", "keep-alive");
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/52.0.2743.116 Safari/537.36");

                content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

                cookieContainer.Add(baseAddress, new Cookie("_gv_lang", "en"));
                cookieContainer.Add(baseAddress, new Cookie("_gv_sessid", "o6r4kpski5re04lh0ghgnrif02"));
                cookieContainer.Add(baseAddress, new Cookie("HTTPSERVERID", "server2"));
                cookieContainer.Add(baseAddress, new Cookie("__utmt", "1"));
                cookieContainer.Add(baseAddress, new Cookie("__utma", "31515437.2137620103.1434828433.1434828433.1434828433.1"));
                cookieContainer.Add(baseAddress, new Cookie("__utmb", "31515437.2.10.1434828433"));
                cookieContainer.Add(baseAddress, new Cookie("__utmc", "31515437"));
                cookieContainer.Add(baseAddress, new Cookie("__utmz", "31515437.1434828433.1.1.utmcsr=(direct)|utmccn=(direct)|utmcmd=(none)"));
                var result = client.PostAsync("/en/purchase/coaches/", content).Result;

                if (result.IsSuccessStatusCode)
                {
                    Console.WriteLine("[{0}] Searching for tickets...", DateTime.Now.ToShortTimeString());
                    string resultContent = result.Content.ReadAsStringAsync().Result;

                    var j = Newtonsoft.Json.Linq.JObject.Parse(resultContent);

                    if (j["coaches"] is JArray)
                    {
                        var coaches = j["coaches"].ToArray();
                        foreach (var c in coaches)
                        {
                            var places = GetCoachFreePlaces(station_id_from, station_id_till, train, c["num"].ToString(), c["coach_class"].ToString(), c["coach_type_id"].ToString(), depTime, "0", client);
                            var coachNumber = int.Parse(c["num"].ToString());

                            var prevColor = Console.ForegroundColor;
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("\n\t\tFound {0} place \n\t\tcouach number {1} \n\t\tplaces : {2}", places.Count(), coachNumber, string.Join(", ", places));
                            Console.ForegroundColor = prevColor;

                            if (places.Count() >= 3)
                            {
                                SendEmailNotification("Congratulations! Yahooo!!!", "3 Tickets found in one coach");
                            }

                            if(coachNumber == 8 && places.Count() > 0)
                            {
                                SendEmailNotification("Last Tiket you are looking for is found!", "1 Tickets found in one coach #8");
                            }

                        }
                    }
                    else
                    {
                        Console.WriteLine("\tNothing Found :(");
                    }
                }
                else
                {
                    Console.WriteLine("[{0}] Failed request", DateTime.Now.ToShortTimeString());
                    if (failCount == 3)
                    {
                        // send email
                        SendEmailNotification("Program Broke", "Program Broke");
                    }
                    failCount++;
                }
            }
        }

        static void SendEmailNotification(string subject, string body)
        {
            try
            {
                using (var mail = new SmtpClient())
                {
                    using (var message = new MailMessage())
                    {
                        message.From = new MailAddress("PUT_HERE_YOUR_EMAIL");
                        message.To.Add(new MailAddress("PUT_HERE_YOUR_EMAIL"));
                        message.Body = body;
                        message.Subject = subject;
                        mail.Send(message);

                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        static string[] GetCoachFreePlaces(string station_id_from, string station_id_till, string train, string coach_num, string coach_class, string coach_type_id, string date_dep, string change_scheme, HttpClient client)
        {
            var content = new FormUrlEncodedContent(new[]
{
                    new KeyValuePair<string, string>("station_id_from", station_id_from),
                    new KeyValuePair<string, string>("station_id_till", station_id_till),
                    new KeyValuePair<string, string>("train", train),
                    new KeyValuePair<string, string>("coach_num", coach_num),
                    new KeyValuePair<string, string>("coach_class", coach_class),
                    new KeyValuePair<string, string>("coach_type_id", coach_type_id),
                    new KeyValuePair<string, string>("date_dep", date_dep),
                    new KeyValuePair<string, string>("change_scheme", change_scheme)
                });

            var result = client.PostAsync("/en/purchase/coach/", content).Result;

            if (result.IsSuccessStatusCode)
            {
                string resultContent = result.Content.ReadAsStringAsync().Result;

                var j = Newtonsoft.Json.Linq.JObject.Parse(resultContent);
                if(j["value"] is JObject && j["value"]["places"] is JObject && j["value"]["places"]["Б"] is JArray)
                {
                    return j["value"]["places"]["Б"].ToArray().Select(x => x.ToString()).ToArray();
                }
            }

            return new string[]{ };
        }
    }
}
