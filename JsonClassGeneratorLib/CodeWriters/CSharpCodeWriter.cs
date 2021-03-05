﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Xamasoft.JsonClassGenerator.CodeWriters
{
    public class CSharpCodeWriter : ICodeBuilder
    {
        public string FileExtension
        {
            get { return ".cs"; }
        }

        public string DisplayName
        {
            get { return "C#"; }
        }

        private const string NoRenameAttribute = "[Obfuscation(Feature = \"renaming\", Exclude = true)]";
        private const string NoPruneAttribute = "[Obfuscation(Feature = \"trigger\", Exclude = false)]";

        private static readonly HashSet<string> _reservedKeywords = new HashSet<string>( StringComparer.OrdinalIgnoreCase ) {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue",
            "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally",
            "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long",
            "namespace", "new", "null", "object", "operator", "out", "override", "params", "private", "protected", "public",
            "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct",
            "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using",
            "virtual", "void", "volatile", "while"
        };

        public bool IsReservedKeyword(string word) => _reservedKeywords.Contains(word ?? string.Empty);

        IReadOnlyCollection<string> ICodeBuilder.ReservedKeywords => _reservedKeywords;

        public string GetTypeName(JsonType type, IJsonClassGeneratorConfig config)
        {
            var arraysAsLists = !config.ExplicitDeserialization;

            switch (type.Type)
            {
                case JsonTypeEnum.Anything         : return "object";
                case JsonTypeEnum.Array            : return arraysAsLists ? "List<" + this.GetTypeName(type.InternalType, config) + ">" : this.GetTypeName(type.InternalType, config) + "[]";
                case JsonTypeEnum.Dictionary       : return "Dictionary<string, " + this.GetTypeName(type.InternalType, config) + ">";
                case JsonTypeEnum.Boolean          : return "bool";
                case JsonTypeEnum.Float            : return "double";
                case JsonTypeEnum.Integer          : return "int";
                case JsonTypeEnum.Long             : return "long";
                case JsonTypeEnum.Date             : return "DateTime";
                case JsonTypeEnum.NonConstrained   : return "object";
                case JsonTypeEnum.NullableBoolean  : return "bool?";
                case JsonTypeEnum.NullableFloat    : return "double?";
                case JsonTypeEnum.NullableInteger  : return "int?";
                case JsonTypeEnum.NullableLong     : return "long?";
                case JsonTypeEnum.NullableDate     : return "DateTime?";
                case JsonTypeEnum.NullableSomething: return "object";
                case JsonTypeEnum.Object           : return type.NewAssignedName;
                case JsonTypeEnum.String           : return "string";
                default: throw new NotSupportedException("Unsupported json type: " + type.Type);
            }
        }

        private bool ShouldApplyNoRenamingAttribute(IJsonClassGeneratorConfig config)
        {
            return config.ApplyObfuscationAttributes && !config.ExplicitDeserialization && !config.UsePascalCase;
        }

        private bool ShouldApplyNoPruneAttribute(IJsonClassGeneratorConfig config)
        {
            return config.ApplyObfuscationAttributes && !config.ExplicitDeserialization && config.UseFields;
        }

        public void WriteFileStart(IJsonClassGeneratorConfig config, StringBuilder sw)
        {
            if (config.UseNamespaces)
            {
                // foreach (var line in JsonClassGenerator.FileHeader)
                // {
                //     sw.AppendFormat("// " + line);
                // }
                sw.AppendLine();
                sw.AppendLine("using System;");
                sw.AppendLine("using System.Collections.Generic;");
                if (ShouldApplyNoPruneAttribute(config) || ShouldApplyNoRenamingAttribute(config))
                    sw.AppendLine("using System.Reflection;");
                if (!config.ExplicitDeserialization && config.UseJsonAttributes)
                {
                    sw.AppendLine("using Newtonsoft.Json;");
                    sw.AppendLine("using Newtonsoft.Json.Linq;");
                }

                if (!config.ExplicitDeserialization && config.UseJsonPropertyName)
                {
                    sw.AppendLine("System.Text.Json;");
                }

                if (config.ExplicitDeserialization)
                    sw.AppendLine("using JsonCSharpClassGenerator;");
                if (config.SecondaryNamespace != null && config.HasSecondaryClasses && !config.UseNestedClasses)
                {
                    sw.AppendFormat("using {0};", config.SecondaryNamespace);
                }
            }

            if (config.UseNestedClasses)
            {
                sw.AppendFormat("    {0} class {1}", config.InternalVisibility ? "internal" : "public", config.MainClass);
                sw.AppendLine("    {");
            }
        }

        public void WriteFileEnd(IJsonClassGeneratorConfig config, StringBuilder sw)
        {
            if (config.UseNestedClasses)
            {
                sw.AppendLine("    }");
            }
        }

        public void WriteDeserializationComment(IJsonClassGeneratorConfig config, StringBuilder sw)
        {
            if (config.UseJsonPropertyName)
            {
                sw.AppendLine("// Root myDeserializedClass = JsonSerializer.Deserialize<Root>(myJsonResponse);");
            }
            else
            {
                sw.AppendLine("// Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); ");
            }
        }

        public void WriteNamespaceStart(IJsonClassGeneratorConfig config, StringBuilder sw, bool root)
        {
            sw.AppendLine();
            sw.AppendFormat("namespace {0}", root && !config.UseNestedClasses ? config.Namespace : (config.SecondaryNamespace ?? config.Namespace));
            sw.AppendLine("{");
            sw.AppendLine();
        }

        public void WriteNamespaceEnd(IJsonClassGeneratorConfig config, StringBuilder sw, bool root)
        {
            sw.AppendLine("}");
        }

        public void WriteClass(IJsonClassGeneratorConfig config, StringBuilder sw, JsonType type)
        {
            var visibility = "public";

            var className = type.AssignedName;
            sw.AppendFormat("    {0} class {1}", visibility, className);
            sw.AppendLine("    {");

            var prefix = config.UseNestedClasses && !type.IsRoot ? "            " : "        ";

#if CAN_SUPRESS
            var shouldSuppressWarning = config.InternalVisibility && !config.UseProperties && !config.ExplicitDeserialization;
            if (shouldSuppressWarning)
            {
                sw.AppendFormat("#pragma warning disable 0649");
                if (!config.UsePascalCase) sw.AppendLine();
            }
            if (config.ExplicitDeserialization)
            {
                if (config.UseProperties) WriteClassWithPropertiesExplicitDeserialization(sw, type, prefix);
                else WriteClassWithFieldsExplicitDeserialization(sw, type, prefix);
            }
            else
#endif
            {
                if (config.ImmutableClasses)
                {
                    this.WriteClassConstructor(config, sw, type, prefix);
                }

                this.WriteClassMembers(config, sw, type, prefix);
            }
#if CAN_SUPPRESS
            if (shouldSuppressWarning)
            {
                sw.WriteLine();
                sw.WriteLine("#pragma warning restore 0649");
                sw.WriteLine();
            }
#endif


            if (config.UseNestedClasses && !type.IsRoot)
                sw.AppendLine("        }");

            if (!config.UseNestedClasses)
                sw.AppendLine("    }");

            sw.AppendLine();
        }

        /// <summary>Converts an identifier from JSON into a C#-safe PascalCase identifier.</summary>
        private string GetCSharpPascalCaseName(string name)
        {
            // Check if property is a reserved keyword
            if (this.IsReservedKeyword(name)) name = "@" + name;

            // Check if property name starts with number
            if (!string.IsNullOrEmpty(name) && char.IsDigit(name[0])) name = "_" + name;

            return name;
        }

        /// <summary>Converts a camelCase identifier from JSON into a C#-safe camelCase identifier.</summary>
        private string GetCSharpCamelCaseName(string camelCaseFromJson)
        {
            if (String.IsNullOrEmpty(camelCaseFromJson)) throw new ArgumentException(message: "Value cannot be null or empty.", paramName: nameof(camelCaseFromJson));

            string name = camelCaseFromJson;

            if (Char.IsUpper(name[0])) name = Char.ToLower(name[0]) + name.Substring(startIndex: 1);

            if      (!Char.IsLetter(name[0]))      name = "_" + name;
            else if (this.IsReservedKeyword(name)) name = "@" + name;

            return name;
        }

        public void WriteClassMembers(IJsonClassGeneratorConfig config, StringBuilder sw, JsonType type, string prefix)
        {
            int count = type.Fields.Count;
            int counter = 1;

            foreach (FieldInfo field in type.Fields)
            {
                string fieldMemberName = this.GetCSharpPascalCaseName(field.MemberName);

                if (config.ExamplesInDocumentation)
                {
                    sw.AppendFormat(prefix + "/// <summary>");
                    sw.AppendFormat(prefix + "/// Examples: " + field.GetExamplesText());
                    sw.AppendFormat(prefix + "/// </summary>");
                }

                if (config.UseJsonPropertyName)
                {
                    sw.AppendFormat(prefix + "[JsonPropertyName(\"{0}\")]{1}", field.JsonMemberName, Environment.NewLine);
                }
                else if (config.UseJsonAttributes || field.ContainsSpecialChars) // If the json Member contains special chars -> add this property
                {
                    sw.AppendFormat(prefix + "[JsonProperty(\"{0}\")]{1}", field.JsonMemberName, Environment.NewLine);
                }
               

                if (config.UseFields)
                {
                    sw.AppendFormat(prefix + "public {0} {1}; {2}", field.Type.GetTypeName(), fieldMemberName, Environment.NewLine);
                }
                else
                {
                    sw.AppendFormat(prefix + "public {0} {1} {{ get; set; }} {2}", field.Type.GetTypeName(), fieldMemberName, Environment.NewLine);
                }

                if ((config.UseJsonAttributes || config.UseJsonPropertyName  )&& count != counter)
                {
                    sw.AppendLine();
                }

                ++counter;
            }

        }

        private void WriteClassConstructor(IJsonClassGeneratorConfig config, StringBuilder sw, JsonType type, string prefix)
        {
            sw.AppendFormat(prefix + "public {0}({1}", type.AssignedName, Environment.NewLine);

            foreach (FieldInfo field in type.Fields)
            {

            }

            sw.AppendLine  (prefix + ")");
        }

    }
}
