using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;

namespace DBCodeGenerator
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            DisplayAppDescription();

            Console.WriteLine("Testing database connection...");
            string connStr = ConfigurationManager.ConnectionStrings["testdb"].ConnectionString;

            DbConnection.TestConnection(connStr);

            var allTables = DbConnection.QueryAllUserTables(connStr);

            Dictionary<string, string> dictConfigs = GetAppConfigs();
            string currentPath = Directory.GetCurrentDirectory();
            string path = Path.Combine(currentPath, Constants.OutputPath);

            Console.WriteLine($"Current Path: {path}");

            DbConnection.StartCodeGenerationProcess(dictConfigs, connStr, path);

            Console.WriteLine("Done!!!");
        }

        private static void DisplayAppDescription()
        {
            string cLine = new string('=', 100);
            Console.WriteLine(cLine);
            Console.WriteLine("Database Code Generator");
            Console.WriteLine(cLine);
            Console.WriteLine("This generates code based on a list of table names from the database stored in file, that is configured in the app.config file.");
            DisplayBox("DTO Class files");
            Console.WriteLine("From the table names this generates code files that have the DTO classes with the properties that match the column names.");
            DisplayBox("Enums of Column Names");
            Console.WriteLine("Enums are generated for the column names to be used in the building of the SQL queries for the tables using a template file configured in the app.config file.");

            DisplayBox("Table Name Constants");
            Console.WriteLine("Constants are created for the table names to be used in the building of the SQL queries for the database using a template file configured in the app.config file..");

            DisplayBox("Exclude Properties from DTO");
            Console.WriteLine("The Exclude Properties File (configered in the app.config file) list the properties to exclude from the DTO class.");
        }

        private static void DisplayBox(string token)
        {
            int len = token.Length + 2;
            string line = new string('-', len);

            Console.WriteLine("+" + line + "+");
            Console.WriteLine("| " + token + " |");
            Console.WriteLine("+" + line + "+");
        }

        private static Dictionary<string, string> GetAppConfigs()
        {
            Dictionary<string, string> dictConfig = new Dictionary<string, string>();

            dictConfig.Add(Constants.TableListFile_Config, ConfigurationManager.AppSettings[Constants.TableListFile_Config]);
            dictConfig.Add(Constants.ColumnEnumTemplateFile_Config, ConfigurationManager.AppSettings[Constants.ColumnEnumTemplateFile_Config]);
            dictConfig.Add(Constants.DTOTemplateFile_Config, ConfigurationManager.AppSettings[Constants.DTOTemplateFile_Config]);
            dictConfig.Add(Constants.TableNameTemplateFile_Config, ConfigurationManager.AppSettings[Constants.TableNameTemplateFile_Config]);
            dictConfig.Add(Constants.ExcludePropertiesFile_Config, ConfigurationManager.AppSettings[Constants.ExcludePropertiesFile_Config]);

            return dictConfig;
        }
    }
}