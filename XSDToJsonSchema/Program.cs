using Microsoft.CSharp;
using NJsonSchema;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XmlSchemaClassGenerator;

namespace XSDToJsonSchema
{
    class Program
    {
        static void Main(string[] args)
        {
            Generate(new[] { @"C:\Users\Admin\Desktop\json_schema\some.xsd" }, @"C:\Users\Admin\Desktop\json_schema");
        }

        static void Generate(IEnumerable<string> files, string outputFolder)
        {
            string TempOutputFolder = Path.Combine(Path.GetTempPath(), "XSDToJsonSchema");

            if (!Directory.Exists(TempOutputFolder))
            {
                Directory.CreateDirectory(TempOutputFolder);
            }

            var generator = new Generator
            {
                OutputFolder = TempOutputFolder,
                Log = s => System.Console.WriteLine(s),
                GenerateNullables = true,
                NamespaceProvider = new Dictionary<NamespaceKey, string>
                {
                    { new NamespaceKey("http://wadl.dev.java.net/2009/02"), "Wadl" }
                }
                .ToNamespaceProvider(new GeneratorConfiguration { NamespacePrefix = "Wadl" }.NamespaceProvider.GenerateNamespace)
            };

            generator.Generate(files);

            //get type
            var generatedFiles = Directory.EnumerateFiles(TempOutputFolder);

            //Convert to json schema
            foreach (var generatedFile in generatedFiles)
            {
                var type = GetGeneratedFileType(generatedFile);

                var schema = JsonSchema4.FromTypeAsync(type).Result;

                var fileName = Path.GetFileNameWithoutExtension(generatedFile);

                var schemaData = schema.ToJson();

                File.WriteAllText(Path.Combine(outputFolder, fileName + ".json"), schemaData);
            }

            Directory.Delete(TempOutputFolder, true);
        }

        static Type GetGeneratedFileType(string csFile)
        {
            var csc = new CSharpCodeProvider(new Dictionary<string, string>() { { "CompilerVersion", "v4.0" } });

            var cp = new CompilerParameters()
            {
                GenerateExecutable = false,
                GenerateInMemory = true
            };

            cp.ReferencedAssemblies.Add("mscorlib.dll");
            cp.ReferencedAssemblies.Add("System.dll");
            cp.ReferencedAssemblies.Add("System.Xml.dll");
            cp.ReferencedAssemblies.Add("System.ComponentModel.DataAnnotations.dll");

            StringBuilder sb = new StringBuilder();
            sb.Append(File.ReadAllText(csFile));

            var result = csc.CompileAssemblyFromSource(cp, sb.ToString());

            if (result.Errors.Count > 0)
            {
                foreach (CompilerError ce in result.Errors)
                {
                    if (ce.IsWarning) continue;
                    Console.WriteLine("{0}({1},{2}: error {3}: {4}", ce.FileName, ce.Line, ce.Column, ce.ErrorNumber, ce.ErrorText);
                }

                throw new Exception("Errors in generated cs file");
            }

            var generatedTypes = result.CompiledAssembly.GetTypes();

            //if (generatedTypes.Count() == 0 || generatedTypes.Count() > 1)
            //{
            //    throw new Exception("Generated file should contain only one type");
            //}

            return generatedTypes.FirstOrDefault();
        }
    }
}
