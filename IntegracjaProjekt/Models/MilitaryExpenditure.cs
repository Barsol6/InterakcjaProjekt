namespace IntegracjaProjekt.Models;

public class MilitaryExpenditure
{
    public int Id { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public int Year { get; set; }
    public decimal PercentageOfGdp { get; set; }
    public string DataSource { get; set; }
    public DateTime DownloadedAt { get; set; }
}