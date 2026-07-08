using System.Reflection;

Console.WriteLine($"Arguments: {string.Join(", ", args)}");
if (args.Length >= 2)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] == "--version" && i <= args.Length - 2)
        {
            var version = args[i + 1];
            Console.WriteLine($"Version: {version}");

            var basePath = Assembly.GetAssembly(typeof(Program))!.Location;
            File.WriteAllText($"{basePath}/../../../../version.txt", version);
        }
    }
}
