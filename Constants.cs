namespace DBCodeGenerator
{
    public class Constants
    {
        // App.config key names:
        public const string ColumnEnumTemplateFile_Config = "ColumnEnumTemplateFile";
        public const string TableListFile_Config = "TableListFile";
        public const string DTOTemplateFile_Config = "DTOTemplateFile";
        public const string TableNameTemplateFile_Config = "TableNameTemplateFile";
        public const string ExcludePropertiesFile_Config = "ExcludePropertiesFile";

        // Inside Template files are the following Tokens to be replaced with values <%{token name}%>
        public const string ClassName_Template = "ClassName";
        public const string CodeGeneration_Template = "CodeGeneration";
        public const string TableName_Template = "TableName";

        public const string OutputPath = "Code Generation Output";

        // Template Token delimiters
        public const string TokenStart = "<%";
        public const string TokenEnd = "%>";

        // Code Generation tokens
        public static readonly string LineTabStart = new string('\t', 2);

        public static string FormatTemplateToken(string token)
        {
            return string.Concat(TokenStart, token, TokenEnd);
        }
    }
}