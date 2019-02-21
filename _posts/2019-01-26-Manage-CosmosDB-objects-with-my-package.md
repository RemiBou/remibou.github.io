# Manage CosmosDB objects (Stored Procedure, Functions, Triggers ...) with this Nuget package

In my Toss project I decided to use CosmosDB as the main data store. Document oriented Database are fun to work with as have to change the way you see data and processing comapred to relationnal database. NoSQL database are often badly framed as "schemaless", (but there isn't such thing as schemaless)[https://martinfowler.com/articles/schemaless/], the schema is just defined elsewhere : not on the database system but on your code.

There is 2 problem with this approach for the developer :
- the persistence is managed by still the database so you have to think how to change the saved data when you are renaming a field for example
- there is some specific database object such as functions and triggers that need to be defined 

That's why I created this package, it'll help you to manage your database object and schema evolution along with your on your repository and it'll be applied when you want (on app startup mostly).

## Reading he embedded ressources

I chose to use embedded js file in assembly for defining objets definitions :
- Because they are defined in javascript, it's better to use js file, the IDE will help the developer
- If it's embedded then the package user won't have to think about securing the folder containing the definitions

Reading the embedded ressource is fairly simple in C# / netstandard :

```cs
//read all the migration embed in CosmosDB/Migrations
var ressources = migrationAssembly.GetManifestResourceNames()
    .Where(r => r.Contains(".CosmosDB.Migrations.") && r.EndsWith(".js"))
    .OrderBy(r => r)
    .ToList();
//for each migration
foreach (var migration in ressources)
{
    string migrationContent;
    using (var stream = migrationAssembly.GetManifestResourceStream(migration))
    {
        using (var reader = new StreamReader(stream))
        {
            migrationContent = await reader.ReadToEndAsync();
        }
    }
    // do something
}
```

- migrationAssembly is sent as a parameter of my method, so the user can add its migration to the app ressources or to a library ressources.
- I don't really think about performance here as this code is supposed to be ran only once per app lifecycle, so I prefer to keep it clear.

## Applying the migrations

I decided to implement the strategy pattern : for each type of objects there is one strategy, so user will be able to implement their own strategies.

This piece of code goes on the "//do something" from the previous code sample :

```cs
var parsedMigration = new ParsedMigrationName(migration);
var strategy = strategies.FirstOrDefault(s => s.Handle(parsedMigration));
if (strategy == null)
{
    throw new InvalidOperationException(string.Format("No strategy found for migration '{0}", migration));
}

await client.CreateDatabaseIfNotExistsAsync(parsedMigration.DataBase);
if (parsedMigration.Collection != null)
{
    await client.CreateDocumentCollectionIfNotExistsAsync(UriFactory.CreateDatabaseUri(parsedMigration.DataBase.Id), parsedMigration.Collection);
}

await strategy.ApplyMigrationAsync(client, parsedMigration, migrationContent);
```

- Parsed migration is the class that check and read the ressource name. Each ressource must respect the documented convention : the "Test.js" file located in "CosmosDB/Migrations/TestDataBase/StoredProcedure/" is the content of the stored procedure called "Test" located in the database "TestDataBase" and in the collection
- Here I create the required database and collection

## Strategy implementation

For each kind of object I want to manager I have to create an implementation of the strategy, here is the one for the triggers :

```cs   
internal class TriggerMigrationStrategy : IMigrationStrategy
{
    public async Task ApplyMigrationAsync(IDocumentClient client, ParsedMigrationName migration, string content)
    {
        var nameSplit = migration.Name.Split('-');

        Trigger trigger = new Trigger()
        {

            Body = content,
            Id = nameSplit[2],
            TriggerOperation = (TriggerOperation)Enum.Parse(typeof(TriggerOperation), nameSplit[1]),
            TriggerType = (TriggerType)Enum.Parse(typeof(TriggerType), nameSplit[0])
        };
        await client.UpsertTriggerAsync(
                        UriFactory.CreateDocumentCollectionUri(migration.DataBase.Id, migration.Collection.Id),
                        trigger);
    }

    public bool Handle(ParsedMigrationName migration)
    {
        return migration.Type == "Trigger";
    }
}
```