using System.Text.Json;
using IntegracjaProjekt.Models;

namespace IntegracjaProjekt.Services;

public class WorldBankApiService
{
    private readonly HttpClient _httpClient;

    private static readonly HashSet<string> EuropeanCountryCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ALB", "AND", "ARM", "AUT", "AZE", "BEL", "BGR", "BIH", "BLR", "CHE", 
        "CYP", "CZE", "DEU", "DNK", "ESP", "EST", "FIN", "FRA", "GBR", "GEO", 
        "GIB", "GRC", "GRL", "HRV", "HUN", "IMN", "IRL", "ISL", "ISR", "ITA", 
        "LIE", "LTU", "LUX", "LVA", "MCA", "MDA", "MKD", "MLT", "MNE", "NLD", 
        "NOR", "POL", "PRT", "ROU", "RUS", "SMR", "SRB", "SVK", "SVN", "SWE", 
        "TUR", "UKR", "XKX"
    };
    public WorldBankApiService()
    {
        _httpClient = new HttpClient();
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "InterakcjaProjekt/1.0");
        }
        
    }

    public async Task<List<MilitaryExpenditure>> FetchMilitaryDataAsync()
    {
        string url = "https://api.worldbank.org/v2/country/all/indicator/MS.MIL.XPND.GD.ZS?date=2010:2024&format=json&per_page=4000";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        string jsonString = await response.Content.ReadAsStringAsync();
        var results = new List<MilitaryExpenditure>();
        
        DateTime downloadedAt = DateTime.Now;

        using var document = JsonDocument.Parse(jsonString);
        var rootElement = document.RootElement;

        if (rootElement.ValueKind == JsonValueKind.Array && rootElement.GetArrayLength() > 1)
        {
            var dataArray = rootElement[1];
            foreach (var item in dataArray.EnumerateArray())
            {
                string countryIso3Code = "XXX";
                if (item.TryGetProperty("countryiso3code", out var isoCodeProp) && isoCodeProp.ValueKind == JsonValueKind.String)
                {
                    countryIso3Code = isoCodeProp.GetString() ?? "XXX";
                }

                if (!EuropeanCountryCodes.Contains(countryIso3Code))
                    continue;
                
                string finalCountryCode = item.GetProperty("country").GetProperty("id").GetString() ?? "XX";
                
                if (!item.TryGetProperty("value", out var valueProp))
                    continue;

                decimal value;
                if (valueProp.ValueKind == JsonValueKind.Number)
                {
                    value = valueProp.GetDecimal();
                }
                else if (valueProp.ValueKind == JsonValueKind.Null)
                {
                    value = 0; 
                }
                else
                {
                    continue; 
                }              
                
                string yearStr = item.GetProperty("date").GetString() ?? "0";

                results.Add(new MilitaryExpenditure
                {
                    CountryCode = finalCountryCode.ToUpper(), 
                    Year = int.Parse(yearStr),
                    PercentageOfGdp = value,
                    DataSource = "WorldBank", 
                    DownloadedAt = downloadedAt
                });
            }
        }

        return results;
    }
}