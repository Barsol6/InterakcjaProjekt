using System.Net;
using IntegracjaProjekt.Models;

namespace IntegracjaProjekt.Services;

public class EurostatApiService
{
    private readonly HttpClient _httpClient;

    public EurostatApiService()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "EurostatApp/1.0 (Student Project)");
    }

    public async Task<List<MilitaryExpenditure>> FetchMilitaryDataAsync()
    {
        string url = "https://ec.europa.eu/eurostat/api/dissemination/sdmx/2.1/data/gov_10a_exp/A.PC_GDP.S13.GF02.TE.?format=SDMX-CSV&startPeriod=2010&endPeriod=2020";
        
        HttpResponseMessage response = await _httpClient.GetAsync(url);
        
        if (!response.IsSuccessStatusCode)
        {
            string errorInfo = await response.Content.ReadAsStringAsync();
            throw new Exception($"Kod HTTP: {response.StatusCode}. Szczegóły: {errorInfo}");
        }

        string rawCsvData = await response.Content.ReadAsStringAsync();
        return ParseCsv(rawCsvData);
    }

    private List<MilitaryExpenditure> ParseCsv(string csvData)
    {
        var results = new List<MilitaryExpenditure>();
        var lines = csvData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        
        if (lines.Length <= 1) return results;

        var header = lines[0].Split(',');
        int geoIdx = Array.IndexOf(header, "geo");
        int timeIdx = Array.IndexOf(header, "TIME_PERIOD");
        int valIdx = Array.IndexOf(header, "OBS_VALUE");

        if (geoIdx == -1 || timeIdx == -1 || valIdx == -1)
            throw new Exception("Nieprawidłowy format danych z Eurostatu.");

        for (int i = 1; i < lines.Length; i++)
        {
            var columns = lines[i].Split(',');

            if (columns.Length <= valIdx || string.IsNullOrWhiteSpace(columns[valIdx]))
                continue;

            string rawValue = columns[valIdx].Trim();
            // Usunięcie liter Eurostatu (np. "1.5 p" - dane przewidywane)
            string numberPart = new string(rawValue.TakeWhile(ch => char.IsDigit(ch) || ch == '.').ToArray());

            if (decimal.TryParse(numberPart, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal value))
            {
                string geoCode = columns[geoIdx].Trim();
                
                results.Add(new MilitaryExpenditure
                {
                    CountryCode = NormalizeCountryCode(geoCode),
                    Year = int.Parse(columns[timeIdx].Trim()),
                    PercentageOfGdp = value,
                    DataSource = "Eurostat",
                    DownloadedAt = DateTime.Now
                });
            }
        }

        return results;
    }

    private string NormalizeCountryCode(string code)
    {
        if (code == "EL") return "GR"; 
        if (code == "UK") return "GB"; 
        return code;
    }
}