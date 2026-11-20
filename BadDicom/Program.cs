using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using BadDicom.Configuration;
using CommandLine;
using DicomTypeTranslation;
using DicomTypeTranslation.TableCreation;
using FAnsi.Discovery;
using FAnsi.Implementation;
using FAnsi.Implementations.MicrosoftSQL;
using FAnsi.Implementations.MySql;
using FAnsi.Implementations.Oracle;
using FAnsi.Implementations.PostgreSql;
using YamlDotNet.Serialization;
using YamlException = YamlDotNet.Core.YamlException;

namespace BadDicom;

internal sealed class Program
{
    private static int _returnCode;
    public const string ConfigFile = "./BadDicom.yaml";

    public static int Main(string[] args)
    {
        _returnCode = 0;

        Parser.Default.ParseArguments<ProgramOptions>(args)
            .WithParsed(RunOptionsAndReturnExitCode)
            .WithNotParsed(HandleParseError);


        return _returnCode;
    }

    private static void HandleParseError(IEnumerable<Error> errs)
    {
        // if user wants help then return exit code 0 otherwise return a failed to parse error code
        _returnCode = errs.Any(e => e.Tag == ErrorType.HelpRequestedError) ? 0 : 500;
    }

    private static void RunOptionsAndReturnExitCode(ProgramOptions opts)
    {

        if (opts.NumberOfPatients <= 0)
            opts.NumberOfPatients = 500;
        if (opts.NumberOfStudies <= 0)
            opts.NumberOfStudies = 2000;


        Config? config = null;
        var configured = true;

        // Read the file rather than asking File.Exists first: File.Exists also returns false for a file the
        // caller isn't allowed to read, which would silently run without the configuration
        try
        {
            var yaml = File.ReadAllText(ConfigFile);
            var d = new StaticDeserializerBuilder(new ConfigContext()).Build();
            config = d.Deserialize<Config>(yaml);
        }
        // Only a missing file means there is no configuration
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            configured = false;
        }
        // The file can't be read, or isn't valid YAML for Config (YamlDotNet wraps every other
        // deserialization failure in a YamlException)
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or YamlException)
        {
            Console.WriteLine($"Error deserializing '{ConfigFile}'{Environment.NewLine}{e}");
            _returnCode = -1;
            return;
        }

        if (configured)
        {
            // A file that is empty, or holds only comments, deserializes to null
            if (config == null)
            {
                Console.WriteLine($"Error deserializing '{ConfigFile}'{Environment.NewLine}The file is empty or holds only comments, so it has no configuration");
                _returnCode = -1;
                return;
            }

            try
            {
                config.UIDs?.Load();
            }
            // A UID list file named in the configuration exists but can't be read
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"Error reading the UID lists named in '{ConfigFile}'{Environment.NewLine}{e}");
                _returnCode = -1;
                return;
            }

            if (config.Database != null)
            {
                try
                {
                    _returnCode = RunDatabaseTarget(config.Database, opts);
                    return;
                }
                catch (Exception e)
                {

                    Console.WriteLine(e);
                    _returnCode = 3;
                    return;
                }
            }
        }

        // Validate Frames option
        if (opts.Frames < 1)
        {
            Console.WriteLine("Error: Frame count must be at least 1");
            _returnCode = -4;
            return;
        }

        if (opts.Frames > 1000)
        {
            Console.WriteLine("Error: Frame count cannot exceed 1000 (memory constraints)");
            _returnCode = -5;
            return;
        }

        if (opts.csv && opts.Frames > 1)
        {
            Console.WriteLine("Error: Cannot use --Frames with --csv (CSV mode doesn't generate images)");
            _returnCode = -6;
            return;
        }

        try
        {
            var identifiers = GetPeople(opts, out var r);
            using var dicomGenerator = GetDataGenerator(opts, r, out var dir);
            Console.WriteLine($"{DateTime.Now} Starting file generation (to {dir?.FullName ?? "/dev/null"})");
            var targetFile = new FileInfo(dir == null ? RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "NUL" : "/dev/null" : Path.Join(dir.FullName, "DicomFiles.csv"));
            dicomGenerator.GenerateTestDataFile(identifiers, targetFile, opts.NumberOfStudies);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            _returnCode = 2;
            return;
        }

        Console.WriteLine($"{DateTime.Now} Finished");

        _returnCode = 0;
    }

    private static DicomDataGenerator GetDataGenerator(ProgramOptions opts, Random r, out DirectoryInfo? dir)
    {
        //Generate the dicom files (of the modalities that the user requested)
        var modalities = string.IsNullOrWhiteSpace(opts.Modalities) ? Array.Empty<string>() : opts.Modalities.Split(",");

        dir = opts.OutputDirectory?.Equals("/dev/null", StringComparison.Ordinal) != false ? null : Directory.CreateDirectory(opts.OutputDirectory);
        return new DicomDataGenerator(r, opts.OutputDirectory, modalities)
        {
            NoPixels = opts.NoPixels,
            Anonymise = opts.Anonymise,
            Layout = opts.Layout,
            MaximumImages = opts.MaximumImages,
            Csv = opts.csv,
            NumberOfFrames = opts.Frames
        };
    }

    private static PersonCollection GetPeople(ProgramOptions opts, out Random r)
    {
        r = opts.Seed == -1 ? new Random() : new Random(opts.Seed);

        //create a cohort of people
        PersonCollection identifiers = new PersonCollection();
        identifiers.GeneratePeople(opts.NumberOfPatients, r);

        return identifiers;
    }

    private static int RunDatabaseTarget(TargetDatabase configDatabase, ProgramOptions opts)
    {
        var batchSize = Math.Max(1, configDatabase.Batches);

        //if we are going into a database we definitely do not need pixels!
        opts.NoPixels = true;


        var swTotal = Stopwatch.StartNew();
        const string neverDistinct = "SOPInstanceUID";

        if (!File.Exists(configDatabase.Template))
        {
            Console.WriteLine($"Listed template file '{configDatabase.Template}' does not exist");
            return -1;
        }

        ImageTableTemplateCollection? template;
        try
        {
            template = ImageTableTemplateCollection.LoadFrom(File.ReadAllText(configDatabase.Template));
        }
        // The file can't be read, or isn't valid YAML for a template (YamlDotNet wraps every other
        // deserialization failure in a YamlException)
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or YamlException)
        {
            Console.WriteLine($"Error reading yaml from '{configDatabase.Template}'{Environment.NewLine}{e}");
            return -2;
        }

        // A template that is empty, or holds only comments, deserializes to null, and one with an empty Tables
        // entry has a null table list
        if (template?.Tables == null)
        {
            Console.WriteLine($"Error reading yaml from '{configDatabase.Template}'{Environment.NewLine}The file is empty, holds only comments or has no Tables, so it lists no tables");
            return -2;
        }

        MySqlImplementation.EnsureLoaded();
        PostgreSqlImplementation.EnsureLoaded();
        OracleImplementation.EnsureLoaded();
        MicrosoftSQLImplementation.EnsureLoaded();

        var server = new DiscoveredServer(configDatabase.ConnectionString, configDatabase.DatabaseType);

        try
        {
            server.TestConnection();
        }
        catch (Exception e)
        {
            Console.WriteLine($"Could not reach target server '{server.Name}'");
            Console.WriteLine(e);
            return -2;
        }

        if (string.IsNullOrWhiteSpace(configDatabase.DatabaseName))
        {
            Console.WriteLine("Database name must be specified in configuration");
            return -3;
        }

        var db = server.ExpectDatabase(configDatabase.DatabaseName);

        if (!db.Exists())
        {
            Console.WriteLine($"Creating Database '{db.GetRuntimeName()}'");
            db.Create();
            Console.WriteLine("Database Created");
        }
        else
        {
            Console.WriteLine($"Found Database '{db.GetRuntimeName()}'");
        }

        var creator = new ImagingTableCreation(db.Server.GetQuerySyntaxHelper());

        Console.WriteLine($"Image template contained schemas for {template.Tables.Count} tables.  Looking for existing tables..");

        //setting up bulk inserters
        var tables = new DiscoveredTable[template.Tables.Count];
        var batches = PerBatch<DataTable>(batchSize, template.Tables.Count);
        var uploaders = PerBatch<IBulkCopy>(batchSize, template.Tables.Count);
        var pks = new string?[template.Tables.Count];

        for (var i = 0; i < template.Tables.Count; i++)
        {
            var tableSchema = template.Tables[i];
            var tbl = db.ExpectTable(tableSchema.TableName);
            tables[i] = tbl;

            if (configDatabase.MakeDistinct)
            {
                var col = tableSchema.Columns.Where(c => c.IsPrimaryKey).ToArray();

                if (col.Length > 1)
                    Console.WriteLine("MakeDistinct only works with single column primary keys e.g. StudyInstanceUID / SeriesInstanceUID");

                pks[i] = col.SingleOrDefault()?.ColumnName;

                if (pks[i] != null)
                {
                    //if it is sop instance uid then we shouldn't be trying to deduplicate
                    if (string.Equals(pks[i], neverDistinct, StringComparison.OrdinalIgnoreCase))
                        pks[i] = null;
                    else
                    {
                        //we will make this a primary key later on
                        var pkIndex = Array.FindIndex(tableSchema.Columns, c => c.IsPrimaryKey);
                        tableSchema.Columns[pkIndex] = tableSchema.Columns[pkIndex] with { IsPrimaryKey = false };
                        Console.WriteLine($"MakeDistinct will apply to '{pks[i]}' on '{tbl.GetFullyQualifiedName()}'");
                    }
                }
            }

            var create = true;

            if (tbl.Exists())
            {
                if (configDatabase.DropTables)
                {
                    Console.WriteLine($"Dropping existing table '{tbl.GetFullyQualifiedName()}'");
                    tbl.Drop();
                }
                else
                {
                    Console.WriteLine($"Table '{tbl.GetFullyQualifiedName()}' already existed (so will not be created)");
                    create = false;
                }
            }

            if (create)
            {
                Console.WriteLine($"About to create '{tbl.GetFullyQualifiedName()}'");
                creator.CreateTable(tbl, tableSchema);
                Console.WriteLine($"Successfully created create '{tbl.GetFullyQualifiedName()}'");
            }

            Console.WriteLine($"Creating uploader for '{tbl.GetRuntimeName()}''");

            for (var j = 0; j < batchSize; j++)
            {
                //fetch schema
                var dt = tbl.GetDataTable();
                dt.Rows.Clear();

                batches[j][i] = dt;
                uploaders[j][i] = tbl.BeginBulkInsert(CultureInfo.InvariantCulture);
            }
        }
        var identifiers = GetPeople(opts, out var r);

        Parallel.For(0, batchSize, i => RunBatch(identifiers, opts, r, batches[i], uploaders[i]));

        swTotal.Stop();

        MakeTablesDistinct(tables, pks);

        Console.WriteLine("Final Row Counts:");

        foreach (var t in tables)
            Console.WriteLine($"{t.GetFullyQualifiedName()}: {t.GetRowCount():0,0}");

        Console.WriteLine($"Total Running Time:{swTotal.Elapsed}");
        return 0;
    }

    /// <summary>
    /// Returns an array of <paramref name="tableCount"/> elements for each of the <paramref name="batchSize"/> batches
    /// </summary>
    private static T[][] PerBatch<T>(int batchSize, int tableCount)
    {
        var perBatch = new T[batchSize][];

        for (var i = 0; i < perBatch.Length; i++)
            perBatch[i] = new T[tableCount];

        return perBatch;
    }

    /// <summary>
    /// Deduplicates each table that has a column in <paramref name="pks"/>, then makes that column its primary key
    /// </summary>
    private static void MakeTablesDistinct(DiscoveredTable[] tables, string?[] pks)
    {
        for (var i = 0; i < tables.Length; i++)
        {
            var pk = pks[i];
            if (pk == null)
                continue;

            Console.WriteLine($"{DateTime.Now} Making table '{tables[i]}' distinct (this may take a long time)");
            var tbl = tables[i];
            tbl.MakeDistinct(500000000);

            Console.WriteLine($"{DateTime.Now} Creating primary key on '{tables[i]}' of '{pk}'");
            var pkColumn = tbl.DiscoverColumn(pk);
            if (pkColumn == null)
            {
                Console.WriteLine($"Error: Could not find column '{pk}' in table '{tbl.GetFullyQualifiedName()}'. Skipping primary key creation.");
                continue;
            }
            tbl.CreatePrimaryKey(500000000, pkColumn);
        }
    }

    private static void RunBatch(PersonCollection identifiers, ProgramOptions opts, Random r, DataTable[] batches, IBulkCopy[] uploaders)
    {
        Stopwatch swGeneration = new();
        Stopwatch swReading = new();
        Stopwatch swUploading = new();

        try
        {
            using var dicomGenerator = GetDataGenerator(opts, r, out _);
            for (var i = 0; i < opts.NumberOfStudies; i++)
            {
                swGeneration.Start();

                var p = identifiers.People[r.Next(identifiers.People.Length)];
                var ds = dicomGenerator.GenerateStudyImages(p, out _);

                swGeneration.Stop();

                foreach (var dataset in ds)
                {
                    var rows = new DataRow[batches.Length];

                    for (var j = 0; j < batches.Length; j++)
                        rows[j] = batches[j].NewRow();

                    swReading.Start();
                    foreach (var item in dataset)
                    {
                        var column = DicomTypeTranslaterReader.GetColumnNameForTag(item.Tag, false);
                        var csharpValue = DicomTypeTranslaterReader.GetCSharpValue(dataset, item);
                        var value = csharpValue == null ? null : DicomTypeTranslater.Flatten(csharpValue);

                        foreach (var row in rows.Where(row => row.Table.Columns.Contains(column)))
                            row[column] = value ?? DBNull.Value;
                    }

                    for (var j = 0; j < batches.Length; j++)
                        batches[j].Rows.Add(rows[j]);

                    swReading.Stop();
                }

                //every 100 and last batch
                if (i % 100 != 0 && i != opts.NumberOfStudies - 1)
                    continue;
                {
                    swUploading.Start();
                    for (var j = 0; j < uploaders.Length; j++)
                    {
                        uploaders[j].Upload(batches[j]);
                        batches[j].Rows.Clear();
                    }
                    swUploading.Stop();
                    Console.WriteLine($"{DateTime.Now} Done {i} studies");
                }

            }
        }
        finally
        {
            for (var i = 0; i < uploaders.Length; i++)
            {
                uploaders[i].Dispose();
                batches[i].Dispose();
            }
        }

        Console.WriteLine($"Total time Generating Dicoms:{swGeneration.Elapsed}");
        Console.WriteLine($"Total time Reading Dicoms:{swReading.Elapsed}");
        Console.WriteLine($"Total time Uploading Records:{swUploading.Elapsed}");

    }
}
