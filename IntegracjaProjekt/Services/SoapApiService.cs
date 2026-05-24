using System.Text;
using System.Xml.Linq;

namespace IntegracjaProjekt.Services;

public class SoapApiService
{
    private readonly HttpClient _httpClient;
    private const string SoapEndpoint = "http://webservices.oorsprong.org/websamples.countryinfo/CountryInfoService.wso";

    public SoapApiService()
    {
        _httpClient = new HttpClient();
    }

    public async Task<(string Name, string Capital, string Currency)> GetCountryDetailsAsync(string countryCode)
    {
        string soapEnvelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
            <soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
              <soap:Body>
                <FullCountryInfo xmlns=""http://www.oorsprong.org/websamples.countryinfo"">
                  <sCountryISOCode>{countryCode}</sCountryISOCode>
                </FullCountryInfo>
              </soap:Body>
            </soap:Envelope>";

        var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");

        HttpResponseMessage response = await _httpClient.PostAsync(SoapEndpoint, content);
        
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Błąd połączenia z usługą SOAP.");
        }

        string xmlResponse = await response.Content.ReadAsStringAsync();
        XDocument doc = XDocument.Parse(xmlResponse);

        XNamespace m = "http://www.oorsprong.org/websamples.countryinfo";

        string name = doc.Descendants(m + "sName").FirstOrDefault()?.Value ?? "Brak danych";
        string capital = doc.Descendants(m + "sCapitalCity").FirstOrDefault()?.Value ?? "Brak danych";
        string currency = doc.Descendants(m + "sCurrencyISOCode").FirstOrDefault()?.Value ?? "Brak danych";

        if (name == "Country not found in the database")
        {
            throw new Exception($"Kod '{countryCode}' nie został znaleziony w bazie SOAP.");
        }

        return (name, capital, currency);
    }
}