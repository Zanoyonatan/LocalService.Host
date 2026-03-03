using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using System.Text;
using System.Text.Json;

namespace ElasticSearchApp;

public partial class MainForm : Form
{
    private readonly string _connectionString;
    private readonly HttpClient _httpClient = new()
    {
        BaseAddress = new Uri("http://localhost:9200/")
    };

    public MainForm()
    {
        InitializeComponent();
        
        // ???? Oracle Client
        try
        {
            var oracleVersion = Oracle.ManagedDataAccess.Client.OracleConnection.GetConnectionInfo(Oracle.ManagedDataAccess.Client.ConnectionInfoType.CustomInfo, false);
            System.Diagnostics.Debug.WriteLine($"? Oracle Client ????");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"? ???? ?Oracle Client: {ex.Message}");
        }
        
        var config = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        _connectionString = config["ConnectionString"];

        // ???? ?? Connection String ????? ???
        if (string.IsNullOrEmpty(_connectionString))
        {
            MessageBox.Show("?????: Connection String ?? ???? ?- appsettings.json");
        }
        else
        {
            var connInfo = System.Text.RegularExpressions.Regex.Replace(_connectionString, @"Password=[^;]*", "Password=****");
            System.Diagnostics.Debug.WriteLine($"? Connection String loaded: {connInfo}");
            System.Diagnostics.Debug.WriteLine($"? App Directory: {AppDomain.CurrentDomain.BaseDirectory}");
            System.Diagnostics.Debug.WriteLine($"? Platform: {(Environment.Is64BitProcess ? "64-bit" : "32-bit")}");
        }
    }

    public void MainForm_Load(object sender, EventArgs e)
    {
        gridResults.DataSource = null;

        gridResults.AutoGenerateColumns = false;

        gridResults.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = "firstName",
            HeaderText = "?? ????",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });

        gridResults.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = "lastName",
            HeaderText = "?? ?????",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });
    }

    private async void btnSearch_Click(object sender, EventArgs e)
    {
        try
        {
            var firstName = txtFirstName.Text.Trim();
            var lastName = txtLastName.Text.Trim();

            if (string.IsNullOrEmpty(firstName) && string.IsNullOrEmpty(lastName))
            {
                MessageBox.Show("?? ????? ????? ??? ???");
                return;
            }

            var mustList = new List<object>();

            if (!string.IsNullOrEmpty(firstName))
            {
                mustList.Add(new
                {
                    multi_match = new
                    {
                        query = firstName,
                        fields = new[] { "firstName^2" },
                        fuzziness = 1
                    }
                });
            }

            if (!string.IsNullOrEmpty(lastName))
            {
                mustList.Add(new
                {
                    multi_match = new
                    {
                        query = lastName,
                        fields = new[] { "lastName^2" },
                        fuzziness = 1
                    }
                });
            }

            var queryObject = new
            {
                query = new
                {
                    @bool = new
                    {
                        must = mustList
                    }
                },
                collapse = new
                {
                    field = "tz"
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(queryObject),
                Encoding.UTF8,
                "application/json");

            System.Diagnostics.Debug.WriteLine("?????? ????? ???????...");
            var response = await _httpClient.PostAsync("test-index/_search", content);

            if (!response.IsSuccessStatusCode)
            {
                MessageBox.Show($"Search failed: {response.StatusCode} - {response.ReasonPhrase}");
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"?????? ????? ???????: {json.Substring(0, Math.Min(200, json.Length))}...");

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var result = JsonSerializer.Deserialize<ElasticResponse>(json, options);
            var tzList = result?.hits?.hits?
                .Select(h => h._source.Tz)
                .Distinct()
                .ToList();

            if (tzList == null || tzList.Count == 0)
            {
                MessageBox.Show("?? ????? ??????");
                gridResults.DataSource = null;
                return;
            }

            System.Diagnostics.Debug.WriteLine($"?????? ?????? ?????? ?? {tzList.Count} ?????? ???????...");
            var oracleResults = await GetPersonsFromOracleAsync(tzList);

            System.Diagnostics.Debug.WriteLine($"?????? {oracleResults.Count} ????? ??????");
            gridResults.DataSource = oracleResults;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"?????: {ex.GetType().Name}\n{ex.Message}\n\n{ex.StackTrace}");
            System.Diagnostics.Debug.WriteLine($"Search Error: {ex}");
        }
    }

    private async void btnAdd_Click(object sender, EventArgs e)
    {
        var firstName = txtFirstName.Text.Trim();
        var lastName = txtLastName.Text.Trim();

        if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName))
        {
            MessageBox.Show("?? ????? ?? ???? ??? ????? ??????");
            return;
        }

        var person = new
        {
            firstName,
            lastName
        };

        var content = new StringContent(
            JsonSerializer.Serialize(person),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync("persons/_doc", content);

        if (response.IsSuccessStatusCode)
        {
            MessageBox.Show("?????? ????? ?????? (??????? ?????? ??? ?????? ?????)");
            txtFirstName.Clear();
            txtLastName.Clear();
        }
        else
        {
            MessageBox.Show("????? ??????");
        }
    }

    private async Task<List<Person>> GetPersonsFromOracleAsync(List<long> tzList)
    {
        var persons = new List<Person>();

        if (tzList == null || tzList.Count == 0)
            return persons;   
        
        // ???? ?? Connection String ????
        if (string.IsNullOrEmpty(_connectionString))
        {
            MessageBox.Show("?????: Connection String ?? ????");
            return persons;
        }

        // ???? ?? Connection String ????? ????? ?????? (??? ??????)
        var connInfo = System.Text.RegularExpressions.Regex.Replace(_connectionString, @"Password=[^;]*", "Password=****");
        System.Diagnostics.Debug.WriteLine($"Connection String: {connInfo}");

        using var conn = new OracleConnection(_connectionString);

        try
        {
            // ??? ????? ???????? ???? ??? ????
            conn.Open();
            System.Diagnostics.Debug.WriteLine("????? ?????? ?????!");
        }
        catch (OracleException ex)
        {
            MessageBox.Show($"????? ?????? ??????:\n\n??? ?????: {ex.Number}\n?????: {ex.Message}\n\nOracleErrorCollection: {string.Join(", ", ex.Errors.Cast<OracleError>().Select(e => e.Message))}");
            System.Diagnostics.Debug.WriteLine($"OracleException: {ex}");
            return persons;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"????? ?????? ??????:\n\n??? ?????: {ex.GetType().Name}\n?????: {ex.Message}\n\nStackTrace: {ex.StackTrace}");
            System.Diagnostics.Debug.WriteLine($"Exception: {ex}");
            return persons;
        }

        var query = @"
        SELECT TZ, FIRST_NAME, LAST_NAME, CITY, BIRTH_DATE
        FROM PERSONS
        WHERE TZ IN (SELECT COLUMN_VALUE FROM TABLE(:tzArray))";

        using var cmd = new OracleCommand(query, conn);
        cmd.CommandTimeout = 30; // timeout ?? 30 ?????

        var tzArray = new OracleParameter
        {
            ParameterName = "tzArray",
            OracleDbType = OracleDbType.Int64,
            CollectionType = OracleCollectionType.PLSQLAssociativeArray,
            Value = tzList.ToArray()
        };

        cmd.Parameters.Add(tzArray);

        try
        {
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                persons.Add(new Person
                {
                    Tz = reader.GetInt64(reader.GetOrdinal("TZ")),
                    FirstName = reader["FIRST_NAME"]?.ToString(),
                    LastName = reader["LAST_NAME"]?.ToString(),
                    City = reader["CITY"]?.ToString(),
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"????? ?????? ???????:\n{ex.GetType().Name}: {ex.Message}");
        }

        return persons;
    }
}
