using IntegracjaProjekt.Data;
using IntegracjaProjekt.Models;
using IntegracjaProjekt.Services;
using Spectre.Console;

namespace IntegracjaProjekt.UI;

public class UiManager
{
    private readonly DatabaseService _dbService;
    private readonly EurostatApiService _apiService;
    private readonly FileTransferService _fileTransferService;
    private readonly AuthService _authService;
    private readonly SoapApiService _soapService; 
    
    private string _jwtToken = string.Empty;

    public UiManager()
    {
        _dbService = new DatabaseService();
        _apiService = new EurostatApiService();
        _fileTransferService = new FileTransferService();
        _authService = new AuthService();
        _soapService = new SoapApiService();
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
        bool isAuthenticated = false;

        while (!isAuthenticated)
        {
            var username = AnsiConsole.Ask<string>("Podaj [green]login[/]:");
            var password = AnsiConsole.Prompt(
                new TextPrompt<string>("Podaj [red]hasło[/]:").Secret());

            var token = _authService.LoginAndGetToken(username, password);

            if (string.IsNullOrEmpty(token))
            {
                AnsiConsole.MarkupLine("[red]Nieprawidłowy login lub hasło! Spróbuj ponownie.[/]\n");
            }
            else
            {
                _jwtToken = token;
                isAuthenticated = true;
            }
        }
    }

    private async Task MainMenuLoopAsync()
    {
        while (true)
        {
            Console.Clear();
            ShowWelcomeScreen();
            
            string currentUsername = _authService.GetUsernameFromToken(_jwtToken);
            string currentRole = _authService.GetRoleFromToken(_jwtToken);
            
            AnsiConsole.MarkupLine($"Zalogowano jako: [bold green]{currentUsername}[/] (Rola: [blue]{currentRole}[/])");
            
            AnsiConsole.MarkupLine($"[grey]Aktywny token JWT: {_jwtToken.Substring(0, 25)}...[/]\n");

            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[yellow]Wybierz akcję:[/]")
                    .PageSize(10)
                    .AddChoices(new[] {
                        "1. Pobierz nowe dane (REST API) -> Zapisz do DB",
                        "2. Przeglądaj dane z bazy (Tabela)",
                        "3. Generuj WYKRESY (Analiza wizualna)",
                        "4. Eksportuj dane do JSON",
                        "5. Eksportuj dane do XML",
                        "6. Importuj dane z JSON -> Zapisz do DB",
                        "7. Importuj dane z XML -> Zapisz do DB",
                        "8. Sprawdź szczegóły państwa (SOAP API)",
                        "9. Wyloguj i zakończ"
                    }));

            try
            {
                if (action.StartsWith("1")) await HandleDownloadAsync();
                else if (action.StartsWith("2")) await HandleViewDataAsync();
                else if (action.StartsWith("3")) await HandleChartAsync();
                else if (action.StartsWith("4")) await HandleExportJsonAsync();
                else if (action.StartsWith("5")) await HandleExportXmlAsync();
                else if (action.StartsWith("6")) await HandleImportJsonAsync();
                else if (action.StartsWith("7")) await HandleImportXmlAsync();
                else if (action.StartsWith("8")) await HandleSoapRequestAsync(); 
                else if (action.StartsWith("9")) return;
            }
            // ... (reszta bloku catch pozostaje bez zmian)
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[bold red]Wystąpił błąd krytyczny:[/] {ex.Message}");
                WaitForKey();
            }
        }
    }


    private async Task HandleDownloadAsync()
    {
        if (!_authService.IsAdminFromToken(_jwtToken))
        {
            AnsiConsole.MarkupLine("[red]Błąd: Twój token JWT nie posiada uprawnień Admina. Odmowa dostępu.[/]");
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
        
        AnsiConsole.MarkupLine("[green]Zakończono sukcesem! Dane zaktualizowane z API.[/]");
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
        _fileTransferService.ExportToJson(dataToJson);
        AnsiConsole.MarkupLine("[green]Wyeksportowano do pliku raport_eurostat.json[/]");
        WaitForKey();
    }

    private async Task HandleExportXmlAsync()
    {
        var dataToXml = await _dbService.GetExpendituresAsync();
        _fileTransferService.ExportToXml(dataToXml);
        AnsiConsole.MarkupLine("[green]Wyeksportowano do pliku raport_eurostat.xml[/]");
        WaitForKey();
    }

    private async Task HandleImportJsonAsync()
    {
        if (!_authService.IsAdminFromToken(_jwtToken))
        {
            AnsiConsole.MarkupLine("[red]Błąd: Twój token JWT nie posiada uprawnień Admina. Odmowa dostępu.[/]");
            WaitForKey();
            return;
        }

        try
        {
            var importedData = _fileTransferService.ImportFromJson();
            await AnsiConsole.Status().StartAsync("Odtwarzanie bazy z pliku JSON...", async ctx => 
            {
                await _dbService.SaveExpendituresAsync(importedData);
            });
            AnsiConsole.MarkupLine($"[green]Sukces! Zaimportowano {importedData.Count} rekordów z pliku JSON do bazy danych.[/]");
        }
        catch (FileNotFoundException ex)
        {
            AnsiConsole.MarkupLine($"[yellow]{ex.Message}[/]");
        }
        WaitForKey();
    }

    private async Task HandleImportXmlAsync()
    {
        if (!_authService.IsAdminFromToken(_jwtToken))
        {
            AnsiConsole.MarkupLine("[red]Błąd: Twój token JWT nie posiada uprawnień Admina. Odmowa dostępu.[/]");
            WaitForKey();
            return;
        }

        try
        {
            var importedData = _fileTransferService.ImportFromXml();
            await AnsiConsole.Status().StartAsync("Odtwarzanie bazy z pliku XML...", async ctx => 
            {
                await _dbService.SaveExpendituresAsync(importedData);
            });
            AnsiConsole.MarkupLine($"[green]Sukces! Zaimportowano {importedData.Count} rekordów z pliku XML do bazy danych.[/]");
        }
        catch (FileNotFoundException ex)
        {
            AnsiConsole.MarkupLine($"[yellow]{ex.Message}[/]");
        }
        WaitForKey();
    }
    
    private async Task HandleSoapRequestAsync()
    {
        var countryCode = AnsiConsole.Ask<string>("Podaj 2-literowy [green]kod państwa[/] (np. PL, DE, FR):").ToUpper();

        try
        {
            await AnsiConsole.Status().StartAsync("Łączenie z usługą SOAP...", async ctx =>
            {
                var details = await _soapService.GetCountryDetailsAsync(countryCode);

                var grid = new Grid();
                grid.AddColumn(new GridColumn().NoWrap());
                grid.AddColumn(new GridColumn().Padding(2, 0, 0, 0));
                
                grid.AddRow("[grey]Kraj:[/]", $"[bold green]{details.Name}[/]");
                grid.AddRow("[grey]Stolica:[/]", $"[bold yellow]{details.Capital}[/]");
                grid.AddRow("[grey]Waluta:[/]", $"[bold blue]{details.Currency}[/]");

                var panel = new Panel(grid)
                    .Header($"[bold white] SOAP API: Właściwości {countryCode} [/]")
                    .BorderColor(Color.SteelBlue)
                    .Padding(1, 1, 1, 1);

                AnsiConsole.Write(panel);
            });
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Błąd pobierania danych SOAP:[/] {ex.Message}");
        }

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