using CommandLine;
using ShellProgressBar;
using StorageV1ToStorageV2;
using System;
using System.Collections.Generic;
using System.Linq;

var result = Parser.Default.ParseArguments<Options>(args);
var serverName = "localhost";
result.WithParsed(options => serverName = options.Server);
var accounts = await serverName.GetV1StorageAccounts();
if (accounts.Any())
{
    Console.WriteLine("Elija las cuenta a Importar (Separadas por ','):");
    var i = 1;
    var accountOptions = new Dictionary<int, StorageV1ToStorageV2.V1.StorageAccount>();
    foreach (var account in accounts)
    {
        Console.WriteLine($"{i} ---> Cuenta [{account.Nombre}]");
        accountOptions.Add(i, account);
        i++;
    }
    Console.WriteLine($"{i++} ---> Todas");

    var selected = Console.ReadLine();
    if (string.IsNullOrEmpty(selected) || string.IsNullOrEmpty(selected.Trim()))
    {
        Console.WriteLine("No se seleciono ninguna cuenta");
        return;
    }

    var selectedAccounts = selected
        .Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(x => int.Parse(x.Trim()))
        .Where(x => accountOptions.ContainsKey(x))
        .Select(x => accountOptions[x]);

    if (selected.Contains((i - 1).ToString())) selectedAccounts = accountOptions.Select(x => x.Value);



    Console.WriteLine("Se va a realizar la migracion de las siguientes cuentas:");
    foreach (var account in selectedAccounts)
    {
        Console.WriteLine(account.Nombre);
    }

    Console.WriteLine("Presione {ENTER} para iniciar la migracion.");
    Console.ReadLine();

    var options = new ProgressBarOptions
    {
        ForegroundColor = ConsoleColor.Yellow,
        BackgroundColor = ConsoleColor.DarkYellow,
        ProgressCharacter = '─'
    };

    using var pbar = new ProgressBar(selectedAccounts.Count(), "Migrando", options);

    Console.Clear();

    foreach (var account in selectedAccounts)
    {
        pbar.WriteLine($"Migrando {account.Nombre}");
        await account.Migrate(pbar,serverName);
        pbar.Tick();
    }


    Console.WriteLine(selected);
    Console.ReadLine();
}
else
{
    Console.WriteLine("No se encontraron cuentas de almacenamiento en el servidor.");
}
