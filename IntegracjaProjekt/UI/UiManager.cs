using IntegracjaProjekt.Models;
using IntegracjaProjekt.Services;
using Spectre.Console;

namespace IntegracjaProjekt.UI;

public class UiManager
{
    private readonly DatabaseService _dbService;
    private readonly EurostatApiService _apiService;
    private readonly WorldBankApiService _wbApiService;
    private readonly FileTransferService _fileTransferService;
    private readonly AuthService _authService;
    private readonly SoapApiService _soapService;
    
    private string _jwtToken = string.Empty;

    public UiManager()
    {
        _dbService = new DatabaseService();
        _apiService = new EurostatApiService();
        _wbApiService = new WorldBankApiService();
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
            new FigletText("Data Integrator")
                .Centered()
                .Color(Color.Blue));
        AnsiConsole.Write(
            new Align(
                new Markup("[bold grey]Wydatki Wojskowe - Eurostat, World Bank & SOAP API[/]\n"), 
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
                    .Title("[yellow]Wybierz akcję z systemu rozproszonego:[/]")
                    .PageSize(12)
                    .AddChoices(new[] {
                        "1. Pobierz nowe dane (EUROSTAT REST API)",
                        "2. Pobierz nowe dane (WORLD BANK REST API)",
                        "3. Przeglądaj połączone dane z bazy (Tabela)",
                        "4. Generuj WYKRESY (Porównanie Eurostat vs WorldBank)",
                        "5. Eksportuj całą bazę do pliku JSON",
                        "6. Eksportuj całą bazę do pliku XML",
                        "7. Importuj dane z JSON -> Odtwórz w bazie",
                        "8. Importuj dane z XML -> Odtwórz w bazie",
                        "9. Sprawdź szczegóły państwa (SOAP API)",
                        "0. Wyloguj i zakończ"
                    }));

            try
            {
                if (action.StartsWith("1")) await HandleDownloadEurostatAsync();
                else if (action.StartsWith("2")) await HandleDownloadWorldBankAsync();
                else if (action.StartsWith("3")) await HandleViewDataAsync();
                else if (action.StartsWith("4")) await HandleChartAsync();
                else if (action.StartsWith("5")) await HandleExportJsonAsync();
                else if (action.StartsWith("6")) await HandleExportXmlAsync();
                else if (action.StartsWith("7")) await HandleImportJsonAsync();
                else if (action.StartsWith("8")) await HandleImportXmlAsync();
                else if (action.StartsWith("9")) await HandleSoapRequestAsync();
                else if (action.StartsWith("0")) return;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[bold red]Wystąpił błąd krytyczny:[/] {ex.Message}");
                WaitForKey();
            }
        }
    }

    // --- LOGIKA AKCJI MENU ---

    private async Task HandleDownloadEurostatAsync()
    {
        if (!_authService.IsAdminFromToken(_jwtToken))
        {
            AnsiConsole.MarkupLine("[red]Błąd: Twój token JWT nie posiada uprawnień Admina. Odmowa dostępu.[/]");
            WaitForKey();
            return;
        }

        await AnsiConsole.Status()
            .StartAsync("Pobieranie danych z Eurostat REST API...", async ctx => 
            {
                var newData = await _apiService.FetchMilitaryDataAsync();
                ctx.Status("Zapis do bazy (Transakcja Serializable)...");
                await _dbService.SaveExpendituresAsync(newData, "Eurostat");
            });
        
        AnsiConsole.MarkupLine("[green]Zakończono sukcesem! Dane Eurostat zostały zaktualizowane.[/]");
        WaitForKey();
    }

    private async Task HandleDownloadWorldBankAsync()
    {
        if (!_authService.IsAdminFromToken(_jwtToken))
        {
            AnsiConsole.MarkupLine("[red]Błąd: Twój token JWT nie posiada uprawnień Admina. Odmowa dostępu.[/]");
            WaitForKey();
            return;
        }

        await AnsiConsole.Status()
            .StartAsync("Pobieranie danych z World Bank REST API...", async ctx => 
            {
                var newData = await _wbApiService.FetchMilitaryDataAsync();
                ctx.Status("Zapis do bazy (Transakcja Serializable)...");
                await _dbService.SaveExpendituresAsync(newData, "WorldBank");
            });
        
        AnsiConsole.MarkupLine("[green]Zakończono sukcesem! Dane World Bank zostały zaktualizowane.[/]");
        WaitForKey();
    }

    private async Task HandleViewDataAsync()
    {
        var data = await _dbService.GetExpendituresAsync();
        if (!data.Any())
        {
            AnsiConsole.MarkupLine("[yellow]Brak danych w bazie. Użyj opcji 1 lub 2.[/]");
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

        var countries = allData.Select(d => d.CountryCode).Distinct().OrderBy(c => c).ToList();
        var selectedCountry = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Wybierz [green]kraj[/] do analizy historycznej:")
                .PageSize(10)
                .AddChoices(countries));

        var countryData = allData
            .Where(d => d.CountryCode == selectedCountry)
            .OrderBy(d => d.Year)
            .ThenBy(d => d.DataSource)
            .ToList();

        var chart = new BarChart()
            .Width(90)
            .Label($"[green bold]Wydatki {selectedCountry} - Eurostat (Niebieski) vs WorldBank (Różowy)[/]")
            .CenterLabel();

        foreach (var item in countryData)
        {
            Color barColor = item.DataSource == "Eurostat" ? Color.SteelBlue : Color.Fuchsia;
            string shortSource = item.DataSource == "Eurostat" ? "EU" : "WB";
            
            chart.AddItem($"{item.Year} ({shortSource})", Math.Round((double)item.PercentageOfGdp, 2), barColor);
        }

        AnsiConsole.Write(chart);
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
        if (!_authService.IsAdminFromToken(_jwtToken)) { WaitForKey(); return; }

        try
        {
            var importedData = _fileTransferService.ImportFromJson();
            await AnsiConsole.Status().StartAsync("Odtwarzanie bazy z pliku JSON...", async ctx => 
            {
                // Grupowanie pozwala zapisać niezależnie dane z Eurostatu i WorldBanku w osobnych transakcjach
                foreach (var sourceGroup in importedData.GroupBy(d => d.DataSource))
                {
                    await _dbService.SaveExpendituresAsync(sourceGroup.ToList(), sourceGroup.Key);
                }
            });
            AnsiConsole.MarkupLine($"[green]Sukces! Zaimportowano {importedData.Count} rekordów z pliku JSON.[/]");
        }
        catch (FileNotFoundException ex)
        {
            AnsiConsole.MarkupLine($"[yellow]{ex.Message}[/]");
        }
        WaitForKey();
    }

    private async Task HandleImportXmlAsync()
    {
        if (!_authService.IsAdminFromToken(_jwtToken)) { WaitForKey(); return; }

        try
        {
            var importedData = _fileTransferService.ImportFromXml();
            await AnsiConsole.Status().StartAsync("Odtwarzanie bazy z pliku XML...", async ctx => 
            {
                foreach (var sourceGroup in importedData.GroupBy(d => d.DataSource))
                {
                    await _dbService.SaveExpendituresAsync(sourceGroup.ToList(), sourceGroup.Key);
                }
            });
            AnsiConsole.MarkupLine($"[green]Sukces! Zaimportowano {importedData.Count} rekordów z pliku XML.[/]");
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
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("[yellow]ID[/]");
        table.AddColumn("[yellow]Źródło[/]");
        table.AddColumn("[yellow]Kraj[/]");
        table.AddColumn(new TableColumn("[yellow]Rok[/]").Centered());
        table.AddColumn(new TableColumn("[yellow]Wydatki (% PKB)[/]").RightAligned());

        // Pokazujemy do 25 rekordów
        foreach (var item in data.Take(25))
        {
            string sourceColor = item.DataSource == "Eurostat" ? "blue" : "fuchsia";
            table.AddRow(
                item.Id.ToString(), 
                $"[{sourceColor}]{item.DataSource}[/]", 
                item.CountryCode, 
                item.Year.ToString(), 
                $"[green]{item.PercentageOfGdp:0.00}%[/]"
            );
        }

        AnsiConsole.Write(table);
        if (data.Count > 25)
        {
            AnsiConsole.MarkupLine($"[grey]... i {data.Count - 25} więcej w bazie.[/]");
        }
    }

    private void WaitForKey()
    {
        AnsiConsole.MarkupLine("\n[grey]Naciśnij dowolny klawisz, aby powrócić do menu...[/]");
        Console.ReadKey(true);
    }
}