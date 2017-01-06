using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization;
using Alexa.NET.Response;
using Alexa.NET.Request;
using System.Globalization;
using Alexa.NET.Request.Type;
using Alexa.NET;
using System.Text.RegularExpressions;
using System.Net.Http;
using Newtonsoft.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializerAttribute(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AlexaBusinessInsights
{
    public class Function
    {
        public SkillResponse FunctionHandler(SkillRequest input, ILambdaContext context)
        {
            var log = context.Logger;
            log.LogLine($"Request type: {input.Request.Type}");

            if (input.GetRequestType() == typeof(LaunchRequest))
            {
                // default launch request, let's just let them know what you can do
                log.LogLine($"Default LaunchRequest made");

                var innerResponse = new PlainTextOutputSpeech();
                (innerResponse as PlainTextOutputSpeech).Text = "Willkommen zu Business Insights. Frage nach Daten für einen Zeitraum.";

                return ResponseBuilder.Tell(innerResponse);
            }
            else if (input.GetRequestType() == typeof(IntentRequest))
            {
                var intentRequest = input.Request as IntentRequest;

                // intent request, process the intent
                log.LogLine($"Intent Requested {intentRequest.Intent.Name}");
                log.LogLine($"  Slot When = {intentRequest.Intent.Slots["When"].Value}");

                var when = intentRequest.Intent.Slots["When"].Value;
                if (String.IsNullOrEmpty(when))
                    return ResponseBuilder.Tell(new PlainTextOutputSpeech("Ich konnte den Zeitraum leider nicht verstehen."));
                
                var dayFormat = Regex.Match(when, @"^\d{4}-\d{2}-\d{2}$");
                var weekFormat = Regex.Match(when, @"^\d{4}-W\d{1,2}$");
                var monthFormat = Regex.Match(when, @"^\d{4}-\d{2}$");

                if (monthFormat.Success)
                {
                    var requestedMonth = DateTime.Parse(when);
                    var previousMonth = requestedMonth.AddMonths(-1);

                    var lastDayOfReqMonth = requestedMonth.AddMonths(1);
                    var lastDayOfPrevMonth = previousMonth.AddMonths(1);

                    var reqMonthData = GetData(requestedMonth, lastDayOfReqMonth);
                    var prevMonthData = GetData(previousMonth, lastDayOfPrevMonth);

                    var text = $"Im letzten Monat gab es {reqMonthData.NewJiraUsers.count} neue JIRA Benutzer und {reqMonthData.NewWunderlistUsers.count} neue Wunderlist Benutzer." +
                                $" Es gab {reqMonthData.NewCompanies.count} neue Anmeldungen von Firmen, davon {reqMonthData.NewCompanies.bigCompanies.Length} über 100 Benutzern.";

                    if (reqMonthData.NewCompanies.bigCompanies.Length > 0)
                    {
                        text += $" Die größeren Firmen: ";
                        foreach(var c in reqMonthData.NewCompanies.bigCompanies)
                        {
                            text += $"{c.name} mit {c.userCount} Benutzern. ";
                        }
                    }

                    log.LogLine("Prev Month: " + prevMonthData.NewCompanies.count);
                    log.LogLine("Req Month: " + reqMonthData.NewCompanies.count);

                    var percentageChange = (int)Math.Round(((double)prevMonthData.NewCompanies.count / (double)reqMonthData.NewCompanies.count) * 100);

                    log.LogLine("Änderung: " + percentageChange);

                    if (percentageChange < 100)
                    {
                        text += $"Es gab {percentageChange}% mehr Anmeldungen als im Monat davor.";
                    }
                    else if (percentageChange > 100)
                    {
                        text += $"Es gab {percentageChange - 100}% weniger Anmeldungen als im Monat davor.";
                    }
                    else
                    {
                        text += $"Es gab im Vormonat ebenfalls {reqMonthData.NewCompanies.count} neue Anmeldungen.";
                    }

                    log.LogLine(text);
                    return ResponseBuilder.Tell(new PlainTextOutputSpeech(text));
                }
                else if (weekFormat.Success)
                { 
                    //Todo
                }
                else if (dayFormat.Success)
                {
                    //Todo
                }
            }
            
            return ResponseBuilder.Tell(new PlainTextOutputSpeech("Das kann ich leider noch nicht."));    
        }

        class CompanyResponse
        {
            public int count { get; set; }
            public BigCompany[] bigCompanies { get; set; }
        }

        class BigCompany
        {
            public string name { get; set; }
            public int userCount { get; set; }
        }

        class UserReponse
        {
            public int count { get; set; }
        }

        class ResponseData
        {
            public CompanyResponse NewCompanies { get; set; }
            public UserReponse NewJiraUsers { get; set; }
            public UserReponse NewWunderlistUsers { get; set; }
        }

        private ResponseData GetData(DateTime from, DateTime to)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("userAuthToken", Environment.GetEnvironmentVariable("USER_AUTH_TOKEN"));

            var res = new ResponseData();
            var query = "?from=" + from.ToString("s", CultureInfo.InvariantCulture) + "&to=" + to.ToString("s", CultureInfo.InvariantCulture);

            res.NewCompanies = JsonConvert.DeserializeObject<CompanyResponse>(client.GetStringAsync("https://store.yasoon.com/api/analytics/newCompanies" + query).Result);
            res.NewJiraUsers = JsonConvert.DeserializeObject<UserReponse>(client.GetStringAsync("https://store.yasoon.com/api/analytics/newUsers/16" + query).Result);
            res.NewWunderlistUsers = JsonConvert.DeserializeObject<UserReponse>(client.GetStringAsync("https://store.yasoon.com/api/analytics/newUsers/17" + query).Result);

            return res;
        } 
    }
}

