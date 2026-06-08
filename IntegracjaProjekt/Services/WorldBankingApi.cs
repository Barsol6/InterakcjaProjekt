using System.Text.Json;
using IntegracjaProjekt.Models;

namespace IntegracjaProjekt.Services;

public class WorldBankApiService
{
    private readonly HttpClient _httpClient;
    
    private static readonly HashSet<string> EuropeanCountries = new(StringComparer.OrdinalIgnoreCase)
    {
        "AD", "AL", "AT", "BA", "BE", "BG", "BY", "CH", "CY", "CZ", "DE", "DK", 
        "EE", "ES", "FI", "FR", "GB", "GR", "HR", "HU", "IE", "IS", "IT", "LI", 
        "LT", "LU", "LV", "MC", "MD", "ME", "MK", "MT", "NL", "NO", "PL", "PT", 
        "RO", "RS", "RU", "SE", "SI", "SK", "SM", "UA", "VA"
    };

    public WorldBankApiService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "EurostatApp/1.0");
    }

    public async Task<List<MilitaryExpenditure>> FetchMilitaryDataAsync()
    {
        string url = "https://api.worldbank.org/v2/country/all/indicator/MS.MIL.TOTL.TF.ZS?date=2010:2020&format=json&per_page=4000";

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        string jsonString = await response.Content.ReadAsStringAsync();
        var results = new List<MilitaryExpenditure>();

        using var document = JsonDocument.Parse(jsonString);
        var rootElement = document.RootElement;

        if (rootElement.ValueKind == JsonValueKind.Array && rootElement.GetArrayLength() > 1)
        {
            var dataArray = rootElement[1];
            foreach (var item in dataArray.EnumerateArray())
            {
                if (!item.TryGetProperty("value", out var valueProp) || valueProp.ValueKind != JsonValueKind.Number)
                    continue;

                string countryCode = item.GetProperty("country").GetProperty("id").GetString() ?? "XX";
                
                if (!EuropeanCountries.Contains(countryCode))
                    continue;

                decimal value = valueProp.GetDecimal();
                string yearStr = item.GetProperty("date").GetString() ?? "0";

                results.Add(new MilitaryExpenditure
                {
                    CountryCode = countryCode.ToUpper(),
                    Year = int.Parse(yearStr),
                    PercentageOfGdp = value, 
                    DataSource = "WorldBank", 
                    DownloadedAt = DateTime.Now
                });
            }
        }

        return results;
    }
}