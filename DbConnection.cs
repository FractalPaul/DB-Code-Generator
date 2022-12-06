using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Text;

namespace DBCodeGenerator
{
    public class DbConnection
    {
        public static void TestConnection(string connStr)
        {
            Console.WriteLine($"DB Connection string found: {connStr}");

            if (!string.IsNullOrEmpty(connStr))
            {
                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    conn.Open();

                    StringBuilder sql = new StringBuilder();
                    sql.Append("SELECT '[' + SYS.SCHEMAS.NAME + '].[' + SYS.TABLES.NAME + ']' ");
                    sql.Append("from sys.tables ");
                    sql.Append(" JOIN SYS.SCHEMAS ON SYS.TABLES.SCHEMA_ID = SYS.SCHEMAS.SCHEMA_ID ");
                    sql.Append(" WHERE ");
                    sql.Append("SYS.TABLES.TYPE = 'U' ");
                    sql.Append(" ORDER BY sys.schemas.name, sys.tables.name");
                    //sql.Append("SYS.SCHEMAS.NAME = 'DBO' AND ");
                    //sql.Append("(SYS.TABLES.name LIKE '%LOT%' OR ");
                    //sql.Append("sys.tables.name like  '%part%' or ");
                    //sql.Append("sys.tables.name like '%tag%')");

                    Console.WriteLine(sql.ToString());

                    var cmd = new SqlCommand(sql.ToString(), conn);

                    var reader = cmd.ExecuteReaderAsync().GetAwaiter().GetResult();

                    string dbName = conn.Database;
                    List<string> tableNames = new List<string>();
                    string title = $"All User Defined Table Names in database: {dbName}";
                    tableNames.Add(title);
                    tableNames.Add(new string('-', title.Length));

                    while (reader.Read())
                    {
                        tableNames.Add(reader.GetString(0));
                    }
                    
                    reader.Close();

                    conn.Close();

                    string tableListFile = Path.Combine(Constants.OutputPath, $"Database Table List ALL ({dbName}).txt");
                    File.WriteAllLines(tableListFile, tableNames);
                    Console.WriteLine("Database Table List file written: ");
                    Console.WriteLine(tableListFile);
                }
            }
            else
            {
                Console.WriteLine("NO connection string found!!");
            }
        }

        /// <summary>
        /// Query all Table Names (User defined).
        /// </summary>
        /// <param name="connStr"></param>
        /// <returns>List of all User defined tables.</returns>
        public static List<string> QueryAllUserTables(string connStr)
        {
            List<string> tableNames = new List<string>();

            if (!string.IsNullOrEmpty(connStr))
            {
                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    conn.Open();

                    StringBuilder sql = new StringBuilder();
                    sql.Append("SELECT ");
                    sql.Append("sys.schemas.name ");
                    sql.Append(", sys.tables.name ");
                    sql.Append(", sys.tables.object_id ");
                    sql.Append(", '[' + sys.schemas.name + '].[' + sys.tables.name + ']' as 'TableName' ");
                    sql.Append("FROM sys.tables ");
                    sql.Append("JOIN sys.schemas on sys.schemas.schema_id = sys.tables.schema_id ");
                    sql.Append("WHERE sys.tables.type = 'U' ");
                    sql.Append("ORDER BY sys.schemas.name, sys.tables.name ");

                    Console.WriteLine(sql.ToString());

                    var cmd = new SqlCommand(sql.ToString(), conn);

                    var reader = cmd.ExecuteReaderAsync().GetAwaiter().GetResult();

                    int tableIndex = reader.GetOrdinal("TableName");

                    while (reader.Read())
                    {
                        tableNames.Add(reader.GetString(tableIndex));
                    }
                    string dbName = conn.Database;
                    reader.Close();

                    conn.Close();
                }

                DisplayAllTableNames(tableNames);
            }
            else
            {
                Console.WriteLine("NO connection string found!!");
            }

            return tableNames;
        }

        public static void StartCodeGenerationProcess(Dictionary<string, string> configs, string connStr, string path)
        {
            // Read the Column header file for the header text to put for the DTO file.
            string colEnumTemplate = File.ReadAllText(configs[Constants.ColumnEnumTemplateFile_Config]);
            string DTOtemplate = File.ReadAllText(configs[Constants.DTOTemplateFile_Config]);
            string tableConstTemplate = File.ReadAllText(configs[Constants.TableNameTemplateFile_Config]);
            var excludeProperties = File.ReadAllLines(configs[Constants.ExcludePropertiesFile_Config]);

            Dictionary<string, string> excludeProps = ConvertToDictionary(excludeProperties);

            // Read the Table name list file.  List of tables to work on code generation.
            string tableListFile = configs[Constants.TableListFile_Config];
            string[] tablesToProcess = File.ReadAllLines(tableListFile);

            DisplayTableNames(tablesToProcess);

            if (tablesToProcess == null || tablesToProcess.Length == 0)
            {
                Console.WriteLine($"No Tables found to process from file: {tableListFile}");
                return;
            }

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();

                // Generat code for the Table Column Enums.
                GenerateColumnEnums(tablesToProcess, colEnumTemplate, conn, path);

                // Generate code for the table name DTO.
                GenerateDTOs(tablesToProcess, DTOtemplate, excludeProps, conn, path);

                // Generate code for the Table List as constants.
                GenerateTableNames(tablesToProcess, tableConstTemplate, conn, path);
                conn.Close();
            }
        }

        private static void DisplayAllTableNames(IList<string> tableNames)
        {
            string line = new string('-', 100);

            Console.WriteLine(line);
            Console.WriteLine("All User defined Tables in database:...");

            foreach (string eachTable in tableNames)
            {
                Console.WriteLine(eachTable);
            }

            Console.WriteLine(line);
        }

        private static void DisplayTableNames(string[] tablesToProcess)
        {
            Console.WriteLine("Tables to be processed:....");

            foreach (string eachTable in tablesToProcess)
            {
                Console.WriteLine(eachTable);
            }
        }

        private static Dictionary<string, string> ConvertToDictionary(string[] excludeProperties)
        {
            Dictionary<string, string> props = new Dictionary<string, string>();

            char[] delimiter = new char[] { ',' };
            foreach (string eachLine in excludeProperties)
            {
                var tokens = eachLine.Split(delimiter, StringSplitOptions.RemoveEmptyEntries);

                if (tokens != null && tokens.Length > 1)
                {
                    props.Add(tokens[0], tokens[1]);
                }
            }

            return props;
        }

        /// <summary>
        /// Given a list of Table Names generate the Enums of the Column Names.
        /// public enum {tablename}_Columns {
        ///  list of column names,
        /// }
        /// </summary>
        /// <param name="tableNames"></param>
        public static void GenerateColumnEnums(IEnumerable<string> tableNames, string enumTemplate, SqlConnection conn, string path)
        {
            // Create File Name to save code generation to.
            path = Path.Combine(path, "Column Enums");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            foreach (string eachTable in tableNames)
            {
                string fileName = GenerateEnumFileName(path, eachTable);

                // Query Column names for this table.
                ICollection<string> cols = QueryColumnNames(conn, eachTable);

                // In Header file replace these tokens with the following values.
                // <%classname%> replace with class name (eachtable name)
                // <%tablename%> replace with table name (eachtable name)
                // <%CodeGeneration%> replace with generated code.
                string className = GenerateEnumName(eachTable);

                string template = enumTemplate.Replace(Constants.FormatTemplateToken(Constants.ClassName_Template), className, StringComparison.OrdinalIgnoreCase);
                template = template.Replace(Constants.FormatTemplateToken(Constants.TableName_Template), eachTable, StringComparison.OrdinalIgnoreCase);

                StringBuilder lines = new StringBuilder();
                bool first = true;
                foreach (string eachCol in cols)
                {
                    if (first)
                    {
                        lines.AppendLine(string.Concat(Constants.LineTabStart, eachCol));
                        first = false;
                    }
                    else
                    {
                        lines.AppendLine(string.Concat(Constants.LineTabStart, ",", eachCol));
                    }
                }
                template = template.Replace(Constants.FormatTemplateToken(Constants.CodeGeneration_Template), lines.ToString(), StringComparison.OrdinalIgnoreCase);

                File.WriteAllText(fileName, template);

                Console.WriteLine($"Column Enum file written: {fileName}");
            }
        }

        private static ICollection<string> QueryColumnNames(SqlConnection conn, string tableName)
        {
            string sql = $"SELECT sys.columns.name FROM sys.columns WHERE object_id = (SELECT OBJECT_ID FROM SYS.TABLES WHERE NAME = '{tableName}')";

            Console.WriteLine(sql);

            SqlCommand sqlCmd = new SqlCommand(sql, conn);

            var reader = sqlCmd.ExecuteReaderAsync().GetAwaiter().GetResult();

            List<string> cols = new List<string>();
            while (reader.Read())
            {
                cols.Add(reader.GetString(0));
            }

            reader.Close();
            reader.DisposeAsync();

            return cols;
        }

        private static Dictionary<string, string> QueryColumnNameType(SqlConnection conn, string tableName)
        {
            StringBuilder sql = new StringBuilder();
            sql.Append($"SELECT sys.columns.name, sys.types.name, sys.columns.is_nullable ");
            sql.Append(" FROM sys.columns ");
            sql.Append(" JOIN sys.types on sys.types.system_type_id = sys.columns.system_type_id and sys.types.user_type_id = sys.columns.user_type_id ");
            sql.Append($" WHERE object_id = (SELECT OBJECT_ID FROM SYS.TABLES WHERE NAME = '{tableName}')");

            Console.WriteLine(sql.ToString());

            SqlCommand sqlCmd = new SqlCommand(sql.ToString(), conn);

            var reader = sqlCmd.ExecuteReaderAsync().GetAwaiter().GetResult();

            Dictionary<string, string> colTypes = new Dictionary<string, string>();
            while (reader.Read())
            {
                colTypes.Add(reader.GetString(0), ConvertToType(reader.GetString(1), reader.GetBoolean(2)));
            }

            reader.Close();
            reader.DisposeAsync();

            return colTypes;
        }

        private static string ConvertToType(string sqlType, bool isNullable)
        {
            string typ = string.Empty;

            switch (sqlType)
            {
                case "smallint":
                    typ = "Int16";
                    break;

                case "tinyint":
                    typ = "byte";
                    break;

                case "int":
                    typ = "int";
                    break;

                case "bigint":
                    typ = "long";
                    break;

                case "numeric":
                case "money":
                case "decimal":
                    typ = "decimal";
                    break;

                case "real":
                    typ = "float";
                    break;

                case "float":
                    typ = "double";
                    break;

                case "nvarchar":
                case "varchar":
                case "nchar":
                case "ntext":
                case "text":
                    typ = "string";
                    break;

                case "datetime2":
                    typ = typeof(DateTime).Name;
                    break;

                case "varbinary":
                    typ = "byte[]";
                    break;

                case "bit":
                    typ = "bool";
                    break;

                case "xml":
                    typ = "string";
                    break;

                default:
                    typ = "object";
                    break;
            }

            if (isNullable && !typ.Equals("string"))
            {
                typ = string.Concat(typ, "?");
            }

            return typ;
        }

        /// <summary>
        /// Given a list of Table Names generate the respective DTOs in their respective files.
        /// </summary>
        /// <param name="tableNames"></param>
        /// <param name="codeTemplate"></param>
        /// <param name="conn"></param>
        /// <param name="path"></param>
        public static void GenerateDTOs(IEnumerable<string> tableNames, string codeTemplate, Dictionary<string, string> excludeProp, SqlConnection conn, string path)
        {
            path = Path.Combine(path, "DTOs");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            // Template file has <%TableName%> and <%CodeGeneration%>
            string code = string.Empty;

            foreach (string eachTable in tableNames)
            {
                string fileNameDTO = GenerateDTOFileName(path, eachTable);
                string enumName = GenerateEnumName(eachTable);

                // Query the Column Names for this table.
                var cols = QueryColumnNameType(conn, eachTable);

                // <%TableName%>
                code = codeTemplate.Replace(Constants.FormatTemplateToken(Constants.TableName_Template), eachTable, StringComparison.OrdinalIgnoreCase);

                StringBuilder lines = new StringBuilder();

                // Generate properties for each column in the database table for the DTO.
                foreach (string eachCol in cols.Keys)
                {
                    string colTyp = cols[eachCol];

                    if (!ExcludePropertyMatch(eachCol, colTyp, excludeProp))
                    {
                        lines.AppendLine(string.Concat(Constants.LineTabStart, "/// <summary>"));
                        lines.AppendLine(string.Concat(Constants.LineTabStart, $"/// {eachCol}"));
                        lines.AppendLine(string.Concat(Constants.LineTabStart, "/// </summary>"));
                        lines.AppendLine(string.Concat(Constants.LineTabStart, "[DataMember]"));
                        lines.AppendLine(string.Concat(Constants.LineTabStart, $"public {colTyp} {eachCol}", " { get; set; }"));
                        lines.AppendLine(string.Empty);
                    }
                }
                // Generate Constructor
                lines.AppendLine($"{Constants.LineTabStart}/// <summary>");
                lines.AppendLine($"{Constants.LineTabStart}/// Default.");
                lines.AppendLine($"{Constants.LineTabStart}/// </summary>");
                lines.AppendLine($"{Constants.LineTabStart}public {eachTable}DTO()");
                lines.AppendLine(string.Concat(Constants.LineTabStart, "{"));
                lines.AppendLine(string.Concat(Constants.LineTabStart, "}"));

                code = code.Replace(Constants.FormatTemplateToken(Constants.CodeGeneration_Template), lines.ToString(), StringComparison.OrdinalIgnoreCase);

                // Write DTO File for each Table.
                File.WriteAllText(fileNameDTO, code);

                Console.WriteLine("DTO file written: ");
                Console.WriteLine(fileNameDTO);
            }
        }

        /// <summary>
        /// Given a Key and Value check to see if the pair exists in the Dictionary.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="excludeProp"></param>
        /// <returns>Returns true if both Key and Value exist in Dictionary otherwise false.</returns>
        private static bool ExcludePropertyMatch(string key, string value, Dictionary<string, string> excludeProp)
        {
            if (excludeProp.ContainsKey(key))
            {
                var kvp = excludeProp[key];
                return kvp.Contains(value, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        /// <summary>
        /// Given a list of Table Names generate the table Names as const
        /// public const string Table{tableName} = "[dbo].[tablename]";
        /// </summary>
        /// <param name="tableNames"></param>
        /// <param name="codeTemplate"></param>
        /// <param name="conn"></param>
        /// <param name="path"></param>
        public static void GenerateTableNames(IEnumerable<string> tableNames, string codeTemplate, SqlConnection conn, string path)
        {
            string tableFileName = "TableNames.cs";
            string fileTableNames = Path.Combine(path, tableFileName);

            StringBuilder sql = new StringBuilder();
            sql.Append("SELECT sys.tables.name, '[' + SYS.SCHEMAS.NAME + '].[' + SYS.TABLES.NAME + ']' ");
            sql.Append("from sys.tables ");
            sql.Append(" JOIN SYS.SCHEMAS ON SYS.TABLES.SCHEMA_ID = SYS.SCHEMAS.SCHEMA_ID ");
            sql.Append(" WHERE ");
            sql.Append("SYS.TABLES.TYPE = 'U' AND ");

            //sql.Append("SYS.SCHEMAS.NAME = 'DBO' AND ");

            List<string> temp = new List<string>(tableNames);
            List<string> tables = new List<string>();

            temp.ForEach(t => tables.Add(string.Concat("'", t, "'")));

            sql.Append($"SYS.TABLES.name IN ({string.Join(", ", tables)})");
            sql.Append(" ORDER BY sys.schemas.name, sys.tables.name");

            SqlCommand sqlCmd = new SqlCommand(sql.ToString(), conn);
            var reader = sqlCmd.ExecuteReaderAsync().GetAwaiter().GetResult();

            StringBuilder lines = new StringBuilder();
            while (reader.Read())
            {
                string tableName = reader.GetString(0);
                string tableProp = reader.GetString(1);

                lines.AppendLine(string.Concat(Constants.LineTabStart, $"public const string TABLE{tableName} = \"{tableProp}\";"));
            }

            // Table Names Code Generation
            // Replace the following tokens:
            // CodeGeneration
            var code = codeTemplate.Replace(Constants.FormatTemplateToken(Constants.CodeGeneration_Template), lines.ToString(), StringComparison.OrdinalIgnoreCase);

            // Write to file.
            File.WriteAllText(fileTableNames, code);
        }

        private static string GenerateDTOFileName(string path, string tableName)
        {
            return Path.Combine(path, string.Concat(tableName, "DTO.cs"));
        }

        private static string GenerateDTOName(string tableName)
        {
            return string.Concat(tableName, "DTO");
        }

        private static string GenerateEnumFileName(string path, string tableName)
        {
            return Path.Combine(path, string.Concat(tableName, "_Columns.cs"));
        }

        private static string GenerateEnumName(string tableName)
        {
            return string.Concat(tableName, "_Columns");
        }
    }
}