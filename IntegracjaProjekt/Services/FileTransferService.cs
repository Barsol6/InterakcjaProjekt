using System.Text.Json;
using System.Xml.Serialization;
using IntegracjaProjekt.Models;

namespace IntegracjaProjekt.Services;

public class FileTransferService
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


    public List<MilitaryExpenditure> ImportFromJson(string filePath = "raport_eurostat.json")
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Plik {filePath} nie istnieje. Najpierw wykonaj eksport, aby go wygenerować.");

        string jsonString = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<List<MilitaryExpenditure>>(jsonString) ?? new List<MilitaryExpenditure>();
    }

    public List<MilitaryExpenditure> ImportFromXml(string filePath = "raport_eurostat.xml")
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Plik {filePath} nie istnieje. Najpierw wykonaj eksport, aby go wygenerować.");

        var xmlSerializer = new XmlSerializer(typeof(List<MilitaryExpenditure>));
        using var reader = new StreamReader(filePath);
        return (List<MilitaryExpenditure>?)xmlSerializer.Deserialize(reader) ?? new List<MilitaryExpenditure>();
    }
}