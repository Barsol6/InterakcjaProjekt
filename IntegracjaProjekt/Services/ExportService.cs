using System.Text.Json;
using System.Xml.Serialization;
using IntegracjaProjekt.Models;

namespace IntegracjaProjekt.Services;

public class ExportService
{
    public void ExportToJson(List<MilitaryExpenditure> data, string filePath = "raport_eurostat.json")
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(data, options);
        
        File.WriteAllText(filePath, jsonString);
    }

    public void ExportToXml(List<MilitaryExpenditure> data, string filePath = "raport_eurostat.xml")
    {
        var xmlSerializer = new XmlSerializer(typeof(List<MilitaryExpenditure>));
        
        using var writer = new StreamWriter(filePath);
        xmlSerializer.Serialize(writer, data);
    }
}