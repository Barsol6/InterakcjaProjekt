using IntegracjaProjekt.Data;
using IntegracjaProjekt.Models;
using IntegracjaProjekt.Services;
using Spectre.Console;

namespace IntegracjaProjekt.UI;

public class UiManager
{
    private readonly DatabaseService _dbService;
    private readonly EurostatApiService _apiService;
    private readonly ExportService _exportService;

    public UiManager()
    {
        _dbService = new DatabaseService();
        _apiService = new EurostatApiService();
        _exportService = new ExportService();
    }

    public async Task StartAsync()
    {
        ShowWelcomeScreen();
        PerformLogin();
        await MainMenuLoopAsync();
    }

    private void ShowWelcomeScreen()
    {
        AnsiConsole.Write(
            new FigletText("Eurostat API")
                .Centered()
                .Color(Color.Blue));
        AnsiConsole.Write(
            new Align(
                new Markup("[bold grey]Wydatki Wojskowe - System Integracyjny[/]\n"), 
                HorizontalAlignment.Center
            ));
    }

    private void PerformLogin()
    {
        var authService = new AuthService();
        bool isAuthenticated = false;

        while (!isAuthenticated)
        {
            var username = AnsiConsole.Ask<string>("Podaj [green]login[/]:");
            var password = AnsiConsole.Prompt(
                new TextPrompt<string>("Podaj [red]hasło[/]:").Secret());

            isAuthenticated = authService.Login(username, password);

            if (!isAuthenticated)
            {
                AnsiConsole.MarkupLine("[red]Nieprawidłowy login lub hasło! Spróbuj ponownie.[/]\n");
            }
        }
    }

    private async Task MainMenuLoopAsync()
    {
        while (true)
        {
            Console.Clear();
            ShowWelcomeScreen();
            AnsiConsole.MarkupLine($"Zalogowano jako: [bold green]{AuthService.CurrentUser?.Username}[/] (Rola: [blue]{AuthService.CurrentUser?.Role}[/])\n");

            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[yellow]Wybierz akcję:[/]")
                    .PageSize(7)
                    .AddChoices(new[] {
                        "1. Pobierz nowe dane (REST API) -> Zapisz do DB",
                        "2. Przeglądaj dane z bazy (Tabela)",
                        "3. Generuj WYKRESY (Analiza wizualna)", // <-- NOWA OPCJA
                        "4. Eksportuj dane do JSON",
                        "5. Eksportuj dane do XML",
                        "6. Wyloguj i zakończ"
                    }));

            try
            {
                if (action.StartsWith("1")) await HandleDownloadAsync();
                else if (action.StartsWith("2")) await HandleViewDataAsync();
                else if (action.StartsWith("3")) await HandleChartAsync(); // <-- NOWA AKCJA
                else if (action.StartsWith("4")) await HandleExportJsonAsync();
                else if (action.StartsWith("5")) await HandleExportXmlAsync();
                else if (action.StartsWith("6")) return;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[bold red]Wystąpił błąd krytyczny:[/] {ex.Message}");
                WaitForKey();
            }
        }
    }


    private async Task HandleDownloadAsync()
    {
        if (!AuthService.IsAdmin())
        {
            AnsiConsole.MarkupLine("[red]Błąd: Brak uprawnień. Tylko Admin może pobierać dane z REST API.[/]");
            WaitForKey();
            return;
        }

        await AnsiConsole.Status()
            .StartAsync("Pobieranie i zapisywanie danych z Eurostatu...", async ctx => 
            {
                var newData = await _apiService.FetchMilitaryDataAsync();
                ctx.Status("Zapis do bazy (Transakcja Serializable)...");
                await _dbService.SaveExpendituresAsync(newData);
            });
        
        AnsiConsole.MarkupLine("[green]Zakończono sukcesem! Dane zaktualizowane.[/]");
        WaitForKey();
    }

    private async Task HandleViewDataAsync()
    {
        var data = await _dbService.GetExpendituresAsync();
        if (!data.Any())
        {
            AnsiConsole.MarkupLine("[yellow]Brak danych w bazie. Użyj opcji pobierania.[/]");
        }
        else
        {
            RenderDataTable(data);
        }
        WaitForKey();
    }
    
    private async Task HandleChartAsync()
    {
        var allData = await _dbService.GetExpendituresAsync();
        if (!allData.Any())
        {
            AnsiConsole.MarkupLine("[yellow]Brak danych do wygenerowania wykresu. Pobierz dane z API.[/]");
            WaitForKey();
            return;
        }

        var chartType = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[yellow]Jaki rodzaj analizy chcesz przeprowadzić?[/]")
                .AddChoices(new[] {
                    "A. Historia wydatków na przestrzeni lat (Dla 1 kraju)",
                    "B. Porównanie wielu krajów w jednym roku",
                    "C. Powrót"
                }));

        if (chartType.StartsWith("C")) return;

        if (chartType.StartsWith("A"))
        {
            var countries = allData.Select(d => d.CountryCode).Distinct().OrderBy(c => c).ToList();
            var selectedCountry = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Wybierz [green]kraj[/] (użyj strzałek):")
                    .PageSize(10)
                    .AddChoices(countries));

            var countryData = allData.Where(d => d.CountryCode == selectedCountry).OrderBy(d => d.Year).ToList();

            var chart = new BarChart()
                .Width(80)
                .Label($"[green bold]Wydatki {selectedCountry} (% PKB) na przestrzeni lat[/]")
                .CenterLabel();

            foreach (var item in countryData)
            {
                chart.AddItem(item.Year.ToString(), Math.Round((double)item.PercentageOfGdp, 2), Color.SteelBlue);
            }

            AnsiConsole.Write(chart);
        }
        else if (chartType.StartsWith("B"))
        {
            var years = allData.Select(d => d.Year.ToString()).Distinct().OrderByDescending(y => y).ToList();
            var selectedYearStr = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Wybierz [green]rok[/] do porównania:")
                    .PageSize(10)
                    .AddChoices(years));

            int selectedYear = int.Parse(selectedYearStr);
            var countriesInYear = allData.Where(d => d.Year == selectedYear).Select(d => d.CountryCode).Distinct().OrderBy(c => c).ToList();

            var selectedCountries = AnsiConsole.Prompt(
                new MultiSelectionPrompt<string>()
                    .Title($"Wybierz [green]kraje[/] do porównania (Spacja zaznacza, Enter zatwierdza):")
                    .PageSize(15)
                    .Required()
                    .InstructionsText("[grey](Spacja przełącza wybór, Enter zatwierdza zaznaczone)[/]")
                    .AddChoices(countriesInYear));

            var chartData = allData.Where(d => d.Year == selectedYear && selectedCountries.Contains(d.CountryCode)).OrderByDescending(d => d.PercentageOfGdp).ToList();

            var chart = new BarChart()
                .Width(80)
                .Label($"[green bold]Porównanie wydatków (% PKB) w {selectedYear} roku[/]")
                .CenterLabel();

            var colors = new[] { Color.Red, Color.Green, Color.Yellow, Color.Magenta, Color.Cyan, Color.DarkOrange };
            int colorIdx = 0;

            foreach (var item in chartData)
            {
                chart.AddItem(item.CountryCode, Math.Round((double)item.PercentageOfGdp, 2), colors[colorIdx % colors.Length]);
                colorIdx++;
            }

            AnsiConsole.Write(chart);
        }

        WaitForKey();
    }

    private async Task HandleExportJsonAsync()
    {
        var dataToJson = await _dbService.GetExpendituresAsync();
        _exportService.ExportToJson(dataToJson);
        AnsiConsole.MarkupLine("[green]Wyeksportowano do pliku raport_eurostat.json[/]");
        WaitForKey();
    }

    private async Task HandleExportXmlAsync()
    {
        var dataToXml = await _dbService.GetExpendituresAsync();
        _exportService.ExportToXml(dataToXml);
        AnsiConsole.MarkupLine("[green]Wyeksportowano do pliku raport_eurostat.xml[/]");
        WaitForKey();
    }


    private void RenderDataTable(List<MilitaryExpenditure> data)
    {
        var table = new Table();
        table.Border = TableBorder.Rounded;
        table.AddColumn("[yellow]ID[/]");
        table.AddColumn("[yellow]Kraj (Kod)[/]");
        table.AddColumn(new TableColumn("[yellow]Rok[/]").Centered());
        table.AddColumn(new TableColumn("[yellow]Wydatki (% PKB)[/]").RightAligned());

        foreach (var item in data.Take(20))
        {
            table.AddRow(item.Id.ToString(), item.CountryCode, item.Year.ToString(), $"[green]{item.PercentageOfGdp:0.00}%[/]");
        }

        AnsiConsole.Write(table);
        if (data.Count > 20)
        {
            AnsiConsole.MarkupLine($"[grey]... i {data.Count - 20} więcej w bazie.[/]");
        }
    }

    private void WaitForKey()
    {
        AnsiConsole.MarkupLine("\n[grey]Naciśnij dowolny klawisz, aby powrócić do menu...[/]");
        Console.ReadKey(true);
    }
}