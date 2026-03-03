using Oracle.ManagedDataAccess.Client;

var conn = new OracleConnection("User Id=SYSTEM;Password=Oracle123;Data Source=localhost:1521/xe");
try
{
conn.Open();
Console.WriteLine("Connected");
Console.ReadLine();
}
catch (Exception ex)
{

	
}

