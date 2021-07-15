using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SonataDiscordProxyBot
{
    public class Spreadsheet
    {
        private readonly GoogleCredential credential;
        private readonly string range;
        private readonly string spreadsheetId;

        public Spreadsheet(string spreadsheetId, string range)
        {
            this.spreadsheetId = spreadsheetId;
            this.range = range;
            string[] Scopes = { SheetsService.Scope.SpreadsheetsReadonly };
            this.credential = GoogleCredential.FromFile("google-sheets.json").CreateScoped(Scopes);
        }
        public string[] GetDiscordCharOwners(string[] characters)
        {
            var service = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = this.credential,
            });

            SpreadsheetsResource.ValuesResource.GetRequest request =
                    service.Spreadsheets.Values.Get(this.spreadsheetId, this.range);

            ValueRange response = request.Execute();
            var values = response.Values.Where(item => !string.IsNullOrEmpty(item[0].ToString()) && item.Count > 1);
            return characters.Select(character => values.FirstOrDefault(item => item.Contains(character))?.First().ToString()).Where(item => !string.IsNullOrEmpty(item)).Distinct().ToArray();
        }
    }
}
